using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Macro.Models;

namespace Macro.Services
{
    /// <summary>
    /// RecipeCompiler
    /// 역할: 계층적인(Tree) 레시피 데이터를 실행 가능한 평탄화된(Flat) 시퀀스로 변환합니다.
    /// </summary>
    public class RecipeCompiler
    {
        private static readonly Lazy<RecipeCompiler> _instance = new Lazy<RecipeCompiler>(() => new RecipeCompiler());
        public static RecipeCompiler Instance => _instance.Value;

        private RecipeCompiler() { }

        /// <summary>
        /// 컴파일 실행 시 트리를 타고 내려가며 유지해야 할 상태(State)를 캡슐화한 클래스
        /// (Parameter Object Pattern)
        /// </summary>
        private class CompilationContext
        {
            public SequenceGroup? ParentGroup { get; }
            public Dictionary<string, System.Windows.Point> ScopeVariables { get; }
            public List<(string GroupName, string VarName, int Duration, ValueSourceType Source, string TimeoutVar, string JumpId)> Timeouts { get; }

            public CompilationContext(
                SequenceGroup? parentGroup, 
                Dictionary<string, System.Windows.Point>? scopeVariables, 
                List<(string, string, int, ValueSourceType, string, string)>? timeouts)
            {
                ParentGroup = parentGroup;
                ScopeVariables = scopeVariables ?? new Dictionary<string, System.Windows.Point>();
                Timeouts = timeouts ?? new List<(string, string, int, ValueSourceType, string, string)>();
            }

            /// <summary>
            /// 현재 그룹의 정보를 바탕으로 자식 그룹에게 물려줄 새로운 컨텍스트를 파생(Derive)합니다.
            /// </summary>
            public CompilationContext DeriveForChildGroup(SequenceGroup currentGroup)
            {
                // 1. Variable Scope Merge (Copy & Override)
                var newScope = new Dictionary<string, System.Windows.Point>(ScopeVariables);
                if (currentGroup.Variables != null)
                {
                    foreach (var v in currentGroup.Variables)
                    {
                        newScope[v.Name] = new System.Windows.Point(v.X, v.Y);
                    }
                }

                // 2. Timeout Context Stack (Copy & Add)
                var newTimeouts = new List<(string, string, int, ValueSourceType, string, string)>(Timeouts);
                
                // 타임아웃이 설정되어 있거나(>0), 변수로 설정된 경우
                bool hasTimeout = currentGroup.TimeoutMs > 0 || (currentGroup.TimeoutSource == ValueSourceType.Variable && !string.IsNullOrEmpty(currentGroup.TimeoutVariable));

                if (hasTimeout)
                {
                    // Start 스텝에서 타이머를 리셋할 때 사용할 변수명
                    string startTimeVar = $"__GroupStart_{currentGroup.Id:N}";
                    newTimeouts.Add((currentGroup.Name, startTimeVar, currentGroup.TimeoutMs, currentGroup.TimeoutSource, currentGroup.TimeoutVariable, currentGroup.TimeoutJumpId));
                }

                // 3. Effective Group Context (Inheritance Logic)
                // ParentRelative면 부모 설정을 물려받은 임시 그룹을 생성
                SequenceGroup effectiveGroup = currentGroup;
                if (currentGroup.CoordinateMode == CoordinateMode.ParentRelative && ParentGroup != null)
                {
                    effectiveGroup = new SequenceGroup
                    {
                        // Identity
                        Name = currentGroup.Name,
                        // Settings Inherited from Parent
                        CoordinateMode = ParentGroup.CoordinateMode,
                        ContextSearchMethod = ParentGroup.ContextSearchMethod,
                        TargetProcessName = ParentGroup.TargetProcessName,
                        TargetNameSource = ParentGroup.TargetNameSource,
                        TargetProcessNameVariable = ParentGroup.TargetProcessNameVariable,
                        ContextWindowState = ParentGroup.ContextWindowState,
                        ProcessNotFoundJumpName = ParentGroup.ProcessNotFoundJumpName,
                        ProcessNotFoundJumpId = ParentGroup.ProcessNotFoundJumpId,
                        RefWindowWidth = ParentGroup.RefWindowWidth,
                        RefWindowHeight = ParentGroup.RefWindowHeight,
                        // Own Settings
                        PostCondition = currentGroup.PostCondition,
                        TimeoutMs = currentGroup.TimeoutMs,
                        TimeoutSource = currentGroup.TimeoutSource,
                        TimeoutVariable = currentGroup.TimeoutVariable,
                        TimeoutJumpId = currentGroup.TimeoutJumpId
                    };
                }

                return new CompilationContext(effectiveGroup, newScope, newTimeouts);
            }
        }

