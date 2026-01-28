using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using Macro.Models;
using Macro.Services;
using Macro.Utils;
using ReactiveUI;

namespace Macro.ViewModels
{
    public partial class TeachingViewModel
    {
        #region Commands

        public ReactiveCommand<Unit, Unit> AddGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> RemoveGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> AddSequenceCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> RemoveSequenceCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> SaveCommand { get; private set; } = null!;
        public ReactiveCommand<SequenceItem, Unit> RunSingleStepCommand { get; private set; } = null!;
        
        // Interaction
        public Interaction<Unit, System.Windows.Point?> GetCoordinateInteraction { get; } = new Interaction<Unit, System.Windows.Point?>();
        public ReactiveCommand<Unit, Unit> PickCoordinateCommand { get; private set; } = null!;
        public ReactiveCommand<CoordinateVariable, Unit> PickVariableCoordinateCommand { get; private set; } = null!;
        public Interaction<Unit, System.Windows.Rect?> GetRegionInteraction { get; } = new Interaction<Unit, System.Windows.Rect?>();
        public Interaction<Unit, string?> CaptureImageInteraction { get; } = new Interaction<Unit, string?>();

        public ReactiveCommand<ImageMatchCondition, Unit> SelectImageCommand { get; private set; } = null!;
        public ReactiveCommand<ImageMatchCondition, Unit> CaptureImageCommand { get; private set; } = null!;
        public ReactiveCommand<ImageMatchCondition, Unit> UpdateRefSizeCommand { get; private set; } = null!;
        public ReactiveCommand<ImageMatchCondition, Unit> TestImageConditionCommand { get; private set; } = null!;
        public ReactiveCommand<object, Unit> PickRegionCommand { get; private set; } = null!;
        public ReactiveCommand<WindowControlAction, Unit> RefreshTargetListCommand { get; private set; } = null!;
        public ReactiveCommand<object, Unit> RefreshContextTargetCommand { get; private set; } = null!; 

        public ReactiveCommand<SequenceItem, Unit> MoveSequenceUpCommand { get; private set; } = null!;
        public ReactiveCommand<SequenceItem, Unit> MoveSequenceDownCommand { get; private set; } = null!;
        public ReactiveCommand<SequenceGroup, Unit> MoveGroupUpCommand { get; private set; } = null!;
        public ReactiveCommand<SequenceGroup, Unit> MoveGroupDownCommand { get; private set; } = null!;
        
        public ReactiveCommand<Unit, Unit> CopySequenceCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> PasteSequenceCommand { get; private set; } = null!;
        
        public ReactiveCommand<Unit, Unit> CopyGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> PasteGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> DuplicateGroupCommand { get; private set; } = null!;
        
        public ReactiveCommand<SwitchCaseCondition, Unit> AddSwitchCaseCommand { get; private set; } = null!;
        public ReactiveCommand<SwitchCaseItem, Unit> RemoveSwitchCaseCommand { get; private set; } = null!;

        public ReactiveCommand<Unit, Unit> OpenVariableManagerCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> CloseVariableManagerCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> AddVariableDefinitionCommand { get; private set; } = null!;
        public ReactiveCommand<VariableDefinition, Unit> RemoveVariableDefinitionCommand { get; private set; } = null!;
        
        // Coordinate Variable Commands
        public ReactiveCommand<Unit, Unit> AddCoordinateVariableCommand { get; private set; } = null!;
        public ReactiveCommand<CoordinateVariable, Unit> RemoveCoordinateVariableCommand { get; private set; } = null!;

        // Group Integer Variable Commands
        public ReactiveCommand<Unit, Unit> AddIntVariableCommand { get; private set; } = null!;
        public ReactiveCommand<GroupIntVariable, Unit> RemoveIntVariableCommand { get; private set; } = null!;

        public ReactiveCommand<MultiAction, Unit> AddSubActionCommand { get; private set; } = null!;
        public ReactiveCommand<IMacroAction, Unit> RemoveSubActionCommand { get; private set; } = null!;

        #endregion

        private void InitializeCommands()
        {
             // Initialize Commands
            AddGroupCommand = ReactiveCommand.Create(AddGroup);
            
            var canRemoveGroup = this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null && !g.IsStartGroup);
            RemoveGroupCommand = ReactiveCommand.Create(RemoveGroup, canRemoveGroup);
            
            var canAddSequence = this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null && !g.IsStartGroup);
            AddSequenceCommand = ReactiveCommand.Create(AddSequence, canAddSequence);

            RemoveSequenceCommand = ReactiveCommand.Create(RemoveSequence, this.WhenAnyValue(x => x.SelectedSequence, (SequenceItem? item) => item != null));
            
