using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text.Json;
using System.Threading.Tasks;
using Macro.Models;
using Macro.Services;
using Macro.Utils;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ReactiveUI;

namespace Macro.ViewModels
{
    public partial class TeachingViewModel
    {
        #region Logic & Helpers

                        private void UpdateGroupJumpTargets(SequenceGroup? oldGroup = null)

                        {

                            _isUpdatingGroupTargets = true;

                            try

                            {

                                DebugLogger.Log("[Logic] UpdateGroupJumpTargets Started");

                

                                var newEntryTargets = new ObservableCollection<JumpTargetViewModel>();

                                var newExitTargets = new ObservableCollection<JumpTargetViewModel>();

                

                                if (SelectedGroup == null)

                                {

                                    AvailableGroupEntryTargets = newEntryTargets;

                                    AvailableGroupExitTargets = newExitTargets;

                                    return;

                                }

                

                                // 1. Entry Targets (Internal Nodes of SelectedGroup)

                                AddNodesToTargetList(SelectedGroup.Nodes, newEntryTargets, new HashSet<string>(), true);

                                DebugLogger.Log($"[Logic] EntryTargets Updated: Count={newEntryTargets.Count}");

                

                                // 2. Exit Targets (Siblings of SelectedGroup)

                                newExitTargets.Add(new JumpTargetViewModel { Id = "(Next Step)", DisplayName = "(Next Step)", IsGroup = false });

                                newExitTargets.Add(new JumpTargetViewModel { Id = "(Stop Execution)", DisplayName = "(Stop Execution)", IsGroup = false });

                

                                var parent = FindParentGroup(SelectedGroup);

                                IEnumerable<ISequenceTreeNode> siblings = (parent != null) ? parent.Nodes : Groups.Cast<ISequenceTreeNode>();

                

                                // For Exit targets, we also need to consider current value as forced

                                var forceIds = new HashSet<string>();

                                var endStep = SelectedGroup.Nodes.OfType<SequenceItem>().FirstOrDefault(i => i.IsGroupEnd);

                                if (endStep != null && !string.IsNullOrEmpty(endStep.SuccessJumpId)) forceIds.Add(endStep.SuccessJumpId);

                

                                // [Fix] Include SwitchCase JumpIds in Group PostCondition (Current Group)

                                if (SelectedGroup.PostCondition is SwitchCaseCondition groupSwitch)

                                {

                                    foreach (var c in groupSwitch.Cases)

                                    {

                                        if (!string.IsNullOrEmpty(c.JumpId)) forceIds.Add(c.JumpId);

                                    }

                                }

                                if (SelectedGroup.PostCondition != null && !string.IsNullOrEmpty(SelectedGroup.PostCondition.FailJumpId))

                                {

                                    forceIds.Add(SelectedGroup.PostCondition.FailJumpId);

                                }

                

                                // [BugFix] Also Include IDs from the Previous Group (oldGroup) to prevent binding loss (null) during View transition

                                if (oldGroup != null)

                                {

                                    if (oldGroup.PostCondition is SwitchCaseCondition oldSwitch)

                                    {

                                        foreach (var c in oldSwitch.Cases)

                                        {

                                            if (!string.IsNullOrEmpty(c.JumpId)) forceIds.Add(c.JumpId);

                                        }

                                    }

                                    if (oldGroup.PostCondition != null && !string.IsNullOrEmpty(oldGroup.PostCondition.FailJumpId))

                                    {

                                        forceIds.Add(oldGroup.PostCondition.FailJumpId);

                                    }

                                }

                

                                AddNodesToTargetList(siblings, newExitTargets, forceIds, false, SelectedGroup);

        

                        // Atomically swap the collections

                        AvailableGroupEntryTargets = newEntryTargets;

                        AvailableGroupExitTargets = newExitTargets;

        

                        DebugLogger.Log($"[Logic] ExitTargets Updated: Count={AvailableGroupExitTargets.Count}");

        

                        UpdateGroupProxyProperties();

                    }

                    finally

                    {

                        _isUpdatingGroupTargets = false;

                    }

                }



        public void UpdateGroupProxyProperties()

        {

            // [Sync] Update Cached Properties from Model

            if (SelectedGroup != null)

            {

                var startStep = SelectedGroup.Nodes.OfType<SequenceItem>().FirstOrDefault(i => i.IsGroupStart);

                _selectedGroupEntryJumpId = startStep?.SuccessJumpId ?? string.Empty;



                var endStep = SelectedGroup.Nodes.OfType<SequenceItem>().FirstOrDefault(i => i.IsGroupEnd);

                                _selectedGroupExitJumpId = endStep?.SuccessJumpId ?? string.Empty;

                                

                                                                                                // [Sync] Group PostCondition FailJumpId

                                

                                                                                                _selectedGroupPostConditionFailJumpId = SelectedGroup.PostCondition?.FailJumpId ?? string.Empty;

                                

                                                                

                                

                                                                                                // [Sync] Group PostCondition VariableName

                                

                                                                                                if (SelectedGroup.PostCondition is VariableCompareCondition vcc) _selectedGroupPostConditionVariableName = vcc.VariableName;

                                

                                                                                                else if (SelectedGroup.PostCondition is SwitchCaseCondition scc) _selectedGroupPostConditionVariableName = scc.TargetVariableName;

                                

                                                                                                else _selectedGroupPostConditionVariableName = string.Empty;

                                

                                                                

                                

                                                                                                // [Fix] Sync SwitchCase ViewModels for protection

                                

                                                                

                                

                                                                SyncGroupPostConditionCases();

                                

                                                            }

                                

                                                            else

                                

                                                            {

                                

                                                                _selectedGroupEntryJumpId = string.Empty;

                                

                                                                _selectedGroupExitJumpId = string.Empty;

                                

                                                                _selectedGroupPostConditionFailJumpId = string.Empty;

                                

                                                                _selectedGroupPostConditionVariableName = string.Empty;

                                

                                                                GroupPostConditionCases.Clear();

                                

                                                            }

                                

                                                

                                

                                                            this.RaisePropertyChanged(nameof(SelectedGroupEntryJumpId));

                                

                                                            this.RaisePropertyChanged(nameof(SelectedGroupExitJumpId));

                                

                                                            this.RaisePropertyChanged(nameof(SelectedGroupPostConditionFailJumpId));

                                

                                                            this.RaisePropertyChanged(nameof(SelectedGroupPostConditionVariableName));

                        }



