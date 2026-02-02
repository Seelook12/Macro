using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Macro.Models;
using Macro.Utils;
using ReactiveUI;

namespace Macro.ViewModels
{
    public partial class TeachingViewModel
    {
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
                    DebugLogger.Log($"[Clipboard] CopySequence Failed: {ex.Message}");
                }
            }
        }

        private void PasteSequence()
        {
            if (string.IsNullOrEmpty(_clipboardJson) || _clipboardIsGroup || SelectedGroup == null) return;

            try
            {
                var newItem = JsonSerializer.Deserialize<SequenceItem>(_clipboardJson, GetJsonOptions());
                if (newItem != null)
                {
                    newItem.ResetId();
                    newItem.Name += " (Copy)";

                    int index = -1;
                    if (SelectedSequence != null)
                    {
                        index = SelectedGroup.Nodes.IndexOf(SelectedSequence);
                    }

                    if (index != -1 && index < SelectedGroup.Nodes.Count - 1)
                    {
                        SelectedGroup.Nodes.Insert(index + 1, newItem);
                    }
                    else
                    {
                        SelectedGroup.Nodes.Add(newItem);
                    }

                    SelectedSequence = newItem;
                    UpdateJumpTargets();
                    UpdateGroupJumpTargets();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Clipboard] PasteSequence Failed: {ex.Message}");
            }
        }

        private void CopyGroup()
        {
            if (SelectedGroup != null && !SelectedGroup.IsStartGroup)
            {
                try
                {
                    _clipboardJson = JsonSerializer.Serialize(SelectedGroup, GetJsonOptions());
                    _clipboardIsGroup = true;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[Clipboard] CopyGroup Failed: {ex.Message}");
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
                    // Sanitize IDs to avoid conflicts
                    var seenIds = new HashSet<string>();
                    // Collect existing IDs
                    SanitizeLoadedData(); // Helper to refresh seenIds? No, SanitizeLoadedData sanitizes everything.
                                          // We just need to ensure newGroup has unique IDs.
                    
                    // Actually, SanitizeGroupIdsRecursive is what we want, but we need to ensure they don't conflict with EXISTING groups.
                    // The easiest way is to just Reset all IDs in the new group.
                    
                    // Force reset all IDs in the new group tree
                    ResetGroupIdsRecursive(newGroup);

                    newGroup.Name += " (Copy)";

                    if (SelectedGroup != null)
                    {
                        // Insert after SelectedGroup
                        // We need to find the parent collection
                        var parent = FindParentGroup(SelectedGroup);
                        if (parent != null)
                        {
                            int index = parent.Nodes.IndexOf(SelectedGroup);
                            parent.Nodes.Insert(index + 1, newGroup);
                        }
                        else
                        {
                            // Root level
                            int index = Groups.IndexOf(SelectedGroup);
                            if (index != -1)
                            {
                                Groups.Insert(index + 1, newGroup);
                            }
                            else
                            {
                                Groups.Add(newGroup);
                            }
                        }
                    }
                    else
                    {
                        Groups.Add(newGroup);
                    }

                    SelectedGroup = newGroup;
                    UpdateJumpTargets();
                    UpdateGroupJumpTargets();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Clipboard] PasteGroup Failed: {ex.Message}");
            }
        }

        private void DuplicateGroup()
        {
            if (SelectedGroup != null && !SelectedGroup.IsStartGroup)
            {
                CopyGroup();
                PasteGroup();
            }
        }

        private void ResetGroupIdsRecursive(SequenceGroup group)
        {
            // Reset Group ID? SequenceGroup.Id is read-only usually or needs setter?
            // SequenceGroup.Id has a getter that returns Guid.NewGuid() initialized field.
            // We can't easily change SequenceGroup.Id if it's readonly.
            // But looking at SequenceGroup definition in MacroModels.cs:
            // public Guid Id { get; } = Guid.NewGuid();
            // It is read-only. But deserialization sets it? 
            // JSON deserializer might set backing field or property if it has private setter.
            // Wait, SequenceGroup.Id doesn't have a setter. 
            // So Deserializer might be skipping it, effectively creating a NEW Id. 
            // Let's verify.
            
            // However, SequenceItems definitely need ResetId().
            var idMap = new Dictionary<string, string>();
            
            // 1. Collect and Map Old IDs to New IDs
            CollectAndResetIds(group, idMap);
            
            // 2. Update Jump Targets
            UpdateJumpReferencesRecursive(group, idMap);
        }

        private void CollectAndResetIds(SequenceGroup group, Dictionary<string, string> idMap)
        {
            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem item)
                {
                    string oldId = item.Id.ToString();
                    item.ResetId();
                    string newId = item.Id.ToString();
                    idMap[oldId] = newId;
                }
                else if (node is SequenceGroup subGroup)
                {
                    CollectAndResetIds(subGroup, idMap);
                }
            }
        }

        private void UpdateJumpReferencesRecursive(SequenceGroup group, Dictionary<string, string> idMap)
        {
            foreach (var node in group.Nodes)
            {
                if (node is SequenceItem item)
                {
                    if (!string.IsNullOrEmpty(item.SuccessJumpId) && idMap.ContainsKey(item.SuccessJumpId))
                        item.SuccessJumpId = idMap[item.SuccessJumpId];
                    
                    if (!string.IsNullOrEmpty(item.Action?.FailJumpId) && idMap.ContainsKey(item.Action.FailJumpId))
                        item.Action.FailJumpId = idMap[item.Action.FailJumpId];

                    UpdateConditionReferences(item.PreCondition, idMap);
                    UpdateConditionReferences(item.PostCondition, idMap);
                }
                else if (node is SequenceGroup subGroup)
                {
                    UpdateJumpReferencesRecursive(subGroup, idMap);
                }
            }
            
            // Group PostCondition
            if (group.PostCondition != null)
            {
                UpdateConditionReferences(group.PostCondition, idMap);
            }
        }

        private void UpdateConditionReferences(IMacroCondition? condition, Dictionary<string, string> idMap)
        {
            if (condition == null) return;
            
            if (!string.IsNullOrEmpty(condition.FailJumpId) && idMap.ContainsKey(condition.FailJumpId))
                condition.FailJumpId = idMap[condition.FailJumpId];
                
            if (condition is SwitchCaseCondition sc)
            {
                foreach (var c in sc.Cases)
                {
                    if (!string.IsNullOrEmpty(c.JumpId) && idMap.ContainsKey(c.JumpId))
                        c.JumpId = idMap[c.JumpId];
                }
            }
        }
    }
}
