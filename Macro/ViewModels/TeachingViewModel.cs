using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Macro.Models;
using Macro.Services;
using Macro.Utils;
using ReactiveUI;

namespace Macro.ViewModels
{
    public partial class TeachingViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        #region Fields

        private SequenceGroup? _selectedGroup;
        private SequenceItem? _selectedSequence;
        private string _currentRecipeName = "No Recipe Selected";
        private bool _isLoading;
        private bool _isUpdatingGroupTargets; 
        private bool _isVariableManagerOpen; 
        private string _selectedSubActionType = "Mouse Click";
        private string _clipboardJson = string.Empty;
        private bool _clipboardIsGroup = false;
        private System.Windows.Media.Imaging.BitmapSource? _testResultImage;

        // ComboBox Lists
        public List<string> ConditionTypes { get; } = new List<string> { "None", "Delay", "Image Match", "Gray Change", "Variable Compare", "Switch Case" };
        public List<string> ActionTypes { get; } = new List<string> { "Idle", "Mouse Click", "Key Press", "Variable Set", "Window Control", "Multi Action" };
        
        public List<WindowControlState> WindowControlStates { get; } = new List<WindowControlState>
        {
            WindowControlState.Restore,
            WindowControlState.Maximize,
            WindowControlState.Minimize
        };
        public List<WindowControlSearchMethod> SearchMethods { get; } = new List<WindowControlSearchMethod>
        {
            WindowControlSearchMethod.ProcessName,
            WindowControlSearchMethod.WindowTitle
        };

        public List<CoordinateMode> CoordinateModes { get; } = new List<CoordinateMode>
        {
            CoordinateMode.Global,
            CoordinateMode.WindowRelative,
            CoordinateMode.ParentRelative
        };

        #endregion

        #region Properties

        public string UrlPathSegment => "Teaching";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public ObservableCollection<SequenceGroup> Groups { get; } = new ObservableCollection<SequenceGroup>();
        
        public ObservableCollection<VariableDefinition> DefinedVariables { get; } = new ObservableCollection<VariableDefinition>();

        public bool IsVariableManagerOpen
        {
            get => _isVariableManagerOpen;
            set => this.RaiseAndSetIfChanged(ref _isVariableManagerOpen, value);
        }

        private ObservableCollection<JumpTargetViewModel> _jumpTargets = new ObservableCollection<JumpTargetViewModel>();
        public ObservableCollection<JumpTargetViewModel> JumpTargets
        {
            get => _jumpTargets;
            set => this.RaiseAndSetIfChanged(ref _jumpTargets, value);
        }

        private ObservableCollection<JumpTargetViewModel> _availableGroupEntryTargets = new ObservableCollection<JumpTargetViewModel>();
        public ObservableCollection<JumpTargetViewModel> AvailableGroupEntryTargets
        {
            get => _availableGroupEntryTargets;
            set => this.RaiseAndSetIfChanged(ref _availableGroupEntryTargets, value);
        }

        private ObservableCollection<JumpTargetViewModel> _availableGroupExitTargets = new ObservableCollection<JumpTargetViewModel>();
        public ObservableCollection<JumpTargetViewModel> AvailableGroupExitTargets
        {
            get => _availableGroupExitTargets;
            set => this.RaiseAndSetIfChanged(ref _availableGroupExitTargets, value);
        }
        
        // Backing Fields for Stable Binding
        private string _selectedGroupEntryJumpId = string.Empty;
        private string _selectedGroupExitJumpId = string.Empty;
        
        private string _selectedStepSuccessJumpId = string.Empty;
        private string _selectedStepPreConditionFailJumpId = string.Empty;
        private string _selectedStepActionFailJumpId = string.Empty;
        private string _selectedStepPostConditionFailJumpId = string.Empty;