        private void SyncGroupPostConditionCases()

        {

            if (SelectedGroup?.PostCondition is SwitchCaseCondition switchCase)

            {

                // 기존 리스트와 비교하여 갱신 (또는 단순 재생성)

                var vms = switchCase.Cases.Select(c => new SwitchCaseItemViewModel(c, () => _isUpdatingGroupTargets)).ToList();

                

                GroupPostConditionCases.Clear();

                foreach (var vm in vms) GroupPostConditionCases.Add(vm);

            }

            else

            {

                GroupPostConditionCases.Clear();

            }

        }



                                private void AddNodesToTargetList(IEnumerable<ISequenceTreeNode> nodes, ObservableCollection<JumpTargetViewModel> targetList, HashSet<string> forceIncludeIds, bool isEntrySelection, ISequenceTreeNode? excludeNode = null)



                                {



                                    // 1. Add Valid Siblings



                                    foreach (var node in nodes)



                                    {



                                        if (node == excludeNode) continue;



                        



                                        if (node is SequenceItem item)



                                        {



                                            // For Entry selection: Hide Start and End



                                            if (isEntrySelection && (item.IsGroupStart || item.IsGroupEnd)) continue;



                        



                                            // For General/Exit selection: Hide Start



                                            if (!isEntrySelection && item.IsGroupStart) continue;



                        



                                            string displayName = item.IsGroupEnd ? "(Finish Group)" : $"📄 {item.Name}";



                                            targetList.Add(new JumpTargetViewModel { Id = item.Id.ToString(), DisplayName = displayName, IsGroup = false });



                                        }



                                        else if (node is SequenceGroup group)



                                        {



                                            var startNode = group.Nodes.FirstOrDefault();



                                            if (startNode != null)



                                            {



                                                targetList.Add(new JumpTargetViewModel { Id = startNode.Id.ToString(), DisplayName = $"📁 {group.Name}", IsGroup = true });



                                            }



                                        }



                                    }



                        



                                    // 2. Handle Forced IDs (Current Settings)



                                    foreach (var id in forceIncludeIds)



                                    {



                                        if (string.IsNullOrEmpty(id) || id == "(Next Step)" || id == "(Stop Execution)" || id == "(Ignore & Continue)") continue;



                                        



                                        // If already added as a valid sibling, skip



                                        if (targetList.Any(t => t.Id == id)) continue;



                        



                                        var node = FindNodeByIdRecursive(Groups, id);



                                        if (node != null)



                                        {



                                            string name = node is SequenceGroup g ? $"📁 {g.Name}" : $"📄 {node.Name}";



                                            targetList.Add(new JumpTargetViewModel { Id = id, DisplayName = $"[외부] {name}", IsGroup = node is SequenceGroup });



                                        }



                                        else



                                        {



                                            targetList.Add(new JumpTargetViewModel { Id = id, DisplayName = $"[알 수 없음] {id}", IsGroup = false });



                                        }



                                    }



                                }



        private ISequenceTreeNode? FindNodeByIdRecursive(IEnumerable<ISequenceTreeNode> nodes, string id)

        {

            foreach (var node in nodes)

            {

                if (node.Id.ToString() == id) return node;

                if (node is SequenceGroup group)

                {

                    var found = FindNodeByIdRecursive(group.Nodes, id);

                    if (found != null) return found;

                }

            }

            return null;

        }



        private void UpdateJumpTargets()
        {
            if (_isLoading) return;

            DebugLogger.Log("[Logic] UpdateJumpTargets Started");

            _isUpdatingGroupTargets = true;
            try
            {
                var forceIncludeIds = new HashSet<string>();
                if (SelectedSequence != null)
                {
                    if (!string.IsNullOrEmpty(SelectedSequence.SuccessJumpId)) forceIncludeIds.Add(SelectedSequence.SuccessJumpId);
                    
                    if (SelectedSequence.PreCondition != null)
                    {
                        if (!string.IsNullOrEmpty(SelectedSequence.PreCondition.FailJumpId)) forceIncludeIds.Add(SelectedSequence.PreCondition.FailJumpId);
                        if (SelectedSequence.PreCondition is SwitchCaseCondition preSwitch)
                        {
                            foreach (var c in preSwitch.Cases) if (!string.IsNullOrEmpty(c.JumpId)) forceIncludeIds.Add(c.JumpId);
                        }
                    }

                    if (SelectedSequence.Action != null && !string.IsNullOrEmpty(SelectedSequence.Action.FailJumpId)) forceIncludeIds.Add(SelectedSequence.Action.FailJumpId);
                    
                    if (SelectedSequence.PostCondition != null)
                    {
                        if (!string.IsNullOrEmpty(SelectedSequence.PostCondition.FailJumpId)) forceIncludeIds.Add(SelectedSequence.PostCondition.FailJumpId);
                        if (SelectedSequence.PostCondition is SwitchCaseCondition postSwitch)
                        {
                            foreach (var c in postSwitch.Cases) if (!string.IsNullOrEmpty(c.JumpId)) forceIncludeIds.Add(c.JumpId);
                        }
                    }
                }

                // [Fix] Race Condition Prevention
                // Create a NEW collection instance instead of Clearing.
                // This ensures that the View bound to the *previous* JumpTargets instance 
                // doesn't see an empty list while it's still unloading/unbinding.
                var newJumpTargets = new ObservableCollection<JumpTargetViewModel>();

                newJumpTargets.Add(new JumpTargetViewModel { Id = "(Next Step)", DisplayName = "(Next Step)", IsGroup = false });
                newJumpTargets.Add(new JumpTargetViewModel { Id = "(Ignore & Continue)", DisplayName = "(Ignore & Continue)", IsGroup = false });
                newJumpTargets.Add(new JumpTargetViewModel { Id = "(Stop Execution)", DisplayName = "(Stop Execution)", IsGroup = false });

                // Strict Sibling Logic
                IEnumerable<ISequenceTreeNode> nodes;

                // [Fix] START Group Exception: Allow jumping to any Root Level Group
                if (SelectedGroup != null && SelectedGroup.IsStartGroup && SelectedSequence == null)
                {
                    nodes = Groups.Cast<ISequenceTreeNode>();
                    // Exclude START group itself from the list? 
                    // AddNodesToTargetList handles 'excludeNode' logic.
                    AddNodesToTargetList(nodes, newJumpTargets, forceIncludeIds, false, SelectedGroup);
                }
                else
                {
                    var parentGroup = SelectedSequence != null ? FindParentGroup(SelectedSequence) : SelectedGroup;
                    nodes = (parentGroup != null) ? parentGroup.Nodes : Groups.Cast<ISequenceTreeNode>();
                    AddNodesToTargetList(nodes, newJumpTargets, forceIncludeIds, false, SelectedSequence);
                }

                // Atomically swap the collection
                JumpTargets = newJumpTargets;

                DebugLogger.Log($"[Logic] JumpTargets Updated: Count={JumpTargets.Count}");

                UpdateStepProxyProperties();
            }
            finally
            {
                _isUpdatingGroupTargets = false;
            }
        }