        /// <summary>
        /// 전체 그룹 리스트를 엔진이 실행 가능한 단일 리스트로 컴파일합니다.
        /// </summary>
        public List<SequenceItem> Compile(IEnumerable<SequenceGroup> groups)
        {
            var result = new List<SequenceItem>();
            if (groups == null) return result;

            foreach (var group in groups)
            {
                // Root Context 생성 (부모 없음)
                var rootContext = new CompilationContext(null, null, null);
                FlattenNodeRecursive(group, result, rootContext);
            }

            return result;
        }

        /// <summary>
        /// 재귀적 평탄화 로직 (Main Loop)
        /// </summary>
        private void FlattenNodeRecursive(ISequenceTreeNode node, List<SequenceItem> result, CompilationContext context)
        {
            if (node is SequenceGroup group)
            {
                // [Global Variable Injection] - 그룹 진입 시 전역 정수 변수 등록
                RegisterGlobalVariables(group);

                // [Log] 디버깅용 로그
                LogGroupContext(group, context.ParentGroup);

                // [Context Calculation] 자식에게 넘겨줄 컨텍스트 계산
                var childContext = context.DeriveForChildGroup(group);

                // [Entry Point Handling] 시작 그룹이면 초기화 로직만 수행
                if (group.IsStartGroup)
                {
                    AddInitializeStep(group, result);
                    return;
                }

                // [Recursion]
                foreach (var child in group.Nodes)
                {
                    FlattenNodeRecursive(child, result, childContext);
                }
            }
            else if (node is SequenceItem item)
            {
                // [Process Item] 아이템 복제 및 주입
                var processedItem = ProcessSequenceItem(item, context);
                if (processedItem != null)
                {
                    result.Add(processedItem);
                }
            }
        }

        #region Helper Methods (Refactored)

        private SequenceItem? ProcessSequenceItem(SequenceItem originalItem, CompilationContext context)
        {
            // 1. Deep Clone
            var item = CloneItem(originalItem);
            if (item == null) return null;

            // 2. Apply Group Context (Naming & Window Settings)
            if (context.ParentGroup != null)
            {
                item.Name = $"{context.ParentGroup.Name}_{item.Name}";
                ApplyWindowContext(item, context.ParentGroup);

                // Group Post-Condition Injection (End Step)
                if (item.IsGroupEnd && context.ParentGroup.PostCondition != null)
                {
                    item.PostCondition = context.ParentGroup.PostCondition;
                }
            }

            // 3. Inject Runtime Variables (for MouseClickAction)
            if (item.Action is MouseClickAction mouseAction)
            {
                mouseAction.RuntimeContextVariables = new Dictionary<string, System.Windows.Point>(context.ScopeVariables);
            }

            // 4. Inject Timeout Logic
            InjectTimeoutLogic(item, context);

            return item;
        }

