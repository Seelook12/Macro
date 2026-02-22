using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Text.Json;
using Macro.Models;
using Macro.Utils;
using ReactiveUI;

namespace Macro.ViewModels
{
    public class GroupScopeItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public SequenceGroup? Group { get; set; }
        public bool IsGlobal => Group == null;
    }

    public class VariableManagerViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        private static readonly JsonSerializerOptions _writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string UrlPathSegment => "VariableManager";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        private readonly TeachingViewModel _teachingVM;

        // 전역 변수 (TeachingVM과 공유)
        private ObservableCollection<VariableDefinition> _definedVariables = new ObservableCollection<VariableDefinition>();
        public ObservableCollection<VariableDefinition> DefinedVariables
        {
            get => _definedVariables;
            set => this.RaiseAndSetIfChanged(ref _definedVariables, value);
        }

        // 그룹 스코프 목록
        public ObservableCollection<GroupScopeItem> IntScopes { get; } = new ObservableCollection<GroupScopeItem>();
        public ObservableCollection<GroupScopeItem> CoordScopes { get; } = new ObservableCollection<GroupScopeItem>();

        private GroupScopeItem? _selectedIntScope;
        public GroupScopeItem? SelectedIntScope
        {
            get => _selectedIntScope;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedIntScope, value);
                UpdateIntVariableView();
            }
        }

        private GroupScopeItem? _selectedCoordScope;
        public GroupScopeItem? SelectedCoordScope
        {
            get => _selectedCoordScope;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCoordScope, value);
                UpdateCoordVariableView();
            }
        }

        // 현재 표시 중인 정수 변수 (그룹 선택 시)
        private ObservableCollection<GroupIntVariable>? _currentGroupIntVariables;
        public ObservableCollection<GroupIntVariable>? CurrentGroupIntVariables
        {
            get => _currentGroupIntVariables;
            set => this.RaiseAndSetIfChanged(ref _currentGroupIntVariables, value);
        }

        // 현재 표시 중인 좌표 변수
        private ObservableCollection<CoordinateVariable>? _currentCoordVariables;
        public ObservableCollection<CoordinateVariable>? CurrentCoordVariables
        {
            get => _currentCoordVariables;
            set => this.RaiseAndSetIfChanged(ref _currentCoordVariables, value);
        }

        // 전역 변수가 선택되었는지 여부 (DataGrid 컬럼 전환용)
        private bool _isGlobalScope = true;
        public bool IsGlobalScope
        {
            get => _isGlobalScope;
            set => this.RaiseAndSetIfChanged(ref _isGlobalScope, value);
        }

        private bool _hasCoordScope;
        public bool HasCoordScope
        {
            get => _hasCoordScope;
            set => this.RaiseAndSetIfChanged(ref _hasCoordScope, value);
        }

        // 커맨드
        public ReactiveCommand<Unit, Unit> AddGlobalVariableCommand { get; }
        public ReactiveCommand<VariableDefinition, Unit> RemoveGlobalVariableCommand { get; }
        public ReactiveCommand<Unit, Unit> AddGroupIntVariableCommand { get; }
        public ReactiveCommand<GroupIntVariable, Unit> RemoveGroupIntVariableCommand { get; }
        public ReactiveCommand<Unit, Unit> AddCoordVariableCommand { get; }
        public ReactiveCommand<CoordinateVariable, Unit> RemoveCoordVariableCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public VariableManagerViewModel(IScreen screen, ObservableCollection<VariableDefinition> sharedVariables, TeachingViewModel teachingVM)
        {
            HostScreen = screen;
            DefinedVariables = sharedVariables;
            _teachingVM = teachingVM;

            AddGlobalVariableCommand = ReactiveCommand.Create(AddGlobalVariable);
            RemoveGlobalVariableCommand = ReactiveCommand.Create<VariableDefinition>(RemoveGlobalVariable);
            AddGroupIntVariableCommand = ReactiveCommand.Create(AddGroupIntVariable);
            RemoveGroupIntVariableCommand = ReactiveCommand.Create<GroupIntVariable>(RemoveGroupIntVariable);
            AddCoordVariableCommand = ReactiveCommand.Create(AddCoordVariable);
            RemoveCoordVariableCommand = ReactiveCommand.Create<CoordinateVariable>(RemoveCoordVariable);
            SaveCommand = ReactiveCommand.Create(SaveAll);

            foreach (var cmd in new IHandleObservableErrors[]
            {
                AddGlobalVariableCommand, RemoveGlobalVariableCommand,
                AddGroupIntVariableCommand, RemoveGroupIntVariableCommand,
                AddCoordVariableCommand, RemoveCoordVariableCommand,
                SaveCommand
            })
            {
                cmd.ThrownExceptions.Subscribe(ex =>
                    System.Diagnostics.Debug.WriteLine($"[Command Error] {ex.Message}"));
            }

            this.WhenActivated(disposables =>
            {
                RefreshGroupScopes();
                Disposable.Create(() => { }).DisposeWith(disposables);
            });
        }

        public void RefreshGroupScopes()
        {
            var allGroups = new List<GroupScopeItem>();
            CollectGroupsRecursive(_teachingVM.Groups, allGroups, 0);

            // 정수 변수 스코프: 전역 + 그룹
            IntScopes.Clear();
            IntScopes.Add(new GroupScopeItem { DisplayName = "[Global] 전역 변수", Group = null });
            foreach (var g in allGroups) IntScopes.Add(g);

            // 좌표 변수 스코프: 그룹만
            CoordScopes.Clear();
            foreach (var g in allGroups) CoordScopes.Add(g);

            HasCoordScope = CoordScopes.Count > 0;

            // 기본 선택
            if (SelectedIntScope == null || !IntScopes.Contains(SelectedIntScope))
                SelectedIntScope = IntScopes.FirstOrDefault();
            if (SelectedCoordScope == null || !CoordScopes.Contains(SelectedCoordScope))
                SelectedCoordScope = CoordScopes.FirstOrDefault();
        }

        private void CollectGroupsRecursive(IEnumerable<SequenceGroup> groups, List<GroupScopeItem> result, int depth)
        {
            foreach (var group in groups)
            {
                if (group.IsStartGroup) continue;
                var prefix = new string(' ', depth * 2);
                result.Add(new GroupScopeItem { DisplayName = $"{prefix}{group.Name}", Group = group });
                var childGroups = group.Nodes.OfType<SequenceGroup>().ToList();
                if (childGroups.Count > 0)
                    CollectGroupsRecursive(childGroups, result, depth + 1);
            }
        }

        private void UpdateIntVariableView()
        {
            if (SelectedIntScope == null)
            {
                IsGlobalScope = true;
                CurrentGroupIntVariables = null;
                return;
            }
            IsGlobalScope = SelectedIntScope.IsGlobal;
            CurrentGroupIntVariables = SelectedIntScope.Group?.IntVariables;
        }

        private void UpdateCoordVariableView()
        {
            CurrentCoordVariables = SelectedCoordScope?.Group?.Variables;
        }

        private void AddGlobalVariable()
        {
            DefinedVariables.Add(new VariableDefinition
            {
                Name = $"NewVar_{DefinedVariables.Count + 1}",
                DefaultValue = "0",
                Description = "New Variable"
            });
        }

        private void RemoveGlobalVariable(VariableDefinition variable)
        {
            if (variable != null && DefinedVariables.Contains(variable))
                DefinedVariables.Remove(variable);
        }

        private void AddGroupIntVariable()
        {
            var group = SelectedIntScope?.Group;
            if (group == null) return;
            group.IntVariables.Add(new GroupIntVariable
            {
                Name = $"IntVar_{group.IntVariables.Count + 1}",
                Value = 0,
                Description = "New Integer Variable"
            });
        }

        private void RemoveGroupIntVariable(GroupIntVariable variable)
        {
            var group = SelectedIntScope?.Group;
            if (group == null || variable == null) return;
            group.IntVariables.Remove(variable);
        }

        private void AddCoordVariable()
        {
            var group = SelectedCoordScope?.Group;
            if (group == null) return;
            group.Variables.Add(new CoordinateVariable
            {
                Name = $"Point_{group.Variables.Count + 1}",
                X = 0, Y = 0,
                Description = "New Coordinate"
            });
        }

        private void RemoveCoordVariable(CoordinateVariable variable)
        {
            var group = SelectedCoordScope?.Group;
            if (group == null || variable == null) return;
            group.Variables.Remove(variable);
        }

        private void SaveAll()
        {
            // 1. 전역 변수 저장 (.vars.json)
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath)) return;
            var varsPath = Path.ChangeExtension(currentRecipe.FilePath, ".vars.json");
            try
            {
                var json = JsonSerializer.Serialize(DefinedVariables, _writeOptions);
                File.WriteAllText(varsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VariableManager] Failed to save variables: {ex.Message}");
            }

            // 2. 레시피 저장 (그룹 변수 포함)
            _teachingVM.SaveCommand.Execute().Subscribe();
        }
    }
}