        public void UpdateStepProxyProperties()
        {
            // [Sync] Update Proxy Properties with Guard
            if (SelectedSequence != null)
            {
                _selectedStepSuccessJumpId = SelectedSequence.SuccessJumpId ?? string.Empty;
                _selectedStepPreConditionFailJumpId = SelectedSequence.PreCondition?.FailJumpId ?? string.Empty;
                _selectedStepActionFailJumpId = SelectedSequence.Action?.FailJumpId ?? string.Empty;
                _selectedStepPostConditionFailJumpId = SelectedSequence.PostCondition?.FailJumpId ?? string.Empty;

                DebugLogger.Log($"[Logic] Sync Proxy Props: SuccessJumpId='{_selectedStepSuccessJumpId}'");
            }
            else
            {
                _selectedStepSuccessJumpId = string.Empty;
                _selectedStepPreConditionFailJumpId = string.Empty;
                _selectedStepActionFailJumpId = string.Empty;
                _selectedStepPostConditionFailJumpId = string.Empty;
            }

            this.RaisePropertyChanged(nameof(SelectedStepSuccessJumpId));
            this.RaisePropertyChanged(nameof(SelectedStepPreConditionFailJumpId));
            this.RaisePropertyChanged(nameof(SelectedStepActionFailJumpId));
            this.RaisePropertyChanged(nameof(SelectedStepPostConditionFailJumpId));

            SyncStepSwitchCases();
        }

        private void SyncStepSwitchCases()
        {
            // 1. Pre-Condition
            if (SelectedSequence?.PreCondition is SwitchCaseCondition preSwitch)
            {
                var vms = preSwitch.Cases.Select(c => new SwitchCaseItemViewModel(c, () => _isUpdatingGroupTargets)).ToList();
                StepPreSwitchCases.Clear();
                foreach (var vm in vms) StepPreSwitchCases.Add(vm);
            }
            else
            {
                StepPreSwitchCases.Clear();
            }

            // 2. Post-Condition
            if (SelectedSequence?.PostCondition is SwitchCaseCondition postSwitch)
            {
                var vms = postSwitch.Cases.Select(c => new SwitchCaseItemViewModel(c, () => _isUpdatingGroupTargets)).ToList();
                StepPostSwitchCases.Clear();
                foreach (var vm in vms) StepPostSwitchCases.Add(vm);
            }
            else
            {
                StepPostSwitchCases.Clear();
            }
        }

        public void UpdateAvailableCoordinateVariables()
        {
            if (_isLoading) return;

            var variables = new List<CoordinateVariable>();
            var group = SelectedGroup ?? (SelectedSequence != null ? FindParentGroup(SelectedSequence) : null);

            while (group != null)
            {
                // 상위 그룹의 변수가 리스트 뒤에 추가되거나, 
                // 혹은 상위 그룹부터 수집해서 하위 그룹 변수로 덮어쓸 수도 있음.
                // 여기서는 단순히 모든 접근 가능한 변수를 리스트업 (Name 충돌 시 하위 그룹 우선 표시 등의 정책은 추후 고려)
                
                // 역순으로 삽입하여 가장 가까운 그룹(자신)의 변수가 먼저 오도록 함
                foreach (var v in group.Variables.Reverse())
                {
                    variables.Insert(0, v);
                }

                var parent = FindParentGroup(group);
                if (parent == group) break; // Safety check
                group = parent;
            }

            AvailableCoordinateVariables.Clear();
            foreach (var v in variables)
            {
                AvailableCoordinateVariables.Add(v);
            }
        }

        public void UpdateAvailableIntVariables()
        {
            if (_isLoading) return;

            var variables = new List<string>();
            
            // 1. Add Global Variables
            foreach (var v in DefinedVariables)
            {
                if (!variables.Contains(v.Name)) variables.Add(v.Name);
            }

            // 2. Add Group Local Variables (Hierarchy)
            var group = SelectedGroup ?? (SelectedSequence != null ? FindParentGroup(SelectedSequence) : null);

            var localVars = new List<string>();
            while (group != null)
            {
                if (group.IntVariables != null)
                {
                    foreach (var v in group.IntVariables.Reverse())
                    {
                        // Add to temporary local list (preserving nearest scope first)
                        if (!localVars.Contains(v.Name)) localVars.Insert(0, v.Name);
                    }
                }
                var parent = FindParentGroup(group);
                if (parent == group) break;
                group = parent;
            }

            // Merge Local Variables
            foreach (var v in localVars)
            {
                if (!variables.Contains(v)) variables.Add(v);
            }

            AvailableIntVariables.Clear();
            foreach (var v in variables)
            {
                AvailableIntVariables.Add(v);
            }
        }

        private async Task RunSingleStepAsync(SequenceItem item)
        {
            if (item == null || Groups == null) return;

            // [Refactoring] Use RecipeCompiler to prepare the item for execution
            // This ensures consistency between Test Run (here) and Actual Run (Dashboard)
            RecipeCompiler.Instance.CompileSingleStep(item, Groups);

            await MacroEngineService.Instance.RunSingleStepAsync(item);
        }

