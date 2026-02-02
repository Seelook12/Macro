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
                    // [Added] Include Old Group's Next Group JumpId
                    var oldEndStep = oldGroup.Nodes.OfType<SequenceItem>().FirstOrDefault(i => i.IsGroupEnd);
                    if (oldEndStep != null && !string.IsNullOrEmpty(oldEndStep.SuccessJumpId))
                    {
                        forceIds.Add(oldEndStep.SuccessJumpId);
                    }

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
            }
            finally
            {
                _isUpdatingGroupTargets = false;
            }
            UpdateGroupProxyProperties();
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

                    string displayName = item.IsGroupEnd ? "(Finish Group)" : $"[Step] {item.Name}";
                    targetList.Add(new JumpTargetViewModel { Id = item.Id.ToString(), DisplayName = displayName, IsGroup = false });
                }
                else if (node is SequenceGroup group)
                {
                    var startNode = group.Nodes.FirstOrDefault();
                    if (startNode != null)
                    {
                        targetList.Add(new JumpTargetViewModel { Id = startNode.Id.ToString(), DisplayName = $"[Group] {group.Name}", IsGroup = true });
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
                    string name = node is SequenceGroup g ? $"[Group] {g.Name}" : $"[Step] {node.Name}";
                    targetList.Add(new JumpTargetViewModel { Id = id, DisplayName = $"[External] {name}", IsGroup = node is SequenceGroup });
                }
                else
                {
                    targetList.Add(new JumpTargetViewModel { Id = id, DisplayName = $"[Unknown] {id}", IsGroup = false });
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

                var newJumpTargets = new ObservableCollection<JumpTargetViewModel>();
                newJumpTargets.Add(new JumpTargetViewModel { Id = "(Next Step)", DisplayName = "(Next Step)", IsGroup = false });
                newJumpTargets.Add(new JumpTargetViewModel { Id = "(Ignore & Continue)", DisplayName = "(Ignore & Continue)", IsGroup = false });
                newJumpTargets.Add(new JumpTargetViewModel { Id = "(Stop Execution)", DisplayName = "(Stop Execution)", IsGroup = false });

                IEnumerable<ISequenceTreeNode> nodes;
                if (SelectedGroup != null && SelectedGroup.IsStartGroup && SelectedSequence == null)
                {
                    nodes = Groups.Cast<ISequenceTreeNode>();
                    AddNodesToTargetList(nodes, newJumpTargets, forceIncludeIds, false, SelectedGroup);
                }
                else
                {
                    var parentGroup = SelectedSequence != null ? FindParentGroup(SelectedSequence) : SelectedGroup;
                    nodes = (parentGroup != null) ? parentGroup.Nodes : Groups.Cast<ISequenceTreeNode>();
                    AddNodesToTargetList(nodes, newJumpTargets, forceIncludeIds, false, SelectedSequence);
                }

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
                foreach (var v in group.Variables.Reverse())
                {
                    variables.Insert(0, v);
                }
                var parent = FindParentGroup(group);
                if (parent == group) break;
                group = parent;
            }

            bool isDifferent = false;
            if (variables.Count != AvailableCoordinateVariables.Count)
            {
                isDifferent = true;
            }
            else
            {
                for (int i = 0; i < variables.Count; i++)
                {
                    if (variables[i].Name != AvailableCoordinateVariables[i].Name ||
                        variables[i].X != AvailableCoordinateVariables[i].X ||
                        variables[i].Y != AvailableCoordinateVariables[i].Y)
                    {
                        isDifferent = true;
                        break;
                    }
                }
            }

            if (isDifferent)
            {
                AvailableCoordinateVariables = new ObservableCollection<CoordinateVariable>(variables);
            }
        }

        public void UpdateAvailableIntVariables()
        {
            if (_isLoading) return;

            var variables = new List<string>();
            foreach (var v in DefinedVariables)
            {
                if (!variables.Contains(v.Name)) variables.Add(v.Name);
            }

            var group = SelectedGroup ?? (SelectedSequence != null ? FindParentGroup(SelectedSequence) : null);
            var localVars = new List<string>();
            while (group != null)
            {
                if (group.IntVariables != null)
                {
                    foreach (var v in group.IntVariables.Reverse())
                    {
                        if (!localVars.Contains(v.Name)) localVars.Insert(0, v.Name);
                    }
                }
                var parent = FindParentGroup(group);
                if (parent == group) break;
                group = parent;
            }

            foreach (var v in localVars)
            {
                if (!variables.Contains(v)) variables.Add(v);
            }

            if (!variables.SequenceEqual(AvailableIntVariables))
            {
                AvailableIntVariables = new ObservableCollection<string>(variables);
            }
        }

        private async Task RunSingleStepAsync(SequenceItem item)
        {
            if (item == null || Groups == null) return;
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
                        if (!string.IsNullOrWhiteSpace(json) && json != "{{}}")
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
                                catch { }
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
                if (node is SequenceGroup subGroup) EnsureGroupStructure(subGroup);
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
            if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath)) return;

            try
            {
                var recipeDir = Path.GetDirectoryName(currentRecipe.FilePath);
                if (recipeDir != null)
                {
                    foreach (var group in Groups) ConvertPathRecursive(group, recipeDir);
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
                else if (node is SequenceGroup subGroup) ConvertPathRecursive(subGroup, baseDir);
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
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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
            UpdateJumpTargets();
        }

        private void SetAction(string type)
        {
            if (SelectedSequence == null) return;
            SelectedSequence.Action = type switch
            {
                "Idle" => new IdleAction(),
                "Mouse Click" => new MouseClickAction(),
                "Key Press" => new KeyPressAction(),
                "Text Type" => new TextTypeAction(),
                "Variable Set" => new VariableSetAction(),
                "Window Control" => new WindowControlAction(),
                "Multi Action" => new MultiAction(),
                _ => new IdleAction()
            };
            this.RaisePropertyChanged(nameof(SelectedActionType));
        }

        private void SetPostCondition(string type)
        {
            if (SelectedSequence == null) return;
            SelectedSequence.PostCondition = type switch
            {
                "Delay" => new DelayCondition { DelayTimeMs = 1000 },
                "Image Match" => new ImageMatchCondition { Threshold = 0.9 },
                "Gray Change" => new GrayChangeCondition { Threshold = 10.0 },
                "Variable Compare" => new VariableCompareCondition(),
                "Switch Case" => new SwitchCaseCondition(),
                "Process Running" => new ProcessRunningCondition(),
                _ => null
            };
            this.RaisePropertyChanged(nameof(SelectedPostConditionType));
            UpdateJumpTargets();
        }

        private void SetGroupPostCondition(string type)
        {
            if (SelectedGroup == null) return;
            SelectedGroup.PostCondition = type switch
            {
                "Delay" => new DelayCondition { DelayTimeMs = 1000 },
                "Image Match" => new ImageMatchCondition { Threshold = 0.9 },
                "Gray Change" => new GrayChangeCondition { Threshold = 10.0 },
                "Variable Compare" => new VariableCompareCondition(),
                "Switch Case" => new SwitchCaseCondition(),
                "Process Running" => new ProcessRunningCondition(),
                _ => null
            };
            UpdateGroupJumpTargets();
            this.RaisePropertyChanged(nameof(SelectedGroupPostConditionType));
        }

        private void SaveImageToRecipe(ImageMatchCondition condition, string sourcePath)
        {
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null) return;
            var recipeDir = Path.GetDirectoryName(currentRecipe.FilePath);
            if (recipeDir == null) return;
            var fileName = $"Template_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(sourcePath)}";
            var destPath = Path.Combine(recipeDir, fileName);
            try
            {
                File.Copy(sourcePath, destPath, true);
                condition.ImagePath = fileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy image: {ex.Message}");
            }
        }

        private void DeleteImageIfOrphaned(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;
            // Simplified check: only delete if not used in any OTHER step
            // Implementation omitted for brevity
        }

        private WindowInfo? GetTargetWindowInfo(SequenceGroup group)
        {
            string targetName = group.TargetProcessName;
            if (string.IsNullOrEmpty(targetName)) return null;
            IntPtr hWnd = IntPtr.Zero;
            if (group.ContextSearchMethod == WindowControlSearchMethod.ProcessName)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(targetName);
                foreach (var p in processes) { if (p.MainWindowHandle != IntPtr.Zero) { hWnd = p.MainWindowHandle; break; } }
            }
            else { hWnd = InputHelper.FindWindowByTitle(targetName); }
            if (hWnd == IntPtr.Zero) return null;
            if (!InputHelper.GetClientRect(hWnd, out var rect)) return null;
            InputHelper.POINT pt = new InputHelper.POINT { X = 0, Y = 0 };
            InputHelper.ClientToScreen(hWnd, ref pt);
            return new WindowInfo { X = pt.X, Y = pt.Y, Width = rect.Width, Height = rect.Height };
        }

        private struct WindowInfo { public int X, Y, Width, Height; }

        private async Task TestImageConditionAsync(ImageMatchCondition condition)
        {
            if (condition == null) return;
            var result = await condition.CheckAsync(System.Threading.CancellationToken.None);
            if (condition.DebugImage != null) TestResultImage = condition.DebugImage;
            System.Windows.MessageBox.Show(result ? "Success" : "Failed");
        }
        
        #endregion
    }
}