        private void InjectTimeoutLogic(SequenceItem item, CompilationContext context)
        {
            var timeouts = context.Timeouts;
            if (timeouts == null || timeouts.Count == 0) return;

            // A. Timer Reset Injection (Start Step of a Timeout Group)
            // 현재 스텝이 속한 그룹(가장 마지막에 추가된 타임아웃 컨텍스트)의 시작점이라면 타이머 리셋 액션 추가
            // (TimeoutMs > 0 이거나 Variable Source인 경우)
            if (item.IsGroupStart && context.ParentGroup != null)
            {
                bool hasTimeout = context.ParentGroup.TimeoutMs > 0 || 
                                  (context.ParentGroup.TimeoutSource == ValueSourceType.Variable && !string.IsNullOrEmpty(context.ParentGroup.TimeoutVariable));
                
                if (hasTimeout)
                {
                    // 가장 최근(자신)의 타임아웃 컨텍스트 가져오기
                    var myTimeout = timeouts.Last(); 
                    var setTimeAction = new CurrentTimeAction { VariableName = myTimeout.VarName };

                    if (item.Action is MultiAction multi)
                    {
                        multi.Actions.Insert(0, setTimeAction);
                    }
                    else
                    {
                        var newMulti = new MultiAction();
                        newMulti.Actions.Add(setTimeAction);
                        newMulti.Actions.Add(item.Action);
                        item.Action = newMulti;
                    }
                }
            }

            // B. Timeout Condition Wrapping (Layered Check)
            // Start Step일 경우, '자기 자신'의 타임아웃 검사는 제외해야 함 (이제 막 타이머를 켰으니까)
            int count = timeouts.Count;
            if (item.IsGroupStart)
            {
                count--; // 마지막(현재 그룹) 컨텍스트 제외
            }

            for (int i = 0; i < count; i++)
            {
                var ctx = timeouts[i];
                item.PreCondition = new TimeoutCheckCondition(item.PreCondition, ctx.GroupName, ctx.VarName, ctx.Duration, ctx.Source, ctx.TimeoutVar, ctx.JumpId);
            }
        }

        private static readonly JsonSerializerOptions _cloneOptions = new JsonSerializerOptions { WriteIndented = false };

        private SequenceItem? CloneItem(SequenceItem item)
        {
            try
            {
                var json = JsonSerializer.Serialize(item, _cloneOptions);
                return JsonSerializer.Deserialize<SequenceItem>(json, _cloneOptions);
            }
            catch (Exception ex)
            {
                MacroEngineService.Instance.AddLog($"[Compiler] Clone failed for {item.Name}: {ex.Message}");
                return null;
            }
        }

        private void ApplyWindowContext(SequenceItem item, SequenceGroup context)
        {
            item.CoordinateMode = context.CoordinateMode;
            item.ContextSearchMethod = context.ContextSearchMethod;
            item.TargetProcessName = context.TargetProcessName;
            item.TargetNameSource = context.TargetNameSource;
            item.TargetProcessNameVariable = context.TargetProcessNameVariable;
            item.ContextWindowState = context.ContextWindowState;
            item.ProcessNotFoundJumpName = context.ProcessNotFoundJumpName;
            item.ProcessNotFoundJumpId = context.ProcessNotFoundJumpId;
            item.RefWindowWidth = context.RefWindowWidth;
            item.RefWindowHeight = context.RefWindowHeight;
        }

        private void RegisterGlobalVariables(SequenceGroup group)
        {
            if (group.IntVariables != null)
            {
                foreach (var iv in group.IntVariables)
                {
                    MacroEngineService.Instance.UpdateVariableWithoutSave(iv.Name, iv.Value.ToString());
                }
            }
        }

        private void AddInitializeStep(SequenceGroup group, List<SequenceItem> result)
        {
            if (!string.IsNullOrEmpty(group.StartJumpId))
            {
                var initItem = new SequenceItem(new IdleAction { DelayTimeMs = 0 })
                {
                    Name = "Initialize (Start)",
                    SuccessJumpId = group.StartJumpId
                };
                result.Add(initItem);
            }
        }