        private void LoadData()
        {
            _isLoading = true;
            try
            {
                Groups.Clear();
                SelectedGroup = null;
                SelectedSequence = null;

                var currentRecipe = RecipeManager.Instance.CurrentRecipe;
                if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath))
                {
                    CurrentRecipeName = "No Recipe Selected";
                    return;
                }

                CurrentRecipeName = currentRecipe.FileName;

                try
                {
                    if (File.Exists(currentRecipe.FilePath))
                    {
                        var json = File.ReadAllText(currentRecipe.FilePath);
                        if (!string.IsNullOrWhiteSpace(json) && json != "{}")
                        {
                            var options = GetJsonOptions();

                            try
                            {
                                var loadedGroups = JsonSerializer.Deserialize<List<SequenceGroup>>(json, options);
                                if (loadedGroups != null && loadedGroups.Count > 0)
                                {
                                    foreach (var g in loadedGroups)
                                    {
                                        EnsureGroupStructure(g);
                                        Groups.Add(g);
                                    }
                                }
                                else
                                {
                                    throw new Exception("Not a group list");
                                }
                            }
                            catch
                            {
                                try
                                {
                                    var loadedItems = JsonSerializer.Deserialize<List<SequenceItem>>(json, options);
                                    if (loadedItems != null)
                                    {
                                        var defaultGroup = new SequenceGroup { Name = "Default Group" };

                                        defaultGroup.Nodes.Add(new SequenceItem(new IdleAction()) { Name = "Start", IsGroupStart = true, IsEnabled = true });

                                        if (loadedItems.Count > 0)
                                        {
                                            var first = loadedItems[0];
                                            defaultGroup.CoordinateMode = first.CoordinateMode;
                                            defaultGroup.ContextSearchMethod = first.ContextSearchMethod;
                                            defaultGroup.TargetProcessName = first.TargetProcessName;
                                            defaultGroup.ContextWindowState = first.ContextWindowState;
                                            defaultGroup.RefWindowWidth = first.RefWindowWidth;
                                            defaultGroup.RefWindowHeight = first.RefWindowHeight;
                                        }

                                        foreach (var item in loadedItems)
                                        {
                                            defaultGroup.Nodes.Add(item);
                                        }

                                        defaultGroup.Nodes.Add(new SequenceItem(new IdleAction()) { Name = "End", IsGroupEnd = true, IsEnabled = true });

                                        Groups.Add(defaultGroup);
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load recipe: {ex.Message}");
                }

                if (Groups.Count == 0 || !Groups[0].IsStartGroup)
                {
                    var startGroup = new SequenceGroup { Name = "START", IsStartGroup = true };
                    Groups.Insert(0, startGroup);
                }
                else if (Groups.Count > 0 && Groups[0].IsStartGroup)
                {
                    Groups[0].Name = "START";
                }

                SanitizeLoadedData();

                LoadVariables();
            }
            finally
            {
                _isLoading = false;
                UpdateJumpTargets();
                UpdateGroupJumpTargets();
                UpdateAvailableIntVariables();
                UpdateAvailableCoordinateVariables();
            }
        }

        private void SanitizeLoadedData()
        {
            var seenIds = new HashSet<string>();
            foreach (var group in Groups)
            {
                SanitizeGroupIdsRecursive(group, seenIds);
            }
        }

        private void SanitizeGroupIdsRecursive(SequenceGroup group, HashSet<string> seenIds)
        {
            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem item)
                {
                    string id = item.Id.ToString();
                    if (seenIds.Contains(id))
                    {
                        item.ResetId();
                        string newId = item.Id.ToString();
                        UpdateJumpReferencesInGroup(group, id, newId);
                    }
                    else
                    {
                        seenIds.Add(id);
                    }

                    // [Sanitize] SwitchCaseItem null JumpId Fix
                    if (item.PreCondition is SwitchCaseCondition preSwitch)
                    {
                        foreach (var c in preSwitch.Cases) if (c.JumpId == null) c.JumpId = string.Empty;
                    }
                    if (item.PostCondition is SwitchCaseCondition postSwitch)
                    {
                        foreach (var c in postSwitch.Cases) if (c.JumpId == null) c.JumpId = string.Empty;
                    }
                }
                else if (node is SequenceGroup subGroup)
                {
                    SanitizeGroupIdsRecursive(subGroup, seenIds);
                }
            }
        }

        private void UpdateJumpReferencesInGroup(SequenceGroup group, string oldId, string newId)
        {
            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem item)
                {
                    if (item.SuccessJumpId == oldId) item.SuccessJumpId = newId;
                    
                    UpdateConditionJumpReferences(item.PreCondition, oldId, newId);
                    if (item.Action?.FailJumpId == oldId) item.Action.FailJumpId = newId;
                    UpdateConditionJumpReferences(item.PostCondition, oldId, newId);
                }
            }
        }

        private void UpdateConditionJumpReferences(IMacroCondition? condition, string oldId, string newId)
        {
            if (condition == null) return;

            if (condition.FailJumpId == oldId) condition.FailJumpId = newId;

            if (condition is SwitchCaseCondition switchCase)
            {
                foreach (var c in switchCase.Cases)
                {
                    if (c.JumpId == oldId) c.JumpId = newId;
                }
            }
        }

        private void EnsureGroupStructure(SequenceGroup group)
        {
            if (group.IsStartGroup) return;

            foreach (var node in group.Nodes)
            {
                if (node is SequenceGroup subGroup)
                {
                    EnsureGroupStructure(subGroup);
                }
            }

            if (!group.Nodes.OfType<SequenceItem>().Any(i => i.IsGroupStart))
            {
                var startStep = new SequenceItem(new IdleAction()) { Name = "Start", IsGroupStart = true, IsEnabled = true };
                group.Nodes.Insert(0, startStep);
            }

            if (!group.Nodes.OfType<SequenceItem>().Any(i => i.IsGroupEnd))
            {
                var endStep = new SequenceItem(new IdleAction()) { Name = "End", IsGroupEnd = true, IsEnabled = true };
                group.Nodes.Add(endStep);
            }
        }