        public string SelectedGroupEntryJumpId
        {
            get => _selectedGroupEntryJumpId;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedGroupEntryJumpId, value);
                if (!_isUpdatingGroupTargets && SelectedGroup != null)
                {
                    var startStep = SelectedGroup.Nodes.OfType<SequenceItem>().FirstOrDefault(i => i.IsGroupStart);
                    if (startStep != null && startStep.SuccessJumpId != value)
                    {
                        startStep.SuccessJumpId = value;
                    }
                }
            }
        }

        public string SelectedGroupExitJumpId
        {
            get => _selectedGroupExitJumpId;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedGroupExitJumpId, value);
                if (!_isUpdatingGroupTargets && SelectedGroup != null)
                {
                    var endStep = SelectedGroup.Nodes.OfType<SequenceItem>().FirstOrDefault(i => i.IsGroupEnd);
                    if (endStep != null && endStep.SuccessJumpId != value)
                    {
                        endStep.SuccessJumpId = value;
                    }
                }
            }
        }

        public string SelectedStepSuccessJumpId
        {
            get => _selectedStepSuccessJumpId;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedStepSuccessJumpId, value);
                if (!_isUpdatingGroupTargets && SelectedSequence != null)
                {
                    SelectedSequence.SuccessJumpId = value;
                }
            }
        }

        public string SelectedStepPreConditionFailJumpId
        {
            get => _selectedStepPreConditionFailJumpId;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedStepPreConditionFailJumpId, value);
                if (!_isUpdatingGroupTargets && SelectedSequence?.PreCondition != null)
                {
                    SelectedSequence.PreCondition.FailJumpId = value;
                }
            }
        }

        public string SelectedStepActionFailJumpId
        {
            get => _selectedStepActionFailJumpId;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedStepActionFailJumpId, value);
                if (!_isUpdatingGroupTargets && SelectedSequence?.Action != null)
                {
                    SelectedSequence.Action.FailJumpId = value;
                }
            }
        }

        public string SelectedStepPostConditionFailJumpId
        {
            get => _selectedStepPostConditionFailJumpId;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedStepPostConditionFailJumpId, value);
                if (!_isUpdatingGroupTargets && SelectedSequence?.PostCondition != null)
                {
                    SelectedSequence.PostCondition.FailJumpId = value;
                }
            }
        }
        
        public ObservableCollection<string> TargetList { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ProcessList { get; } = new ObservableCollection<string>();

        public string CurrentRecipeName
        {
            get => _currentRecipeName;
            set => this.RaiseAndSetIfChanged(ref _currentRecipeName, value);
        }

        public SequenceGroup? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (_selectedGroup != value)
                {
                    DebugLogger.Log($"[VM] SelectedGroup Changing: {(value?.Name ?? "null")} (ID: {value?.Id})");
                    _selectedGroup = value;
                    
                    UpdateGroupProxyProperties();
                    
                    this.RaisePropertyChanged(nameof(SelectedGroup));
                    DebugLogger.Log($"[VM] SelectedGroup Changed & Notified");
                }
            }
        }

        public SequenceItem? SelectedSequence
        {
            get => _selectedSequence;
            set
            {
                if (_selectedSequence != value)
                {
                    DebugLogger.Log($"[VM] SelectedSequence Changing: {(value?.Name ?? "null")} (ID: {value?.Id})");
                    if (value != null)
                    {
                        DebugLogger.Log($"[VM] > Current SuccessJumpId: '{value.SuccessJumpId}'");
                    }

                    _selectedSequence = value;
                    
                    // [Restored] Update targets to apply smart filtering (Current Group + Force Includes)
                    UpdateJumpTargets();
                    
                    this.RaisePropertyChanged(nameof(SelectedSequence));
                    
                    NotifyTypeChanges();
                    DebugLogger.Log($"[VM] SelectedSequence Changed & Notified");
                }
            }
        }

        public string SelectedPreConditionType
        {
            get => GetConditionType(SelectedSequence?.PreCondition);
            set => SetPreCondition(value);
        }

        public string SelectedActionType
        {
            get => GetActionType(SelectedSequence?.Action);
            set => SetAction(value);
        }

        public string SelectedPostConditionType
        {
            get => GetConditionType(SelectedSequence?.PostCondition);
            set => SetPostCondition(value);
        }

        public string SelectedSubActionType
        {
            get => _selectedSubActionType;
            set => this.RaiseAndSetIfChanged(ref _selectedSubActionType, value);
        }

        public System.Windows.Media.Imaging.BitmapSource? TestResultImage
        {
            get => _testResultImage;
            set => this.RaiseAndSetIfChanged(ref _testResultImage, value);
        }

        #endregion

        #region Constructor

        public TeachingViewModel(IScreen screen)
        {
            HostScreen = screen;
            
            Groups.CollectionChanged += (s, e) => UpdateJumpTargets();
            
            UpdateJumpTargets();

            this.WhenAnyValue(x => x.SelectedGroup)
                .Subscribe(_ => UpdateGroupJumpTargets());

            InitializeCommands();

            this.WhenActivated(disposables =>
            {
                LoadData();
                
                var dRecipe = RecipeManager.Instance.WhenAnyValue(x => x.CurrentRecipe)
                    .Skip(1)
                    .Subscribe(_ => LoadData());
                disposables.Add(dRecipe);
            });
        }

        #endregion
    }

    public class JumpTargetViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsGroup { get; set; }
    }
}