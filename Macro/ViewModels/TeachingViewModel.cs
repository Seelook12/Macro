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
using Macro.Services;
using Macro.Utils;
using ReactiveUI;

namespace Macro.ViewModels
{
    public class TeachingViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        #region Fields

        private SequenceItem? _selectedSequence;
        private string _currentRecipeName = "No Recipe Selected";
        private ObservableCollection<string> _sequenceNames = new ObservableCollection<string>();

        // ComboBox Lists
        public List<string> ConditionTypes { get; } = new List<string> { "None", "Delay", "Image Match", "Gray Change" };
        public List<string> ActionTypes { get; } = new List<string> { "Mouse Click", "Key Press" };

        #endregion

        #region Properties

        public string UrlPathSegment => "Teaching";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public ObservableCollection<SequenceItem> Sequences { get; }

        public ObservableCollection<string> SequenceNames => _sequenceNames;

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
        public ReactiveCommand<SequenceItem, Unit> RunSingleStepCommand { get; }
        
        // Interaction: Input(Unit) -> Output(Point?)
        public Interaction<Unit, System.Windows.Point?> GetCoordinateInteraction { get; } = new Interaction<Unit, System.Windows.Point?>();
        public ReactiveCommand<Unit, Unit> PickCoordinateCommand { get; }

        // Interaction: Input(Unit) -> Output(Rect?)
        public Interaction<Unit, System.Windows.Rect?> GetRegionInteraction { get; } = new Interaction<Unit, System.Windows.Rect?>();
        
        // Interaction: Input(Unit) -> Output(string? TempFilePath)
        public Interaction<Unit, string?> CaptureImageInteraction { get; } = new Interaction<Unit, string?>();

        public ReactiveCommand<ImageMatchCondition, Unit> SelectImageCommand { get; }
        public ReactiveCommand<ImageMatchCondition, Unit> CaptureImageCommand { get; }
        public ReactiveCommand<object, Unit> PickRegionCommand { get; }

        #endregion

        #region Constructor

        public TeachingViewModel(IScreen screen)
        {
            HostScreen = screen;
            Sequences = new ObservableCollection<SequenceItem>();

            // 스텝 이름 목록 자동 갱신
            var sequencesChanged = Observable.FromEventPattern<System.Collections.Specialized.NotifyCollectionChangedEventHandler, System.Collections.Specialized.NotifyCollectionChangedEventArgs>(
                h => Sequences.CollectionChanged += h,
                h => Sequences.CollectionChanged -= h);

            sequencesChanged
                .Subscribe(_ => UpdateSequenceNames());

            // 초기 목록 생성
            UpdateSequenceNames();

            // Initialize Commands
            AddSequenceCommand = ReactiveCommand.Create(AddSequence);
            RemoveSequenceCommand = ReactiveCommand.Create(RemoveSequence, this.WhenAnyValue(x => x.SelectedSequence, (SequenceItem? item) => item != null));
            SaveCommand = ReactiveCommand.CreateFromTask(SaveSequencesAsync);
            RunSingleStepCommand = ReactiveCommand.CreateFromTask<SequenceItem>(RunSingleStepAsync);
            
            // 좌표 픽업 커맨드
            PickCoordinateCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedSequence?.Action is MouseClickAction mouseAction)
                {
                    var point = await GetCoordinateInteraction.Handle(Unit.Default);
                    if (point.HasValue)
                    {
                        mouseAction.X = (int)point.Value.X;
                        mouseAction.Y = (int)point.Value.Y;
                    }
                }
            }, this.WhenAnyValue(x => x.SelectedSequence, (SequenceItem? item) => item?.Action is MouseClickAction));

            // 이미지 선택 커맨드
            SelectImageCommand = ReactiveCommand.Create<ImageMatchCondition>(condition =>
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
                }
            });

            // 이미지 캡처 커맨드
            CaptureImageCommand = ReactiveCommand.CreateFromTask<ImageMatchCondition>(async condition =>
            {
                if (condition == null) return;

                var tempPath = await CaptureImageInteraction.Handle(Unit.Default);

                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    SaveImageToRecipe(condition, tempPath);
                    try { File.Delete(tempPath); } catch { }
                }
            });

            // 영역 선택 커맨드 (범용)
            PickRegionCommand = ReactiveCommand.CreateFromTask<object>(async obj =>
            {
                if (obj == null) return;

                var rect = await GetRegionInteraction.Handle(Unit.Default);
                if (rect.HasValue)
                {
                    var r = rect.Value;
                    if (obj is ImageMatchCondition imgMatch)
                    {
                        imgMatch.UseRegion = true;
                        imgMatch.RegionX = (int)r.X;
                        imgMatch.RegionY = (int)r.Y;
                        imgMatch.RegionW = (int)r.Width;
                        imgMatch.RegionH = (int)r.Height;
                    }
                    else if (obj is GrayChangeCondition grayChange)
                    {
                        grayChange.X = (int)r.X;
                        grayChange.Y = (int)r.Y;
                        grayChange.Width = (int)r.Width;
                        grayChange.Height = (int)r.Height;
                    }
                }
            });

            this.WhenActivated(disposables =>
            {
                // 1. 화면 진입 시 초기 로드
                LoadData();
                
                // 2. 화면이 활성화된 상태에서 레시피가 변경되면 다시 로드 (Reactive)
                RecipeManager.Instance.WhenAnyValue(x => x.CurrentRecipe)
                    .Skip(1)
                    .Subscribe(_ => LoadData())
                    .DisposeWith(disposables);
            });
        }

        #endregion

        #region Logic Methods

        private void UpdateSequenceNames()
        {
            _sequenceNames.Clear();
            _sequenceNames.Add("(Next Step)");
            _sequenceNames.Add("(Ignore & Continue)");
            _sequenceNames.Add("(Stop Execution)");
            
            foreach (var item in Sequences)
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    _sequenceNames.Add(item.Name);
                }
            }
        }

        private async Task RunSingleStepAsync(SequenceItem item)
        {
            if (item == null) return;
            try
            {
                System.Windows.Point? foundPoint = null;
                if (item.PreCondition != null)
                {
                    bool check = await item.PreCondition.CheckAsync();
                    if (check) foundPoint = item.PreCondition.FoundPoint;
                }
                await item.Action.ExecuteAsync(foundPoint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Single run failed: {ex.Message}");
            }
        }

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
                    if (!string.IsNullOrWhiteSpace(json) && json != "{}")
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
                var json = JsonSerializer.Serialize(Sequences, GetJsonOptions());
                await File.WriteAllTextAsync(currentRecipe.FilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save recipe: {ex.Message}");
            }
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
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
                GrayChangeCondition => "Gray Change",
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
                "Gray Change" => new GrayChangeCondition { Threshold = 10.0 },
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
                "Gray Change" => new GrayChangeCondition { Threshold = 10.0 },
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

        private void SaveImageToRecipe(ImageMatchCondition condition, string sourcePath)
        {
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            
            if (currentRecipe != null && !string.IsNullOrEmpty(currentRecipe.FilePath))
            {
                var recipeDir = Path.GetDirectoryName(currentRecipe.FilePath);
                if (recipeDir != null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(sourcePath);
                    var ext = Path.GetExtension(sourcePath);
                    var newFileName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    
                    var destPath = Path.Combine(recipeDir, newFileName);
                    
                    try
                    {
                        File.Copy(sourcePath, destPath, true);
                        condition.ImagePath = destPath; 
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