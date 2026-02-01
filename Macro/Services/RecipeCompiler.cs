using System;
using System.Collections.Generic;
using System.Linq;
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
                FlattenNodeRecursive(group, result, null, new Dictionary<string, System.Windows.Point>());
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
        private void FlattenNodeRecursive(ISequenceTreeNode node, List<SequenceItem> result, SequenceGroup? parentGroupContext, Dictionary<string, System.Windows.Point> scopeVariables)
        {
            if (node is SequenceGroup group)
            {
                // [Scope Management]
                // 현재 스코프 복제 (상속)
                var currentScope = new Dictionary<string, System.Windows.Point>(scopeVariables ?? new Dictionary<string, System.Windows.Point>());
                
                // 현재 그룹의 변수 덮어쓰기 (Override)
                if (group.Variables != null)
                {
                    foreach (var v in group.Variables)
                    {
                        currentScope[v.Name] = new System.Windows.Point(v.X, v.Y);
                    }
                }

                // [Global Variable Injection]
                // 정수 변수는 전역 컨텍스트(EngineService)에 즉시 등록
                if (group.IntVariables != null)
                {
                    foreach (var iv in group.IntVariables)
                    {
                        MacroEngineService.Instance.UpdateVariable(iv.Name, iv.Value.ToString());
                    }
                }

                // [Context Inheritance]
                // ParentRelative 모드일 경우 부모 설정을 그대로 복사 (임시 객체 사용하지 않고 그룹 객체 속성을 일시적으로 변경? 
                // -> 아니오, 그룹 객체를 변경하면 UI에 영향을 줄 수 있으므로 주의해야 함.
                // 하지만 DashboardViewModel의 기존 로직은 group 객체를 직접 수정했음.
                // 안전을 위해, '상속된 속성값'만 따로 관리하여 자식에게 넘기는 것이 좋으나
                // 호환성을 위해 기존 로직(group 속성 덮어쓰기)을 유지하되, 
                // UI 바인딩된 원본 객체가 아닌지 확인 필요.
                // -> Dashboard는 파일에서 새로 로드한 객체를 사용하므로 수정해도 UI 영향 없음. (OK)
                
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
                    // START 그룹은 내부 노드를 실행하지 않고 점프만 수행
                    return;
                }

                foreach (var child in group.Nodes)
                {
                    FlattenNodeRecursive(child, result, group, currentScope);
                }
            }
            else if (node is SequenceItem item)
            {
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