        private void LogGroupContext(SequenceGroup group, SequenceGroup? parentContext)
        {
            // 실제 적용되는 모드를 확인하기 위함
            var effectiveMode = group.CoordinateMode;
            if (effectiveMode == CoordinateMode.ParentRelative && parentContext != null)
            {
                effectiveMode = parentContext.CoordinateMode;
            }
            
            // 너무 잦은 로그 방지를 위해 필요한 경우에만 주석 해제하거나 레벨 조정
            // MacroEngineService.Instance.AddLog($"[Compiler] Group: {group.Name}, Mode: {group.CoordinateMode} -> {effectiveMode}");
        }

        #endregion

        /// <summary>
        /// 단일 스텝을 테스트 실행하기 위해 컴파일합니다. (필요한 컨텍스트 주입)
        /// </summary>
        public void CompileSingleStep(SequenceItem item, IEnumerable<SequenceGroup> rootGroups)
        {
            if (item == null || rootGroups == null) return;

            // 1. 부모 그룹 찾기 및 계층 구조(Hierarchy) 구성
            var hierarchy = new Stack<SequenceGroup>();
            var parent = FindParentGroupRecursive(rootGroups, item);

            var current = parent;
            while (current != null)
            {
                hierarchy.Push(current);
                var nextParent = FindParentGroupRecursive(rootGroups, current);
                if (nextParent == null || hierarchy.Contains(nextParent)) break;
                current = nextParent;
            }

            // 2. 컨텍스트 및 변수 상속 (Root -> Leaf 순서 적용)
            var effectiveContext = new SequenceGroup
            {
                CoordinateMode = CoordinateMode.Global,
                ContextSearchMethod = WindowControlSearchMethod.ProcessName,
                ContextWindowState = WindowControlState.Maximize,
                RefWindowWidth = 1920,
                RefWindowHeight = 1080
            };

            var runtimeVars = new Dictionary<string, System.Windows.Point>();

            foreach (var group in hierarchy)
            {
                if (group.Variables != null)
                {
                    foreach (var v in group.Variables)
                    {
                        runtimeVars[v.Name] = new System.Windows.Point(v.X, v.Y);
                    }
                }

                if (group.IntVariables != null)
                {
                    foreach (var iv in group.IntVariables)
                    {
                        MacroEngineService.Instance.UpdateVariableWithoutSave(iv.Name, iv.Value.ToString());
                    }
                }

                if (group.CoordinateMode != CoordinateMode.ParentRelative)
                {
                    // 복사 로직 중복을 피하기 위해 단순화
                    effectiveContext.CoordinateMode = group.CoordinateMode;
                    effectiveContext.ContextSearchMethod = group.ContextSearchMethod;
                    effectiveContext.TargetProcessName = group.TargetProcessName;
                    effectiveContext.TargetNameSource = group.TargetNameSource;
                    effectiveContext.TargetProcessNameVariable = group.TargetProcessNameVariable;
                    effectiveContext.ContextWindowState = group.ContextWindowState;
                    effectiveContext.ProcessNotFoundJumpName = group.ProcessNotFoundJumpName;
                    effectiveContext.ProcessNotFoundJumpId = group.ProcessNotFoundJumpId;
                    effectiveContext.RefWindowWidth = group.RefWindowWidth;
                    effectiveContext.RefWindowHeight = group.RefWindowHeight;
                }
            }

            // 3. 최종 컨텍스트를 아이템에 주입
            if (item.CoordinateMode == CoordinateMode.ParentRelative || item.CoordinateMode == CoordinateMode.WindowRelative)
            {
                ApplyWindowContext(item, effectiveContext);
            }

            // 4. 변수 주입
            if (item.Action is MouseClickAction mouseAction)
            {
                mouseAction.RuntimeContextVariables = runtimeVars;
            }
        }

        private SequenceGroup? FindParentGroupRecursive(IEnumerable<ISequenceTreeNode> nodes, ISequenceTreeNode target)
        {
            foreach (var node in nodes)
            {
                if (node is SequenceGroup group)
                {
                    if (group.Nodes.Contains(target)) return group;

                    var found = FindParentGroupRecursive(group.Nodes, target);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}