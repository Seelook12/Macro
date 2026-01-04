using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
    public class TeachingViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        #region Fields

        private SequenceItem? _selectedSequence;
        private string _currentRecipeName = "No Recipe Selected";
        private ObservableCollection<string> _sequenceNames = new ObservableCollection<string>();

        // ComboBox Lists
        public List<string> ConditionTypes { get; } = new List<string> { "None", "Delay", "Image Match", "Gray Change", "Variable Compare" };
        public List<string> ActionTypes { get; } = new List<string> { "Idle", "Mouse Click", "Key Press", "Variable Set", "Window Control" };

        #endregion

        #region Properties

        public string UrlPathSegment => "Teaching";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public ObservableCollection<SequenceItem> Sequences { get; }

        public ObservableCollection<string> SequenceNames => _sequenceNames;
        public ObservableCollection<string> TargetList { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ProcessList { get; } = new ObservableCollection<string>(); // New Collection
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
            CoordinateMode.WindowRelative
        };

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

        // Jump Target Selection Helpers (Name <-> Id Mapping)
        public string SuccessJumpTarget
        {
            get => GetJumpNameFromId(SelectedSequence?.SuccessJumpId, SelectedSequence?.SuccessJumpName);
            set
            {
                if (SelectedSequence != null)
                {
                    SelectedSequence.SuccessJumpName = value;
                    SelectedSequence.SuccessJumpId = GetJumpIdFromName(value);
                    this.RaisePropertyChanged(nameof(SuccessJumpTarget));
                }
            }
        }

        public string ProcessNotFoundJumpTarget
        {
            get => GetJumpNameFromId(SelectedSequence?.ProcessNotFoundJumpId, SelectedSequence?.ProcessNotFoundJumpName);
            set
            {
                if (SelectedSequence != null)
                {
                    SelectedSequence.ProcessNotFoundJumpName = value;
                    SelectedSequence.ProcessNotFoundJumpId = GetJumpIdFromName(value);
                    this.RaisePropertyChanged(nameof(ProcessNotFoundJumpTarget));
                }
            }
        }

        public string PreFailJumpTarget
        {
            get => GetJumpNameFromId(SelectedSequence?.PreCondition?.FailJumpId, SelectedSequence?.PreCondition?.FailJumpName);
            set
            {
                if (SelectedSequence?.PreCondition != null)
                {
                    SelectedSequence.PreCondition.FailJumpName = value;
                    SelectedSequence.PreCondition.FailJumpId = GetJumpIdFromName(value);
                    this.RaisePropertyChanged(nameof(PreFailJumpTarget));
                }
            }
        }

        public string ActionFailJumpTarget
        {
            get => GetJumpNameFromId(SelectedSequence?.Action?.FailJumpId, SelectedSequence?.Action?.FailJumpName);
            set
            {
                if (SelectedSequence?.Action != null)
                {
                    SelectedSequence.Action.FailJumpName = value;
                    SelectedSequence.Action.FailJumpId = GetJumpIdFromName(value);
                    this.RaisePropertyChanged(nameof(ActionFailJumpTarget));
                }
            }
        }

        public string PostFailJumpTarget
        {
            get => GetJumpNameFromId(SelectedSequence?.PostCondition?.FailJumpId, SelectedSequence?.PostCondition?.FailJumpName);
            set
            {
                if (SelectedSequence?.PostCondition != null)
                {
                    SelectedSequence.PostCondition.FailJumpName = value;
                    SelectedSequence.PostCondition.FailJumpId = GetJumpIdFromName(value);
                    this.RaisePropertyChanged(nameof(PostFailJumpTarget));
                }
            }
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
        public ReactiveCommand<ImageMatchCondition, Unit> TestImageConditionCommand { get; }
        public ReactiveCommand<object, Unit> PickRegionCommand { get; }
        public ReactiveCommand<WindowControlAction, Unit> RefreshTargetListCommand { get; }
        public ReactiveCommand<SequenceItem, Unit> RefreshContextTargetCommand { get; } // Updated Command

        public ReactiveCommand<SequenceItem, Unit> MoveSequenceUpCommand { get; }
        public ReactiveCommand<SequenceItem, Unit> MoveSequenceDownCommand { get; }
        public ReactiveCommand<Unit, Unit> CopySequenceCommand { get; }
        public ReactiveCommand<Unit, Unit> PasteSequenceCommand { get; }

        private string _clipboardJson = string.Empty;

        private System.Windows.Media.Imaging.BitmapSource? _testResultImage;
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
            
            MoveSequenceUpCommand = ReactiveCommand.Create<SequenceItem>(MoveSequenceUp);
            MoveSequenceDownCommand = ReactiveCommand.Create<SequenceItem>(MoveSequenceDown);
            CopySequenceCommand = ReactiveCommand.Create(CopySequence, this.WhenAnyValue(x => x.SelectedSequence, (SequenceItem? item) => item != null));
            PasteSequenceCommand = ReactiveCommand.Create(PasteSequence);

            RefreshTargetListCommand = ReactiveCommand.CreateFromTask<WindowControlAction>(async (action) => 
            {
                if (action == null) return;

                await Task.Run(() =>
                {
                    List<string> items = new List<string>();

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
            });

            // Context Target Refresh Command
            RefreshContextTargetCommand = ReactiveCommand.CreateFromTask<SequenceItem>(async (item) =>
            {
                if (item == null) return;
                
                await Task.Run(() =>
                {
                    List<string> items = new List<string>();
                    if (item.ContextSearchMethod == WindowControlSearchMethod.ProcessName)
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
            });

            // 좌표 픽업 커맨드
            PickCoordinateCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedSequence?.Action is MouseClickAction mouseAction)
                {
                    var point = await GetCoordinateInteraction.Handle(Unit.Default);
                    if (point.HasValue)
                    {
                        var p = point.Value;

                        if (SelectedSequence.CoordinateMode == CoordinateMode.WindowRelative)
                        {
                            var winInfo = GetTargetWindowInfo(SelectedSequence);
                            if (winInfo.HasValue)
                            {
                                // 자동 기준 해상도 설정
                                SelectedSequence.RefWindowWidth = winInfo.Value.Width;
                                SelectedSequence.RefWindowHeight = winInfo.Value.Height;

                                // 상대 좌표 변환
                                mouseAction.X = (int)(p.X - winInfo.Value.X);
                                mouseAction.Y = (int)(p.Y - winInfo.Value.Y);
                                return;
                            }
                        }

                        mouseAction.X = (int)p.X;
                        mouseAction.Y = (int)p.Y;
                    }
                }
            }, this.WhenAnyValue(x => x.SelectedSequence, x => x.SelectedSequence!.Action, 
                (item, action) => item != null && action is MouseClickAction));

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

            // 이미지 테스트 커맨드
            TestImageConditionCommand = ReactiveCommand.CreateFromTask<ImageMatchCondition>(async condition =>
            {
                if (condition == null) return;
                
                try 
                {
                    condition.TestResult = "Searching...";
                    TestResultImage = null;
                    
                    await Task.Run(() => 
                    {
                        var captureSource = ScreenCaptureHelper.GetScreenCapture();
                        if (captureSource == null) 
                        {
                            condition.TestResult = "Capture Failed";
                            return;
                        }

                        // 1. 비율(Scale) 및 좌표 보정값 계산
                        double scaleX = 1.0;
                        double scaleY = 1.0;
                        double winX = 0;
                        double winY = 0;

                        if (SelectedSequence != null && SelectedSequence.CoordinateMode == CoordinateMode.WindowRelative)
                        {
                            var winInfo = GetTargetWindowInfo(SelectedSequence);
                            if (winInfo.HasValue)
                            {
                                if (SelectedSequence.RefWindowWidth > 0 && SelectedSequence.RefWindowHeight > 0)
                                {
                                    scaleX = (double)winInfo.Value.Width / SelectedSequence.RefWindowWidth;
                                    scaleY = (double)winInfo.Value.Height / SelectedSequence.RefWindowHeight;
                                }
                                winX = winInfo.Value.X;
                                winY = winInfo.Value.Y;
                            }
                        }

                        // 2. ROI 설정
                        System.Windows.Rect? searchRoi = null;
                        OpenCvSharp.Rect? drawRoi = null;

                        if (condition.UseRegion && condition.RegionW > 0 && condition.RegionH > 0)
                        {
                            // (원래 상대좌표 * 비율) + 현재 창 시작점
                            double rx = condition.RegionX * scaleX + winX;
                            double ry = condition.RegionY * scaleY + winY;
                            double rw = condition.RegionW * scaleX;
                            double rh = condition.RegionH * scaleY;

                            searchRoi = new System.Windows.Rect(rx, ry, rw, rh);
                            drawRoi = new OpenCvSharp.Rect((int)rx, (int)ry, (int)rw, (int)rh);
                        }
                        
                        var result = ImageSearchService.FindImageDetailed(captureSource, condition.ImagePath, condition.Threshold, searchRoi);

                        condition.TestScore = result.Score;

                        // 3. 그리기 (OpenCV Mat)
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
                                condition.TestResult = $"Success ({result.Point.Value.X:F0}, {result.Point.Value.Y:F0})";
                                
                                int tW = 50, tH = 50;
                                try 
                                {
                                    using (var tempMat = Cv2.ImRead(condition.ImagePath))
                                    {
                                        if (!tempMat.Empty())
                                        {
                                            tW = tempMat.Width;
                                            tH = tempMat.Height;
                                        }
                                    }
                                } catch {}

                                // 마커 크기 스케일링 적용
                                int scaledW = (int)(tW * scaleX);
                                int scaledH = (int)(tH * scaleY);

                                // 중심 -> 좌상단 변환 (스케일된 크기 기준)
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
            });

            // 영역 선택 커맨드 (범용)
            PickRegionCommand = ReactiveCommand.CreateFromTask<object>(async obj =>
            {
                if (obj == null || SelectedSequence == null) return;

                var rect = await GetRegionInteraction.Handle(Unit.Default);
                if (rect.HasValue)
                {
                    var r = rect.Value;
                    double targetX = r.X;
                    double targetY = r.Y;

                    if (SelectedSequence.CoordinateMode == CoordinateMode.WindowRelative)
                    {
                        var winInfo = GetTargetWindowInfo(SelectedSequence);
                        if (winInfo.HasValue)
                        {
                            // 자동 기준 해상도 설정
                            SelectedSequence.RefWindowWidth = winInfo.Value.Width;
                            SelectedSequence.RefWindowHeight = winInfo.Value.Height;

                            // 상대 좌표 변환
                            targetX -= winInfo.Value.X;
                            targetY -= winInfo.Value.Y;
                        }
                    }

                    if (obj is ImageMatchCondition imgMatch)
                    {
                        imgMatch.UseRegion = true;
                        imgMatch.RegionX = (int)targetX;
                        imgMatch.RegionY = (int)targetY;
                        imgMatch.RegionW = (int)r.Width;
                        imgMatch.RegionH = (int)r.Height;
                    }
                    else if (obj is GrayChangeCondition grayChange)
                    {
                        grayChange.X = (int)targetX;
                        grayChange.Y = (int)targetY;
                        grayChange.Width = (int)r.Width;
                        grayChange.Height = (int)r.Height;
                    }
                }
            });

            this.WhenActivated(disposables =>
            {
                // 점프 이름 변경 시 ID 자동 동기화 관찰
                var d1 = this.WhenAnyValue(x => x.SelectedSequence)
                    .WhereNotNull()
                    .Subscribe(seq =>
                    {
                        // SuccessJumpName 감시
                        var d2 = seq.WhenAnyValue(x => x.SuccessJumpName)
                            .Subscribe(name => seq.SuccessJumpId = GetJumpIdFromName(name));
                        disposables.Add(d2);

                        // ProcessNotFoundJumpName 감시
                        var dP = seq.WhenAnyValue(x => x.ProcessNotFoundJumpName)
                            .Subscribe(name => seq.ProcessNotFoundJumpId = GetJumpIdFromName(name));
                        disposables.Add(dP);

                        // PreCondition FailJumpName 감시
                        var d3 = seq.WhenAnyValue(x => x.PreCondition)
                            .WhereNotNull()
                            .Subscribe(cond =>
                            {
                                var d4 = cond.WhenAnyValue(x => x.FailJumpName)
                                    .Subscribe(name => cond.FailJumpId = GetJumpIdFromName(name));
                                disposables.Add(d4);
                            });
                        disposables.Add(d3);

                        // Action FailJumpName 감시
                        var d5 = seq.WhenAnyValue(x => x.Action)
                            .WhereNotNull()
                            .Subscribe(act =>
                            {
                                var d6 = act.WhenAnyValue(x => x.FailJumpName)
                                    .Subscribe(name => act.FailJumpId = GetJumpIdFromName(name));
                                disposables.Add(d6);
                            });
                        disposables.Add(d5);

                        // PostCondition FailJumpName 감시
                        var d7 = seq.WhenAnyValue(x => x.PostCondition)
                            .WhereNotNull()
                            .Subscribe(cond =>
                            {
                                var d8 = cond.WhenAnyValue(x => x.FailJumpName)
                                    .Subscribe(name => cond.FailJumpId = GetJumpIdFromName(name));
                                disposables.Add(d8);
                            });
                        disposables.Add(d7);

                    });
                disposables.Add(d1);

                // 1. 화면 진입 시 초기 로드
                LoadData();
                
                // 2. 화면이 활성화된 상태에서 레시피가 변경되면 다시 로드 (Reactive)
                var dRecipe = RecipeManager.Instance.WhenAnyValue(x => x.CurrentRecipe)
                    .Skip(1)
                    .Subscribe(_ => LoadData());
                disposables.Add(dRecipe);
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
            await MacroEngineService.Instance.RunSingleStepAsync(item);
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
            this.RaisePropertyChanged(nameof(SuccessJumpTarget));
            this.RaisePropertyChanged(nameof(ProcessNotFoundJumpTarget));
        }

        private string GetJumpNameFromId(string? id, string? fallbackName)
        {
            if (string.IsNullOrEmpty(id)) return fallbackName ?? "(Next Step)";
            if (id == "(Next Step)" || id == "(Ignore & Continue)" || id == "(Stop Execution)") return id;

            var target = Sequences.FirstOrDefault(s => s.Id.ToString() == id);
            return target?.Name ?? fallbackName ?? "(Next Step)";
        }

        private string GetJumpIdFromName(string name)
        {
            if (name == "(Next Step)" || name == "(Ignore & Continue)" || name == "(Stop Execution)") return name;
            var target = Sequences.FirstOrDefault(s => s.Name == name);
            return target?.Id.ToString() ?? string.Empty;
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
                IdleAction => "Idle",
                MouseClickAction => "Mouse Click",
                KeyPressAction => "Key Press",
                VariableSetAction => "Variable Set",
                WindowControlAction => "Window Control",
                _ => "Idle" // Default
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
                "Variable Compare" => new VariableCompareCondition(),
                _ => null
            };
            this.RaisePropertyChanged(nameof(SelectedPostConditionType));
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
            else if (type == "Variable Set" && !(SelectedSequence.Action is VariableSetAction))
            {
                SelectedSequence.Action = new VariableSetAction();
            }
            else if (type == "Window Control" && !(SelectedSequence.Action is WindowControlAction))
            {
                var action = new WindowControlAction();
                SelectedSequence.Action = action;
                // 창 제어 액션을 처음 선택했을 때 목록을 한번 갱신해주면 사용자 경험이 좋음
                RefreshTargetListCommand.Execute(action).Subscribe();
            }
            this.RaisePropertyChanged(nameof(SelectedActionType));
        }

        private void AddSequence()
        {
            var newAction = new IdleAction();
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
                    // 기존 경로 보관 (삭제용)
                    string oldPath = condition.ImagePath;

                    var fileName = Path.GetFileNameWithoutExtension(sourcePath);
                    var ext = Path.GetExtension(sourcePath);
                    var newFileName = $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    
                    var destPath = Path.Combine(recipeDir, newFileName);
                    
                    try
                    {
                        File.Copy(sourcePath, destPath, true);
                        
                        // 캐시 비우기 (새 이미지가 반영되도록)
                        ImageSearchService.ClearCache();
                        
                        condition.ImagePath = destPath; 

                        // 기존 파일이 다른 곳에서 안 쓰이면 삭제
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
            foreach (var seq in Sequences)
            {
                if (IsPathMatch(seq.PreCondition, path, currentCondition)) return true;
                if (IsPathMatch(seq.PostCondition, path, currentCondition)) return true;
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

        private void RemoveSequence()
        {
            if (SelectedSequence != null)
            {
                var itemToRemove = SelectedSequence;
                Sequences.Remove(itemToRemove);
                
                // 삭제된 스텝의 이미지가 더 이상 안 쓰이면 파일 삭제 처리
                if (itemToRemove.PreCondition is ImageMatchCondition preImg) 
                    DeleteImageIfOrphaned(preImg.ImagePath);
                if (itemToRemove.PostCondition is ImageMatchCondition postImg)
                    DeleteImageIfOrphaned(postImg.ImagePath);

                SelectedSequence = null;
            }
        }

        private void DeleteImageIfOrphaned(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (!IsImagePathUsed(path, null!))
            {
                try { File.Delete(path); } catch { }
            }
        }

        private void MoveSequenceUp(SequenceItem item)
        {
            int index = Sequences.IndexOf(item);
            if (index > 0)
            {
                Sequences.Move(index, index - 1);
            }
        }

        private void MoveSequenceDown(SequenceItem item)
        {
            int index = Sequences.IndexOf(item);
            if (index < Sequences.Count - 1)
            {
                Sequences.Move(index, index + 1);
            }
        }

        private void CopySequence()
        {
            if (SelectedSequence != null)
            {
                try
                {
                    _clipboardJson = JsonSerializer.Serialize(SelectedSequence, GetJsonOptions());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
                }
            }
        }

        private void PasteSequence()
        {
            if (string.IsNullOrEmpty(_clipboardJson)) return;

            try
            {
                var newItem = JsonSerializer.Deserialize<SequenceItem>(_clipboardJson, GetJsonOptions());
                if (newItem != null)
                {
                    // ID 재생성 (중복 방지)
                    newItem.ResetId();
                    newItem.Name += " (Copy)";

                    // 현재 선택된 위치 바로 뒤에 추가, 선택된 게 없으면 맨 뒤에 추가
                    if (SelectedSequence != null)
                    {
                        int index = Sequences.IndexOf(SelectedSequence);
                        if (index >= 0 && index < Sequences.Count)
                        {
                            Sequences.Insert(index + 1, newItem);
                        }
                        else
                        {
                            Sequences.Add(newItem);
                        }
                    }
                    else
                    {
                        Sequences.Add(newItem);
                    }
                    
                    // 새로 붙여넣은 아이템 선택
                    SelectedSequence = newItem;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Paste failed: {ex.Message}");
            }
        }

        private (int X, int Y, int Width, int Height)? GetTargetWindowInfo(SequenceItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.TargetProcessName)) return null;

            IntPtr hWnd = IntPtr.Zero;
            if (item.ContextSearchMethod == WindowControlSearchMethod.ProcessName)
            {
                var p = System.Diagnostics.Process.GetProcessesByName(item.TargetProcessName).FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero);
                if (p != null) hWnd = p.MainWindowHandle;
            }
            else
            {
                hWnd = InputHelper.FindWindowByTitle(item.TargetProcessName);
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

        #endregion
    }
}
