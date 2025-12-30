using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Macro.Models;
using Macro.Utils;
using ReactiveUI;

namespace Macro.ViewModels
{
    public class TeachingViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        #region Fields

        private SequenceItem? _selectedSequence;
        private string _currentRecipeName = "No Recipe Selected";

        // ComboBox Lists
        public List<string> ConditionTypes { get; } = new List<string> { "None", "Delay", "Image Match" };
        public List<string> ActionTypes { get; } = new List<string> { "Mouse Click", "Key Press" };

        #endregion

        #region Properties

        public string UrlPathSegment => "Teaching";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public ObservableCollection<SequenceItem> Sequences { get; }

        public string CurrentRecipeName
        {
            get => _currentRecipeName;
            set => this.RaiseAndSetIfChanged(ref _currentRecipeName, value);
        }

        public SequenceItem? SelectedSequence
        {
            get => _selectedSequence;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSequence, value);
                NotifyTypeChanges();
            }
        }

        // Bound Properties for ComboBoxes
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

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> AddSequenceCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveSequenceCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        #endregion

        #region Constructor

        public TeachingViewModel(IScreen screen)
        {
            HostScreen = screen;
            Sequences = new ObservableCollection<SequenceItem>();

            // Initialize Commands
            AddSequenceCommand = ReactiveCommand.Create(AddSequence);
            RemoveSequenceCommand = ReactiveCommand.Create(RemoveSequence, this.WhenAnyValue(x => x.SelectedSequence, (SequenceItem? item) => item != null));
            SaveCommand = ReactiveCommand.CreateFromTask(SaveSequencesAsync);

            // Activation Logic (Load Data)
            this.WhenActivated(disposables =>
            {
                // 1. 화면 진입 시 초기 로드
                LoadData();
                
                // 2. 화면이 활성화된 상태에서 레시피가 변경되면 다시 로드 (Reactive)
                RecipeManager.Instance.WhenAnyValue(x => x.CurrentRecipe)
                    .Skip(1) // 첫 번째 값은 위 LoadData()에서 처리했거나 중복일 수 있으므로 건너뜀 (선택 사항이나 안전하게)
                    .Subscribe(_ => LoadData())
                    .DisposeWith(disposables);

                // 필요한 경우 해제 로직 추가
                Disposable.Create(() => 
                { 
                    // 비활성화 시 필요한 작업
                }).DisposeWith(disposables);
            });
        }

        #endregion

        #region Logic Methods

        private void LoadData()
        {
            Sequences.Clear();
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
                    if (!string.IsNullOrWhiteSpace(json) && json != "{}") // 빈 파일 체크
                    {
                        var loadedData = JsonSerializer.Deserialize<List<SequenceItem>>(json, GetJsonOptions());
                        if (loadedData != null)
                        {
                            foreach (var item in loadedData)
                            {
                                Sequences.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load recipe: {ex.Message}");
                // TODO: Show Error Message
            }
        }

        private async Task SaveSequencesAsync()
        {
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath))
            {
                // TODO: Show Warning "No recipe selected"
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(Sequences, GetJsonOptions());
                await File.WriteAllTextAsync(currentRecipe.FilePath, json);
                // TODO: Show Success Message (Optional)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save recipe: {ex.Message}");
                // TODO: Show Error Message
            }
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
                // Note: Polymorphism is handled by attributes on the Interface definitions in MacroModels.cs
            };
        }

        // --- Helper Methods for Type Handling ---

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
                _ => "None"
            };
        }

        private string GetActionType(IMacroAction? action)
        {
            return action switch
            {
                MouseClickAction => "Mouse Click",
                KeyPressAction => "Key Press",
                _ => "Mouse Click" // Default
            };
        }

        private void SetPreCondition(string type)
        {
            if (SelectedSequence == null) return;

            SelectedSequence.PreCondition = type switch
            {
                "Delay" => new DelayCondition { DelayTimeMs = 1000 },
                "Image Match" => new ImageMatchCondition { Threshold = 0.9 },
                _ => null
            };
            this.RaisePropertyChanged(nameof(SelectedPreConditionType));
        }

        private void SetPostCondition(string type)
        {
            if (SelectedSequence == null) return;

            SelectedSequence.PostCondition = type switch
            {
                "Delay" => new DelayCondition { DelayTimeMs = 500 },
                "Image Match" => new ImageMatchCondition { Threshold = 0.9 },
                _ => null
            };
            this.RaisePropertyChanged(nameof(SelectedPostConditionType));
        }

        private void SetAction(string type)
        {
            if (SelectedSequence == null) return;

            if (type == "Mouse Click" && !(SelectedSequence.Action is MouseClickAction))
            {
                SelectedSequence.Action = new MouseClickAction();
            }
            else if (type == "Key Press" && !(SelectedSequence.Action is KeyPressAction))
            {
                SelectedSequence.Action = new KeyPressAction();
            }
            this.RaisePropertyChanged(nameof(SelectedActionType));
        }

        private void AddSequence()
        {
            var newAction = new MouseClickAction { X = 100, Y = 100, ClickType = "Left" };
            var newItem = new SequenceItem(newAction)
            {
                Name = $"Step {Sequences.Count + 1}",
                IsEnabled = true
            };

            Sequences.Add(newItem);
            SelectedSequence = newItem;
        }

        private void RemoveSequence()
        {
            if (SelectedSequence != null)
            {
                Sequences.Remove(SelectedSequence);
                SelectedSequence = null;
            }
        }

        #endregion
    }
}
