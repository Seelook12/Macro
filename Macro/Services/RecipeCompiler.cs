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
    /// 핵심 목표:
    /// 1. 중첩 그룹(Nested Group)의 재귀적 평탄화
    /// 2. 좌표 모드(ParentRelative) 및 윈도우 컨텍스트(TargetProcess)의 상속 처리
    /// 3. 변수 스코프(Variable Scope)의 계층적 주입
    /// 4. 기존 레거시 데이터와의 완벽한 호환성 유지
    /// </summary>
    public class RecipeCompiler
    {
        private static readonly Lazy<RecipeCompiler> _instance = new Lazy<RecipeCompiler>(() => new RecipeCompiler());
        public static RecipeCompiler Instance => _instance.Value;

        private RecipeCompiler() { }

        /// <summary>
        /// 전체 그룹 리스트를 엔진이 실행 가능한 단일 리스트로 컴파일합니다.
        /// (DashboardViewModel의 로직 이관)
        /// </summary>
        public List<SequenceItem> Compile(IEnumerable<SequenceGroup> groups)
        {
            var result = new List<SequenceItem>();
            if (groups == null) return result;

            foreach (var group in groups)
            {
                // 최상위 그룹은 부모 컨텍스트가 없으므로 null, 빈 변수 스코프로 시작
                FlattenNodeRecursive(group, result, null, new Dictionary<string, System.Windows.Point>(), new List<(string, int, string)>());
            }

            return result;
        }

        /// <summary>
        /// 단일 스텝을 테스트 실행하기 위해 컴파일합니다. (필요한 컨텍스트 주입)
        /// (TeachingViewModel의 RunSingleStep 로직 이관 및 개선)
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
                // 상위 부모 찾기
                var nextParent = FindParentGroupRecursive(rootGroups, current); 
                
                // 루프 방지 및 종료 조건
                if (nextParent == null || hierarchy.Contains(nextParent)) break;
                
                current = nextParent;
            }

            // 2. 컨텍스트 및 변수 상속 (Root -> Leaf 순서 적용)
            // 임시 컨텍스트 그룹 생성 (상속 누적용)
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
                // [Variable Inheritance]
                if (group.Variables != null)
                {
                    foreach (var v in group.Variables)
                    {
                        runtimeVars[v.Name] = new System.Windows.Point(v.X, v.Y);
                    }
                }

                // [Context Inheritance]
                // 현재 그룹이 ParentRelative라면 이전 단계까지 누적된 effectiveContext를 유지
                // WindowRelative/Global이라면 해당 그룹의 설정을 effectiveContext에 덮어씀
                if (group.CoordinateMode != CoordinateMode.ParentRelative)
                {
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
                // 아이템 자체가 ParentRelative면 계산된 컨텍스트 적용
                // 아이템이 WindowRelative면? 그룹 컨텍스트를 따를지 아이템 자체 설정을 따를지?
                // 현재 로직상 SequenceItem은 자체적인 TargetProcessName 속성이 있지만, 
                // 보통 그룹 설정을 따르도록 Flattening에서 덮어씌움.
                // 따라서 여기서도 덮어씌우는 것이 일관성 있음.
                
                item.CoordinateMode = effectiveContext.CoordinateMode;
                item.ContextSearchMethod = effectiveContext.ContextSearchMethod;
                item.TargetProcessName = effectiveContext.TargetProcessName;
                item.TargetNameSource = effectiveContext.TargetNameSource;
                item.TargetProcessNameVariable = effectiveContext.TargetProcessNameVariable;
                item.ContextWindowState = effectiveContext.ContextWindowState;
                item.ProcessNotFoundJumpName = effectiveContext.ProcessNotFoundJumpName;
                item.ProcessNotFoundJumpId = effectiveContext.ProcessNotFoundJumpId;
                item.RefWindowWidth = effectiveContext.RefWindowWidth;
                item.RefWindowHeight = effectiveContext.RefWindowHeight;
            }

            // 4. 변수 주입 (MouseClickAction)
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

        /// <summary>
        /// 재귀적 평탄화 로직 (Core Logic)
        /// </summary>
        private void FlattenNodeRecursive(ISequenceTreeNode node, List<SequenceItem> result, SequenceGroup? parentGroupContext, Dictionary<string, System.Windows.Point> scopeVariables, List<(string VarName, int Duration, string JumpId)> timeoutContexts)
        {
            if (node is SequenceGroup group)
            {
                // [Scope Management]
                var currentScope = new Dictionary<string, System.Windows.Point>(scopeVariables ?? new Dictionary<string, System.Windows.Point>());
                
                if (group.Variables != null)
                {
                    foreach (var v in group.Variables)
                    {
                        currentScope[v.Name] = new System.Windows.Point(v.X, v.Y);
                    }
                }

                // [Global Variable Injection]
                if (group.IntVariables != null)
                {
                    foreach (var iv in group.IntVariables)
                    {
                        MacroEngineService.Instance.UpdateVariable(iv.Name, iv.Value.ToString());
                    }
                }

                // [Timeout Logic Context Setup] (Clone list for this branch)
                var currentTimeoutContexts = new List<(string VarName, int Duration, string JumpId)>(timeoutContexts);
                
                if (group.TimeoutMs > 0)
                {
                    string startTimeVar = $"__GroupStart_{group.Id:N}";
                    // Add MY timeout to the list passed to children
                    currentTimeoutContexts.Add((startTimeVar, group.TimeoutMs, group.TimeoutJumpId));
                }

                // [Context Inheritance]
                if (group.CoordinateMode == CoordinateMode.ParentRelative && parentGroupContext != null)
                {
                    group.CoordinateMode = parentGroupContext.CoordinateMode;
                    group.ContextSearchMethod = parentGroupContext.ContextSearchMethod;
                    group.TargetProcessName = parentGroupContext.TargetProcessName;
                    group.TargetNameSource = parentGroupContext.TargetNameSource;
                    group.TargetProcessNameVariable = parentGroupContext.TargetProcessNameVariable;
                    group.ContextWindowState = parentGroupContext.ContextWindowState;
                    group.ProcessNotFoundJumpName = parentGroupContext.ProcessNotFoundJumpName;
                    group.ProcessNotFoundJumpId = parentGroupContext.ProcessNotFoundJumpId;
                    group.RefWindowWidth = parentGroupContext.RefWindowWidth;
                    group.RefWindowHeight = parentGroupContext.RefWindowHeight;
                }
                
                MacroEngineService.Instance.AddLog($"[Compiler] Group: {group.Name}, Mode: {group.CoordinateMode}, RefW: {group.RefWindowWidth}");

                // [Entry Point Handling]
                if (group.IsStartGroup)
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
                    return;
                }

                foreach (var child in group.Nodes)
                {
                    FlattenNodeRecursive(child, result, group, currentScope, currentTimeoutContexts);
                }
            }
            else if (node is SequenceItem item)
            {
                // 1. Deep Clone
                try 
                {
                    var options = new JsonSerializerOptions { WriteIndented = false };
                    var json = JsonSerializer.Serialize(item, options);
                    var clone = JsonSerializer.Deserialize<SequenceItem>(json, options);
                    if (clone != null) item = clone;
                }
                catch (Exception ex)
                {
                    MacroEngineService.Instance.AddLog($"[Compiler] Clone failed for {item.Name}: {ex.Message}");
                }

                // [Item Context Injection]
                if (parentGroupContext != null)
                {
                    item.Name = $"{parentGroupContext.Name}_{item.Name}";
                    
                    ApplyContext(item, parentGroupContext);

                    // [Group Post-Condition Injection]
                    if (item.IsGroupEnd && parentGroupContext.PostCondition != null)
                    {
                        item.PostCondition = parentGroupContext.PostCondition;
                    }
                }
                
                // [Variable Injection]
                if (item.Action is MouseClickAction mouseAction)
                {
                    mouseAction.RuntimeContextVariables = new Dictionary<string, System.Windows.Point>(scopeVariables);
                }

                // [Timeout Logic Injection]
                
                // A. Timer Reset Injection (Only for Start Step of a Timeout Group)
                if (item.IsGroupStart && parentGroupContext != null && parentGroupContext.TimeoutMs > 0)
                {
                    string startTimeVar = $"__GroupStart_{parentGroupContext.Id:N}";
                    var setTimeAction = new CurrentTimeAction { VariableName = startTimeVar };
                    
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

                // B. Timeout Condition Wrapping (Layered Check)
                // Wrap in reverse order (Inner-most first, though order doesn't strictly matter for AND logic)
                // Actually, if Outer fails, we should jump to Outer's target.
                // If Inner fails, jump to Inner's target.
                // Wrapping order: 
                // Condition = InnerCheck( Original )
                // Condition = OuterCheck( InnerCheck( Original ) )
                // When OuterCheck runs: if timeout -> Jump Outer. Else -> Run InnerCheck.
                // This is correct. The last applied wrapper runs FIRST.
                // So we should iterate the list normally?
                // List has [Parent, Child].
                // 1. Wrap with Parent -> Parent(Original)
                // 2. Wrap with Child -> Child(Parent(Original))
                // Execution: Child runs -> OK -> Parent runs -> OK -> Original runs.
                // Wait, if Child timeout triggers, it jumps to Child Target. Parent check is skipped?
                // Yes, CheckAsync returns false immediately.
                // Is this desired?
                // If Child timeout happens, we execute Child's fail jump. Correct.
                // If Child is OK, but Parent timeout happens?
                // Parent check runs. If fail, execute Parent's fail jump. Correct.
                
                // So, iterating the list naturally (Parent -> Child) means Child is the OUTERMOST wrapper.
                // Result: ChildCheck( ParentCheck ( Original ) )
                // Execution: Child check -> Parent Check -> Original.
                // This seems fine.
                
                if (timeoutContexts != null)
                {
                    foreach (var ctx in timeoutContexts)
                    {
                        item.PreCondition = new TimeoutCheckCondition(item.PreCondition, ctx.VarName, ctx.Duration, ctx.JumpId);
                    }
                }

                result.Add(item);
            }
        }

        private void ApplyContext(SequenceItem item, SequenceGroup context)
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
    }
}