        private async Task SaveSequencesAsync()
        {
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath))
            {
                return;
            }

            try
            {
                var recipeDir = Path.GetDirectoryName(currentRecipe.FilePath);
                if (recipeDir != null)
                {
                    foreach (var group in Groups)
                    {
                        ConvertPathRecursive(group, recipeDir);
                    }
                }

                var json = JsonSerializer.Serialize(Groups, GetJsonOptions());
                await File.WriteAllTextAsync(currentRecipe.FilePath, json);

                SaveVariables();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save recipe: {ex.Message}");
            }
        }

        private void ConvertPathRecursive(SequenceGroup group, string baseDir)
        {
            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem item)
                {
                    ConvertPathToRelative(item.PreCondition, baseDir);
                    ConvertPathToRelative(item.PostCondition, baseDir);
                }
                else if (node is SequenceGroup subGroup)
                {
                    ConvertPathRecursive(subGroup, baseDir);
                }
            }
        }

        private void ConvertPathToRelative(IMacroCondition? condition, string baseDir)
        {
            if (condition is ImageMatchCondition imgMatch)
            {
                if (!string.IsNullOrEmpty(imgMatch.ImagePath) && Path.IsPathRooted(imgMatch.ImagePath))
                {
                    var fileDir = Path.GetDirectoryName(imgMatch.ImagePath);
                    if (string.Equals(fileDir?.TrimEnd('\\', '/'), baseDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    {
                        imgMatch.ImagePath = Path.GetFileName(imgMatch.ImagePath);
                    }
                }
            }
        }

        private void LoadVariables()
        {
            DefinedVariables.Clear();
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null) return;

            var varsPath = Path.ChangeExtension(currentRecipe.FilePath, ".vars.json");
            if (File.Exists(varsPath))
            {
                try
                {
                    var json = File.ReadAllText(varsPath);
                    var vars = JsonSerializer.Deserialize<List<VariableDefinition>>(json, GetJsonOptions());
                    if (vars != null)
                    {
                        foreach (var v in vars) DefinedVariables.Add(v);
                    }
                }
                catch { }
            }
        }

        private void SaveVariables()
        {
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null) return;

            var varsPath = Path.ChangeExtension(currentRecipe.FilePath, ".vars.json");
            try
            {
                var json = JsonSerializer.Serialize(DefinedVariables, GetJsonOptions());
                File.WriteAllText(varsPath, json);
            }
            catch { }
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 한글 그대로 저장
            };
        }

        public SequenceGroup? FindParentGroup(ISequenceTreeNode? item)
        {
            if (item == null) return null;
            return FindParentGroupRecursive(Groups, item);
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

        public SequenceGroup? GetEffectiveGroupContext(SequenceItem item)
        {
            var parent = FindParentGroup(item);
            return ResolveGroupContext(parent);
        }

        public SequenceGroup? ResolveGroupContext(SequenceGroup? group)
        {
            if (group == null) return null;

            if (group.CoordinateMode == CoordinateMode.ParentRelative)
            {
                var parent = FindParentGroup(group);
                // If parent is null (root) but still set to ParentRelative, fallback to itself
                if (parent == null) return group;

                return ResolveGroupContext(parent);
            }

            return group;
        }

        private void NotifyTypeChanges()
        {
            this.RaisePropertyChanged(nameof(SelectedPreConditionType));
            this.RaisePropertyChanged(nameof(SelectedActionType));
            this.RaisePropertyChanged(nameof(SelectedPostConditionType));
            // UpdateJumpTargets(); // Removed to maintain static list
        }

        private string GetConditionType(IMacroCondition? condition)
        {
            return condition switch
            {
                DelayCondition => "Delay",
                ImageMatchCondition => "Image Match",
                GrayChangeCondition => "Gray Change",
                VariableCompareCondition => "Variable Compare",
                SwitchCaseCondition => "Switch Case",
                ProcessRunningCondition => "Process Running",
                _ => "None"
            };
        }

        private string GetActionType(IMacroAction? action)
        {
            return action switch
            {
                IdleAction => "Idle",
                MouseClickAction => "Mouse Click",
                KeyPressAction => "Key Press",
                TextTypeAction => "Text Type",
                VariableSetAction => "Variable Set",
                WindowControlAction => "Window Control",
                MultiAction => "Multi Action",
                _ => "Idle"
            };
        }

        private void SetPreCondition(string type)
        {
            if (SelectedSequence == null) return;

            SelectedSequence.PreCondition = type switch
            {
                "Delay" => new DelayCondition { DelayTimeMs = 1000 },
                "Image Match" => new ImageMatchCondition { Threshold = 0.9 },
                "Gray Change" => new GrayChangeCondition { Threshold = 10.0 },
                "Variable Compare" => new VariableCompareCondition(),
                "Switch Case" => new SwitchCaseCondition(),
                "Process Running" => new ProcessRunningCondition(),
                _ => null
            };
            this.RaisePropertyChanged(nameof(SelectedPreConditionType));
            UpdateStepProxyProperties();
        }

        private void SetPostCondition(string type)
        {
            if (SelectedSequence == null) return;

            SelectedSequence.PostCondition = type switch
            {
                "Delay" => new DelayCondition { DelayTimeMs = 500 },
                "Image Match" => new ImageMatchCondition { Threshold = 0.9 },
                "Gray Change" => new GrayChangeCondition { Threshold = 10.0 },
                "Variable Compare" => new VariableCompareCondition(),
                "Switch Case" => new SwitchCaseCondition(),
                "Process Running" => new ProcessRunningCondition(),
                _ => null
            };
            this.RaisePropertyChanged(nameof(SelectedPostConditionType));
            UpdateStepProxyProperties();
        }

        private void SetGroupPostCondition(string type)
        {
            if (SelectedGroup == null) return;

            SelectedGroup.PostCondition = type switch
            {
                "Delay" => new DelayCondition { DelayTimeMs = 500 },
                "Image Match" => new ImageMatchCondition { Threshold = 0.9 },
                "Gray Change" => new GrayChangeCondition { Threshold = 10.0 },
                "Variable Compare" => new VariableCompareCondition(),
                "Switch Case" => new SwitchCaseCondition(),
                "Process Running" => new ProcessRunningCondition(),
                _ => null
            };
            this.RaisePropertyChanged(nameof(SelectedGroupPostConditionType));
            UpdateGroupProxyProperties();
        }

        private void SetAction(string type)
        {
            if (SelectedSequence == null) return;

            if (type == "Idle" && !(SelectedSequence.Action is IdleAction))
            {
                SelectedSequence.Action = new IdleAction();
            }
            else if (type == "Mouse Click" && !(SelectedSequence.Action is MouseClickAction))
            {
                SelectedSequence.Action = new MouseClickAction();
            }
            else if (type == "Key Press" && !(SelectedSequence.Action is KeyPressAction))
            {
                SelectedSequence.Action = new KeyPressAction();
            }
            else if (type == "Text Type" && !(SelectedSequence.Action is TextTypeAction))
            {
                SelectedSequence.Action = new TextTypeAction();
            }
            else if (type == "Variable Set" && !(SelectedSequence.Action is VariableSetAction))
            {
                SelectedSequence.Action = new VariableSetAction();
            }
            else if (type == "Window Control" && !(SelectedSequence.Action is WindowControlAction))
            {
                var action = new WindowControlAction();
                SelectedSequence.Action = action;
                RefreshTargetListCommand.Execute(action).Subscribe();
            }
            else if (type == "Multi Action" && !(SelectedSequence.Action is MultiAction))
            {
                SelectedSequence.Action = new MultiAction();
            }
            this.RaisePropertyChanged(nameof(SelectedActionType));
            UpdateStepProxyProperties();
        }

        private void SaveImageToRecipe(ImageMatchCondition condition, string sourcePath)
        {
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;

            if (currentRecipe != null && !string.IsNullOrEmpty(currentRecipe.FilePath))
            {
                var recipeDir = Path.GetDirectoryName(currentRecipe.FilePath);
                if (recipeDir != null)
                {
                    string oldPath = condition.ImagePath;

                    var fileName = Path.GetFileNameWithoutExtension(sourcePath);
                    var ext = Path.GetExtension(sourcePath);
                    var newFileName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}{ext}";

                    var destPath = Path.Combine(recipeDir, newFileName);

                    try
                    {
                        File.Copy(sourcePath, destPath, true);

                        ImageSearchService.ClearCache();

                        condition.ImagePath = newFileName;

                        if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath))
                        {
                            bool isUsedElsewhere = IsImagePathUsed(oldPath, condition);
                            if (!isUsedElsewhere)
                            {
                                try { File.Delete(oldPath); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"이미지 저장 실패: {ex.Message}");
                        condition.ImagePath = sourcePath;
                    }
                }
            }
            else
            {
                condition.ImagePath = sourcePath;
            }
        }

        private bool IsImagePathUsed(string path, object currentCondition)
        {
            foreach (var group in Groups)
            {
                if (CheckGroupForImageRecursive(group, path, currentCondition)) return true;
            }
            return false;
        }

        private bool CheckGroupForImageRecursive(SequenceGroup group, string path, object currentCondition)
        {
            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem seq)
                {
                    if (IsPathMatch(seq.PreCondition, path, currentCondition)) return true;
                    if (IsPathMatch(seq.PostCondition, path, currentCondition)) return true;
                }
                else if (node is SequenceGroup subGroup)
                {
                    if (CheckGroupForImageRecursive(subGroup, path, currentCondition)) return true;
                }
            }
            return false;
        }

        private bool IsPathMatch(IMacroCondition? condition, string path, object currentCondition)
        {
            if (condition == null || condition == currentCondition) return false;
            if (condition is ImageMatchCondition imgMatch)
            {
                return imgMatch.ImagePath == path;
            }
            return false;
        }

        private void DeleteImageIfOrphaned(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (!IsImagePathUsed(path, null!))
            {
                try { File.Delete(path); } catch { }
            }
        }

        private void CopySequence()
        {
            if (SelectedSequence != null)
            {
                try
                {
                    _clipboardJson = JsonSerializer.Serialize(SelectedSequence, GetJsonOptions());
                    _clipboardIsGroup = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
                }
            }
        }

        private void CopyGroup()
        {
            if (SelectedGroup != null)
            {
                try
                {
                    _clipboardJson = JsonSerializer.Serialize(SelectedGroup, GetJsonOptions());
                    _clipboardIsGroup = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Group Copy failed: {ex.Message}");
                }
            }
        }

        private void PasteGroup()
        {
            if (string.IsNullOrEmpty(_clipboardJson) || !_clipboardIsGroup) return;

            try
            {
                var newGroup = JsonSerializer.Deserialize<SequenceGroup>(_clipboardJson, GetJsonOptions());
                if (newGroup != null)
                {
                    newGroup.Name += " (Copy)";

                    var idMap = new Dictionary<string, string>();

                    RemapGroupIdsRecursive(newGroup, idMap);

                    UpdateGroupJumpsRecursive(newGroup, idMap);

                    if (SelectedGroup != null)
                    {
                        SelectedGroup.Nodes.Add(newGroup);
                    }
                    else
                    {
                        Groups.Add(newGroup);
                    }

                    SelectedGroup = newGroup;
                    SelectedSequence = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Group Paste failed: {ex.Message}");
            }
        }

        private void RemapGroupIdsRecursive(SequenceGroup group, Dictionary<string, string> idMap)
        {
            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem item)
                {
                    var oldId = item.Id.ToString();
                    item.ResetId();
                    var newId = item.Id.ToString();
                    if (!idMap.ContainsKey(oldId)) idMap[oldId] = newId;
                }
                else if (node is SequenceGroup subGroup)
                {
                    RemapGroupIdsRecursive(subGroup, idMap);
                }
            }
        }

        private void UpdateGroupJumpsRecursive(SequenceGroup group, Dictionary<string, string> idMap)
        {
            if (!string.IsNullOrEmpty(group.ProcessNotFoundJumpId) && idMap.ContainsKey(group.ProcessNotFoundJumpId))
            {
                group.ProcessNotFoundJumpId = idMap[group.ProcessNotFoundJumpId];
            }
            if (!string.IsNullOrEmpty(group.StartJumpId) && idMap.ContainsKey(group.StartJumpId))
            {
                group.StartJumpId = idMap[group.StartJumpId];
            }

            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem item)
                {
                    if (!string.IsNullOrEmpty(item.SuccessJumpId) && idMap.ContainsKey(item.SuccessJumpId))
                    {
                        item.SuccessJumpId = idMap[item.SuccessJumpId];
                    }
                    UpdateComponentJumpId(item.PreCondition, idMap);
                    UpdateComponentJumpId(item.Action, idMap);
                    UpdateComponentJumpId(item.PostCondition, idMap);
                }
                else if (node is SequenceGroup subGroup)
                {
                    UpdateGroupJumpsRecursive(subGroup, idMap);
                }
            }
        }

        private void DuplicateGroup()
        {
            if (SelectedGroup != null)
            {
                CopyGroup();
                PasteGroup();
            }
        }

        private void UpdateComponentJumpId(object? component, Dictionary<string, string> idMap)
        {
            if (component == null) return;

            if (component is IMacroCondition cond)
            {
                if (!string.IsNullOrEmpty(cond.FailJumpId) && idMap.ContainsKey(cond.FailJumpId))
                {
                    cond.FailJumpId = idMap[cond.FailJumpId];
                }

                // [Fix] SwitchCaseCondition 내부의 JumpId들도 매핑 테이블에 따라 업데이트
                if (cond is SwitchCaseCondition switchCase)
                {
                    foreach (var caseItem in switchCase.Cases)
                    {
                        if (!string.IsNullOrEmpty(caseItem.JumpId) && idMap.ContainsKey(caseItem.JumpId))
                        {
                            caseItem.JumpId = idMap[caseItem.JumpId];
                        }
                    }
                }
            }
            else if (component is IMacroAction act)
            {
                if (!string.IsNullOrEmpty(act.FailJumpId) && idMap.ContainsKey(act.FailJumpId))
                {
                    act.FailJumpId = idMap[act.FailJumpId];
                }
            }
        }

        private void PasteSequence()
        {
            if (string.IsNullOrEmpty(_clipboardJson) || SelectedGroup == null || _clipboardIsGroup) return;

            try
            {
                var newItem = JsonSerializer.Deserialize<SequenceItem>(_clipboardJson, GetJsonOptions());
                if (newItem != null)
                {
                    newItem.ResetId();
                    newItem.Name += " (Copy)";

                    if (SelectedGroup != null)
                    {
                        ValidateAndClearJumpId(newItem, SelectedGroup);
                    }

                    if (SelectedSequence != null)
                    {
                        var parent = FindParentGroup(SelectedSequence);
                        if (parent == SelectedGroup)
                        {
                            int index = parent.Nodes.IndexOf(SelectedSequence);
                            if (index >= 0) parent.Nodes.Insert(index + 1, newItem);
                            else parent.Nodes.Add(newItem);
                        }
                        else
                        {
                            SelectedGroup.Nodes.Add(newItem);
                        }
                    }
                    else
                    {
                        SelectedGroup.Nodes.Add(newItem);
                    }

                    SelectedSequence = newItem;
                    UpdateJumpTargets();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Paste failed: {ex.Message}");
            }
        }

        private void ValidateAndClearJumpId(SequenceItem item, SequenceGroup currentGroup)
        {
            if (!string.IsNullOrEmpty(item.SuccessJumpId) && !IsIdInGroupRecursive(currentGroup, item.SuccessJumpId))
            {
                item.SuccessJumpId = string.Empty;
            }

            ValidateConditionJump(item.PreCondition, currentGroup);
            ValidateActionJump(item.Action, currentGroup);
            ValidateConditionJump(item.PostCondition, currentGroup);
        }

        private void ValidateConditionJump(IMacroCondition? condition, SequenceGroup group)
        {
            if (condition == null) return;
            if (!string.IsNullOrEmpty(condition.FailJumpId) && !IsIdInGroupRecursive(group, condition.FailJumpId))
            {
                condition.FailJumpId = string.Empty;
            }

            // [Fix] SwitchCaseCondition 내부의 JumpId들도 유효성 검사
            if (condition is SwitchCaseCondition switchCase)
            {
                foreach (var caseItem in switchCase.Cases)
                {
                    if (!string.IsNullOrEmpty(caseItem.JumpId) && !IsIdInGroupRecursive(group, caseItem.JumpId))
                    {
                        caseItem.JumpId = string.Empty;
                    }
                }
            }
        }

        private void ValidateActionJump(IMacroAction? action, SequenceGroup group)
        {
            if (action == null) return;
            if (!string.IsNullOrEmpty(action.FailJumpId) && !IsIdInGroupRecursive(group, action.FailJumpId))
            {
                action.FailJumpId = string.Empty;
            }
        }

        private bool IsIdInGroupRecursive(SequenceGroup group, string id)
        {
            foreach (var node in group.Nodes)
            {
                if (node.Id.ToString() == id) return true;
                if (node is SequenceGroup subGroup)
                {
                    if (IsIdInGroupRecursive(subGroup, id)) return true;
                }
            }
            return false;
        }

        private (int X, int Y, int Width, int Height)? GetTargetWindowInfo(SequenceGroup group)
        {
            if (group == null || string.IsNullOrEmpty(group.TargetProcessName)) return null;

            IntPtr hWnd = IntPtr.Zero;
            if (group.ContextSearchMethod == WindowControlSearchMethod.ProcessName)
            {
                var p = System.Diagnostics.Process.GetProcessesByName(group.TargetProcessName).FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero);
                if (p != null) hWnd = p.MainWindowHandle;
            }
            else
            {
                hWnd = InputHelper.FindWindowByTitle(group.TargetProcessName);
            }

            if (hWnd != IntPtr.Zero)
            {
                if (InputHelper.GetClientRect(hWnd, out var clientRect))
                {
                    InputHelper.POINT pt = new InputHelper.POINT { X = 0, Y = 0 };
                    InputHelper.ClientToScreen(hWnd, ref pt);

                    return (pt.X, pt.Y, clientRect.Width, clientRect.Height);
                }
            }
            return null;
        }

        private async Task TestImageConditionAsync(ImageMatchCondition condition)
        {
            if (condition == null) return;

            try
            {
                condition.TestResult = "Searching...";
                TestResultImage = null;

                var parentGroup = SelectedGroup ?? (SelectedSequence != null ? GetEffectiveGroupContext(SelectedSequence) : null);

                await Task.Run(() =>
                {
                    var captureSource = ScreenCaptureHelper.GetScreenCapture();
                    var bounds = ScreenCaptureHelper.GetScreenBounds();

                    if (captureSource == null)
                    {
                        condition.TestResult = "Capture Failed";
                        return;
                    }

                    double scaleX = 1.0;
                    double scaleY = 1.0;
                    double winX = 0;
                    double winY = 0;
                    (int X, int Y, int Width, int Height)? winInfo = null;

                    if (parentGroup != null && parentGroup.CoordinateMode == CoordinateMode.WindowRelative)
                    {
                        winInfo = GetTargetWindowInfo(parentGroup);
                        if (winInfo.HasValue)
                        {
                            if (parentGroup.RefWindowWidth > 0 && parentGroup.RefWindowHeight > 0)
                            {
                                scaleX = (double)winInfo.Value.Width / parentGroup.RefWindowWidth;
                                scaleY = (double)winInfo.Value.Height / parentGroup.RefWindowHeight;
                            }
                            winX = winInfo.Value.X;
                            winY = winInfo.Value.Y;
                        }
                    }

                    System.Windows.Rect? searchRoi = null;
                    OpenCvSharp.Rect? drawRoi = null;

                    if (condition.UseRegion && condition.RegionW > 0 && condition.RegionH > 0)
                    {
                        double rxAbs = condition.RegionX * scaleX + winX;
                        double ryAbs = condition.RegionY * scaleY + winY;

                        double rx = rxAbs - bounds.Left;
                        double ry = ryAbs - bounds.Top;

                        double rw = condition.RegionW * scaleX;
                        double rh = condition.RegionH * scaleY;

                        searchRoi = new System.Windows.Rect(rx, ry, rw, rh);
                        drawRoi = new OpenCvSharp.Rect((int)rx, (int)ry, (int)rw, (int)rh);
                    }
                    else if (winInfo.HasValue && parentGroup != null && parentGroup.CoordinateMode == CoordinateMode.WindowRelative)
                    {
                        double rx = winInfo.Value.X - bounds.Left;
                        double ry = winInfo.Value.Y - bounds.Top;
                        double rw = winInfo.Value.Width;
                        double rh = winInfo.Value.Height;

                        searchRoi = new System.Windows.Rect(rx, ry, rw, rh);
                        drawRoi = new OpenCvSharp.Rect((int)rx, (int)ry, (int)rw, (int)rh);
                    }

                    string targetPath = condition.ImagePath;
                    if (!string.IsNullOrEmpty(targetPath) && !Path.IsPathRooted(targetPath))
                    {
                        var currentRecipe = RecipeManager.Instance.CurrentRecipe;
                        if (currentRecipe != null && !string.IsNullOrEmpty(currentRecipe.FilePath))
                        {
                            var dir = Path.GetDirectoryName(currentRecipe.FilePath);
                            if (dir != null)
                            {
                                targetPath = Path.Combine(dir, targetPath);
                            }
                        }
                    }

                    var result = ImageSearchService.FindImageDetailed(captureSource, targetPath, condition.Threshold, searchRoi, scaleX, scaleY);

                    condition.TestScore = result.Score;
                    MacroEngineService.Instance.AddLog($"[Test Match] Score: {result.Score:F4} (Threshold: {condition.Threshold}), Scale: {scaleX:F2}x{scaleY:F2}, ROI: {searchRoi}");

                    using (var mat = BitmapSourceConverter.ToMat(captureSource))
                    {
                        if (drawRoi.HasValue)
                        {
                            Cv2.Rectangle(mat, drawRoi.Value, Scalar.Blue, 2);
                            Cv2.PutText(mat, "ROI", new OpenCvSharp.Point(drawRoi.Value.X, drawRoi.Value.Y - 10),
                                HersheyFonts.HersheySimplex, 0.5, Scalar.Blue, 1);
                        }

                        if (result.Point.HasValue)
                        {
                            double foundAbsX = result.Point.Value.X + bounds.Left;
                            double foundAbsY = result.Point.Value.Y + bounds.Top;
                            condition.TestResult = $"Found({foundAbsX:F0},{foundAbsY:F0}) Bounds[{bounds.Left},{bounds.Top}]";

                            int tW = 50, tH = 50;
                            try
                            {
                                using (var tempMat = Cv2.ImRead(targetPath))
                                {
                                    if (!tempMat.Empty())
                                    {
                                        tW = tempMat.Width;
                                        tH = tempMat.Height;
                                    }
                                }
                            }
                            catch { }

                            int scaledW = (int)(tW * scaleX);
                            int scaledH = (int)(tH * scaleY);

                            int matchX = (int)(result.Point.Value.X - scaledW / 2);
                            int matchY = (int)(result.Point.Value.Y - scaledH / 2);
                            var matchRect = new OpenCvSharp.Rect(matchX, matchY, scaledW, scaledH);

                            Cv2.Rectangle(mat, matchRect, Scalar.Red, 3);
                            Cv2.PutText(mat, $"Found {result.Score:P0}", new OpenCvSharp.Point(matchX, matchY - 10),
                                HersheyFonts.HersheySimplex, 0.5, Scalar.Red, 1);

                            int cx = (int)result.Point.Value.X;
                            int cy = (int)result.Point.Value.Y;
                            Cv2.Line(mat, cx - 10, cy, cx + 10, cy, Scalar.Red, 2);
                            Cv2.Line(mat, cx, cy - 10, cx, cy + 10, Scalar.Red, 2);
                        }
                        else
                        {
                            condition.TestResult = "Failed (Low Score)";
                        }

                        var resultSource = BitmapSourceConverter.ToBitmapSource(mat);
                        resultSource.Freeze();

                        RxApp.MainThreadScheduler.Schedule(() =>
                        {
                            TestResultImage = resultSource;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                condition.TestResult = "Error";
                MacroEngineService.Instance.AddLog($"[Test] 이미지 매칭 오류: {ex.Message}");
            }
        }

        #endregion
    }
}