            SaveCommand = ReactiveCommand.CreateFromTask(SaveSequencesAsync);
            RunSingleStepCommand = ReactiveCommand.CreateFromTask<SequenceItem>(RunSingleStepAsync);
            
            MoveGroupUpCommand = ReactiveCommand.Create<SequenceGroup>(MoveGroupUp);
            MoveGroupDownCommand = ReactiveCommand.Create<SequenceGroup>(MoveGroupDown);
            MoveSequenceUpCommand = ReactiveCommand.Create<SequenceItem>(MoveSequenceUp);
            MoveSequenceDownCommand = ReactiveCommand.Create<SequenceItem>(MoveSequenceDown);
            
            CopySequenceCommand = ReactiveCommand.Create(CopySequence, this.WhenAnyValue(x => x.SelectedSequence, (SequenceItem? item) => item != null));
            PasteSequenceCommand = ReactiveCommand.Create(PasteSequence, this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null));

            CopyGroupCommand = ReactiveCommand.Create(CopyGroup, this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null && !g.IsStartGroup));

            PasteGroupCommand = ReactiveCommand.Create(PasteGroup);

            DuplicateGroupCommand = ReactiveCommand.Create(DuplicateGroup, this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null && !g.IsStartGroup));

            AddSwitchCaseCommand = ReactiveCommand.Create<SwitchCaseCondition>(cond => 
            {
                if (cond == null) return;
                cond.Cases.Add(new SwitchCaseItem { CaseValue = 0, JumpId = "" });
                
                // [Fix] If this is Group PostCondition, sync the proxy list
                if (SelectedGroup?.PostCondition == cond) SyncGroupPostConditionCases();
                
                // [Fix] If this is Step Pre/Post Condition, sync the proxy list
                if (SelectedSequence?.PreCondition == cond || SelectedSequence?.PostCondition == cond) SyncStepSwitchCases();
            });

            RemoveSwitchCaseCommand = ReactiveCommand.Create<SwitchCaseItem>(item => 
            {
                if (SelectedSequence?.PreCondition is SwitchCaseCondition pre && pre.Cases.Contains(item))
                {
                    pre.Cases.Remove(item);
                    SyncStepSwitchCases();
                }
                else if (SelectedSequence?.PostCondition is SwitchCaseCondition post && post.Cases.Contains(item))
                {
                    post.Cases.Remove(item);
                    SyncStepSwitchCases();
                }
                else if (SelectedGroup?.PostCondition is SwitchCaseCondition groupPost && groupPost.Cases.Contains(item))
                {
                    groupPost.Cases.Remove(item);
                    SyncGroupPostConditionCases();
                }
            });

            OpenVariableManagerCommand = ReactiveCommand.Create(() => { IsVariableManagerOpen = true; });
            CloseVariableManagerCommand = ReactiveCommand.Create(() => { IsVariableManagerOpen = false; }); 
            
            AddVariableDefinitionCommand = ReactiveCommand.Create(() => 
            {
                DefinedVariables.Add(new VariableDefinition { Name = "NewVar", DefaultValue = "0", Description = "Description" });
            });

            RemoveVariableDefinitionCommand = ReactiveCommand.Create<VariableDefinition>(v => 
            {
                DefinedVariables.Remove(v);
            });

            AddCoordinateVariableCommand = ReactiveCommand.Create(() => 
            {
                if (SelectedGroup != null)
                {
                    SelectedGroup.Variables.Add(new CoordinateVariable 
                    { 
                        Name = $"Point_{SelectedGroup.Variables.Count + 1}", 
                        X = 0, 
                        Y = 0, 
                        Description = "New Coordinate" 
                    });
                    UpdateAvailableCoordinateVariables();
                }
            }, this.WhenAnyValue(x => x.SelectedGroup).Select(g => g != null));

            RemoveCoordinateVariableCommand = ReactiveCommand.Create<CoordinateVariable>(v => 
            {
                if (SelectedGroup != null && SelectedGroup.Variables.Contains(v))
                {
                    SelectedGroup.Variables.Remove(v);
                    UpdateAvailableCoordinateVariables();
                }
            });

            AddIntVariableCommand = ReactiveCommand.Create(() => 
            {
                if (SelectedGroup != null)
                {
                    if (SelectedGroup.IntVariables == null) SelectedGroup.IntVariables = new System.Collections.ObjectModel.ObservableCollection<GroupIntVariable>();
                    
                    SelectedGroup.IntVariables.Add(new GroupIntVariable 
                    { 
                        Name = $"IntVar_{SelectedGroup.IntVariables.Count + 1}", 
                        Value = 0, 
                        Description = "New Integer Variable" 
                    });
                    UpdateAvailableIntVariables();
                }
            }, this.WhenAnyValue(x => x.SelectedGroup).Select(g => g != null));

            RemoveIntVariableCommand = ReactiveCommand.Create<GroupIntVariable>(v => 
            {
                if (SelectedGroup != null && SelectedGroup.IntVariables != null && SelectedGroup.IntVariables.Contains(v))
                {
                    SelectedGroup.IntVariables.Remove(v);
                    UpdateAvailableIntVariables();
                }
            });

            AddSubActionCommand = ReactiveCommand.Create<MultiAction>(parent => 
            {
                if (parent == null) return;
                
                IMacroAction newAction = SelectedSubActionType switch
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
                
                if (newAction is WindowControlAction winAct)
                {
                    RefreshTargetListCommand.Execute(winAct).Subscribe();
                }

                parent.Actions.Add(newAction);
            });

            RemoveSubActionCommand = ReactiveCommand.Create<IMacroAction>(child => 
            {
                if (SelectedSequence?.Action is MultiAction parent)
                {
                    if (parent.Actions.Contains(child))
                    {
                        parent.Actions.Remove(child);
                    }
                }
            });

            RefreshTargetListCommand = ReactiveCommand.CreateFromTask<WindowControlAction>(RefreshTargetListAsync);
            RefreshContextTargetCommand = ReactiveCommand.CreateFromTask<object>(RefreshContextTargetAsync);

            PickCoordinateCommand = ReactiveCommand.CreateFromTask(PickCoordinateAsync, this.WhenAnyValue(x => x.SelectedSequence, x => x.SelectedSequence!.Action, 
                (item, action) => item != null && action is MouseClickAction));

            PickVariableCoordinateCommand = ReactiveCommand.CreateFromTask<CoordinateVariable>(async (variable) => 
            {
                if (variable == null) return;

                var point = await GetCoordinateInteraction.Handle(Unit.Default);
                if (point.HasValue)
                {
                    var p = point.Value;
                    
                    // [Fix] Resolve effective context for ParentRelative groups
                    var contextGroup = ResolveGroupContext(SelectedGroup);

                    if (contextGroup != null && contextGroup.CoordinateMode == CoordinateMode.WindowRelative)
                    {
                        var winInfo = GetTargetWindowInfo(contextGroup);
                        if (winInfo.HasValue)
                        {
                            contextGroup.RefWindowWidth = winInfo.Value.Width;
                            contextGroup.RefWindowHeight = winInfo.Value.Height;

                            variable.X = (int)(p.X - winInfo.Value.X);
                            variable.Y = (int)(p.Y - winInfo.Value.Y);
                            return;
                        }
                    }
                    
                    variable.X = (int)p.X;
                    variable.Y = (int)p.Y;
                }
            });

            SelectImageCommand = ReactiveCommand.Create<ImageMatchCondition>(SelectImage);
            CaptureImageCommand = ReactiveCommand.CreateFromTask<ImageMatchCondition>(CaptureImageAsync);
            UpdateRefSizeCommand = ReactiveCommand.Create<ImageMatchCondition>(UpdateCurrentGroupReferenceSize);
            TestImageConditionCommand = ReactiveCommand.CreateFromTask<ImageMatchCondition>(TestImageConditionAsync);
            PickRegionCommand = ReactiveCommand.Create<object>(PickRegion);
        }

        #region Command Handlers

        private void AddGroup()
        {
            var newGroup = new SequenceGroup { Name = $"Group {Guid.NewGuid().ToString().Substring(0, 4)}" };
            
            var startStep = new SequenceItem(new IdleAction())
            {
                Name = "Start",
                IsGroupStart = true,
                IsEnabled = true
            };
            newGroup.Nodes.Add(startStep);

            var endStep = new SequenceItem(new IdleAction())
            {
                Name = "End",
                IsGroupEnd = true,
                IsEnabled = true
            };
            newGroup.Nodes.Add(endStep);

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

        private void RemoveGroup()
        {
            if (SelectedGroup != null)
            {
                if (Groups.Contains(SelectedGroup))
                {
                    Groups.Remove(SelectedGroup);
                }
                else
                {
                    var parent = FindParentGroup(SelectedGroup);
                    if (parent != null)
                    {
                        parent.Nodes.Remove(SelectedGroup);
                    }
                }
                SelectedGroup = null;
            }
        }

        private void AddSequence()
        {
            if (SelectedGroup == null) return;

            var newAction = new IdleAction();
            var newItem = new SequenceItem(newAction)
            {
                Name = $"Step {SelectedGroup.Nodes.OfType<SequenceItem>().Count() + 1}",
                IsEnabled = true
            };

            SelectedGroup.Nodes.Add(newItem);
            SelectedSequence = newItem;
            UpdateJumpTargets(); 
            UpdateGroupJumpTargets();
        }

        private void RemoveSequence()
        {
            if (SelectedSequence != null)
            {
                var parentGroup = FindParentGroup(SelectedSequence);
                if (parentGroup != null)
                {
                    var itemToRemove = SelectedSequence;
                    parentGroup.Nodes.Remove(itemToRemove);
                    
                    if (itemToRemove.PreCondition is ImageMatchCondition preImg) 
                        DeleteImageIfOrphaned(preImg.ImagePath);
                    if (itemToRemove.PostCondition is ImageMatchCondition postImg)
                        DeleteImageIfOrphaned(postImg.ImagePath);

                    SelectedSequence = null;
                    UpdateJumpTargets(); 
                    UpdateGroupJumpTargets();
                }
            }
        }

        private void MoveGroupUp(SequenceGroup group)
        {
            if (group.IsStartGroup) return;

            var parent = FindParentGroup(group);
            if (parent != null)
            {
                int index = parent.Nodes.IndexOf(group);
                if (index > 0) parent.Nodes.Move(index, index - 1);
            }
            else
            {
                int index = Groups.IndexOf(group);
                if (index > 1) Groups.Move(index, index - 1);
            }
        }

        private void MoveGroupDown(SequenceGroup group)
        {
            if (group.IsStartGroup) return;

            var parent = FindParentGroup(group);
            if (parent != null)
            {
                int index = parent.Nodes.IndexOf(group);
                if (index >= 0 && index < parent.Nodes.Count - 1) parent.Nodes.Move(index, index + 1);
            }
            else
            {
                int index = Groups.IndexOf(group);
                if (index >= 0 && index < Groups.Count - 1) Groups.Move(index, index + 1);
            }
        }

        private void MoveSequenceUp(SequenceItem item)
        {
            var parentGroup = FindParentGroup(item);
            if (parentGroup != null)
            {
                int index = parentGroup.Nodes.IndexOf(item);
                if (index > 0)
                {
                    parentGroup.Nodes.Move(index, index - 1);
                    UpdateJumpTargets(); 
                }
            }
        }

        private void MoveSequenceDown(SequenceItem item)
        {
            var parentGroup = FindParentGroup(item);
            if (parentGroup != null)
            {
                int index = parentGroup.Nodes.IndexOf(item);
                if (index < parentGroup.Nodes.Count - 1)
                {
                    parentGroup.Nodes.Move(index, index + 1);
                    UpdateJumpTargets();
                }
            }
        }

        private async Task RefreshTargetListAsync(WindowControlAction action)
        {
            if (action == null) return;

            await Task.Run(() =>
            {
                System.Collections.Generic.List<string> items;

                if (action.SearchMethod == WindowControlSearchMethod.ProcessName)
                {
                    var processes = System.Diagnostics.Process.GetProcesses();
                    items = processes.Select(p => p.ProcessName).Distinct().OrderBy(n => n).ToList();
                }
                else
                {
                    items = InputHelper.GetOpenWindows().Distinct().OrderBy(n => n).ToList();
                }
                
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    TargetList.Clear();
                    foreach (var name in items)
                    {
                        TargetList.Add(name);
                    }
                });
            });
        }

        private async Task RefreshContextTargetAsync(object parameter)
        {
            if (parameter == null) return;
            
            WindowControlSearchMethod searchMethod = WindowControlSearchMethod.ProcessName;

            if (parameter is SequenceGroup group)
            {
                searchMethod = group.ContextSearchMethod;
            }
            else if (parameter is ProcessRunningCondition condition)
            {
                searchMethod = condition.SearchMethod;
            }

            await Task.Run(() =>
            {
                System.Collections.Generic.List<string> items;
                if (searchMethod == WindowControlSearchMethod.ProcessName)
                {
                    var processes = System.Diagnostics.Process.GetProcesses();
                    items = processes.Select(p => p.ProcessName).Distinct().OrderBy(n => n).ToList();
                }
                else
                {
                    items = InputHelper.GetOpenWindows().Distinct().OrderBy(n => n).ToList();
                }

                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    ProcessList.Clear();
                    foreach (var name in items)
                    {
                        ProcessList.Add(name);
                    }
                });
            });
        }

        private async Task PickCoordinateAsync()
        {
            if (SelectedSequence?.Action is MouseClickAction mouseAction)
            {
                var point = await GetCoordinateInteraction.Handle(Unit.Default);
                if (point.HasValue)
                {
                    var p = point.Value;
                    
                    var parentGroup = GetEffectiveGroupContext(SelectedSequence);

                    if (parentGroup != null && parentGroup.CoordinateMode == CoordinateMode.WindowRelative)
                    {
                        var winInfo = GetTargetWindowInfo(parentGroup);
                        if (winInfo.HasValue)
                        {
                            parentGroup.RefWindowWidth = winInfo.Value.Width;
                            parentGroup.RefWindowHeight = winInfo.Value.Height;

                            mouseAction.X = (int)(p.X - winInfo.Value.X);
                            mouseAction.Y = (int)(p.Y - winInfo.Value.Y);
                            return;
                        }
                    }

                    mouseAction.X = (int)p.X;
                    mouseAction.Y = (int)p.Y;
                }
            }
        }

        private void PickRegion(object condition)
        {
             // RegionPicker interaction logic is handled in View via CommandBinding usually, 
             // but here we use Interaction pattern.
             // We need to implement the picking logic similar to PickCoordinateAsync
             // Wait, the original code used CommandParameter and a generic interaction.
             // Let's implement the async handler here.
             
             PickRegionAsync(condition).ConfigureAwait(false);
        }

        private async Task PickRegionAsync(object condition)
        {
            var rect = await GetRegionInteraction.Handle(Unit.Default);
            if (rect.HasValue)
            {
                var r = rect.Value;
                
                // Common scaling logic
                var parentGroup = SelectedGroup != null ? ResolveGroupContext(SelectedGroup) : (SelectedSequence != null ? GetEffectiveGroupContext(SelectedSequence) : null);
                double winX = 0, winY = 0;
                
                if (parentGroup != null && parentGroup.CoordinateMode == CoordinateMode.WindowRelative)
                {
                     var winInfo = GetTargetWindowInfo(parentGroup);
                     if (winInfo.HasValue)
                     {
                         parentGroup.RefWindowWidth = winInfo.Value.Width;
                         parentGroup.RefWindowHeight = winInfo.Value.Height;
                         winX = winInfo.Value.X;
                         winY = winInfo.Value.Y;
                     }
                }

                if (condition is ImageMatchCondition img)
                {
                    img.UseRegion = true;
                    img.RegionX = (int)(r.X - winX);
                    img.RegionY = (int)(r.Y - winY);
                    img.RegionW = (int)r.Width;
                    img.RegionH = (int)r.Height;
                }
                else if (condition is GrayChangeCondition gray)
                {
                    gray.X = (int)(r.X - winX);
                    gray.Y = (int)(r.Y - winY);
                    gray.Width = (int)r.Width;
                    gray.Height = (int)r.Height;
                }
            }
        }

        private void SelectImage(ImageMatchCondition condition)
        {
            if (condition == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.bmp",
                Title = "Select Template Image"
            };

            if (dlg.ShowDialog() == true)
            {
                SaveImageToRecipe(condition, dlg.FileName);
                UpdateCurrentGroupReferenceSize(condition);
            }
        }

        private async Task CaptureImageAsync(ImageMatchCondition condition)
        {
            if (condition == null) return;

            var tempPath = await CaptureImageInteraction.Handle(Unit.Default);

            if (!string.IsNullOrEmpty(tempPath) && System.IO.File.Exists(tempPath))
            {
                SaveImageToRecipe(condition, tempPath);
                try { System.IO.File.Delete(tempPath); } catch { }
                UpdateCurrentGroupReferenceSize(condition);
            }
        }

        private void UpdateCurrentGroupReferenceSize(IMacroCondition targetCondition)
        {
            SequenceGroup? groupToUpdate = null;

            if (SelectedSequence != null)
            {
                if (SelectedSequence.PreCondition == targetCondition || 
                    SelectedSequence.PostCondition == targetCondition)
                {
                    groupToUpdate = GetEffectiveGroupContext(SelectedSequence);
                }
            }
            
            if (groupToUpdate == null && SelectedGroup != null)
            {
                groupToUpdate = ResolveGroupContext(SelectedGroup);
            }

            if (groupToUpdate != null && groupToUpdate.CoordinateMode == CoordinateMode.WindowRelative)
            {
                 var winInfo = GetTargetWindowInfo(groupToUpdate);
                 if (winInfo.HasValue)
                 {
                     groupToUpdate.RefWindowWidth = winInfo.Value.Width;
                     groupToUpdate.RefWindowHeight = winInfo.Value.Height;
                 }
            }
        }

        #endregion
    }
}