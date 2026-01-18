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

        private SequenceGroup? _selectedGroup;
        private SequenceItem? _selectedSequence;
        private string _currentRecipeName = "No Recipe Selected";
        private bool _isLoading;
        private bool _isVariableManagerOpen; // Variable Manager Overlay Control
        
        // ComboBox Lists
        public List<string> ConditionTypes { get; } = new List<string> { "None", "Delay", "Image Match", "Gray Change", "Variable Compare", "Switch Case" };
        public List<string> ActionTypes { get; } = new List<string> { "Idle", "Mouse Click", "Key Press", "Variable Set", "Window Control", "Multi Action" };

        #endregion

        #region Properties

        public string UrlPathSegment => "Teaching";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        public ObservableCollection<SequenceGroup> Groups { get; } = new ObservableCollection<SequenceGroup>();
        
        // Defined Variables (Loaded from sidecar .vars.json)
        public ObservableCollection<VariableDefinition> DefinedVariables { get; } = new ObservableCollection<VariableDefinition>();

        public bool IsVariableManagerOpen
        {
            get => _isVariableManagerOpen;
            set => this.RaiseAndSetIfChanged(ref _isVariableManagerOpen, value);
        }

        public ObservableCollection<JumpTargetViewModel> JumpTargets { get; } = new ObservableCollection<JumpTargetViewModel>();
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

        public SequenceGroup? SelectedGroup
        {
            get => _selectedGroup;
            set => this.RaiseAndSetIfChanged(ref _selectedGroup, value);
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

        public ReactiveCommand<Unit, Unit> AddGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveGroupCommand { get; }
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
        public ReactiveCommand<SequenceGroup, Unit> RefreshContextTargetCommand { get; } // Updated Command for Group

        public ReactiveCommand<SequenceItem, Unit> MoveSequenceUpCommand { get; }
        public ReactiveCommand<SequenceItem, Unit> MoveSequenceDownCommand { get; }
        public ReactiveCommand<SequenceGroup, Unit> MoveGroupUpCommand { get; }
        public ReactiveCommand<SequenceGroup, Unit> MoveGroupDownCommand { get; }
        
        public ReactiveCommand<Unit, Unit> CopySequenceCommand { get; }
        public ReactiveCommand<Unit, Unit> PasteSequenceCommand { get; }
        
        public ReactiveCommand<Unit, Unit> CopyGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> PasteGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> DuplicateGroupCommand { get; }
        
        public ReactiveCommand<SwitchCaseCondition, Unit> AddSwitchCaseCommand { get; }
        public ReactiveCommand<SwitchCaseItem, Unit> RemoveSwitchCaseCommand { get; }

        // Variable Manager Commands
        public ReactiveCommand<Unit, Unit> OpenVariableManagerCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseVariableManagerCommand { get; }
        public ReactiveCommand<Unit, Unit> AddVariableDefinitionCommand { get; }
        public ReactiveCommand<VariableDefinition, Unit> RemoveVariableDefinitionCommand { get; }

        public ReactiveCommand<MultiAction, Unit> AddSubActionCommand { get; }
        public ReactiveCommand<IMacroAction, Unit> RemoveSubActionCommand { get; }
        
        // Helper Property for SubAction Type Selection
        private string _selectedSubActionType = "Mouse Click";
        public string SelectedSubActionType
        {
            get => _selectedSubActionType;
            set => this.RaiseAndSetIfChanged(ref _selectedSubActionType, value);
        }

        private string _clipboardJson = string.Empty;
        private bool _clipboardIsGroup = false;

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
            
            // 그룹 변경 감지 (이름 목록 갱신)
            Groups.CollectionChanged += (s, e) => UpdateJumpTargets();
            
            // 초기 목록 생성
            UpdateJumpTargets();

            // Initialize Commands
            AddGroupCommand = ReactiveCommand.Create(AddGroup);
            
            // [Modified] Start Group 삭제 방지
            var canRemoveGroup = this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null && !g.IsStartGroup);
            RemoveGroupCommand = ReactiveCommand.Create(RemoveGroup, canRemoveGroup);
            
            // [Modified] Start Group에 스텝 추가 방지
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

            CopyGroupCommand = ReactiveCommand.Create(CopyGroup, this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null));

            PasteGroupCommand = ReactiveCommand.Create(PasteGroup);

            // [Modified] Start Group 복제 방지
            DuplicateGroupCommand = ReactiveCommand.Create(DuplicateGroup, this.WhenAnyValue(x => x.SelectedGroup, (SequenceGroup? g) => g != null && !g.IsStartGroup));

            AddSwitchCaseCommand = ReactiveCommand.Create<SwitchCaseCondition>(cond => 
            {
                cond?.Cases.Add(new SwitchCaseItem { CaseValue = 0, JumpId = "" });
            });

            RemoveSwitchCaseCommand = ReactiveCommand.Create<SwitchCaseItem>(item => 
            {
                if (SelectedSequence?.PreCondition is SwitchCaseCondition pre && pre.Cases.Contains(item))
                    pre.Cases.Remove(item);
                else if (SelectedSequence?.PostCondition is SwitchCaseCondition post && post.Cases.Contains(item))
                    post.Cases.Remove(item);
            });

            // Variable Manager Init
            OpenVariableManagerCommand = ReactiveCommand.Create(() => { IsVariableManagerOpen = true; });
            CloseVariableManagerCommand = ReactiveCommand.Create(() => { IsVariableManagerOpen = false; }); // Close triggers Save logic usually, handled in SaveCommand or explicit
            
            AddVariableDefinitionCommand = ReactiveCommand.Create(() => 
            {
                DefinedVariables.Add(new VariableDefinition { Name = "NewVar", DefaultValue = "0", Description = "Description" });
            });

            RemoveVariableDefinitionCommand = ReactiveCommand.Create<VariableDefinition>(v => 
            {
                DefinedVariables.Remove(v);
            });

            AddSubActionCommand = ReactiveCommand.Create<MultiAction>(parent => 
            {
                if (parent == null) return;
                
                IMacroAction newAction = SelectedSubActionType switch
                {
                    "Idle" => new IdleAction(),
                    "Mouse Click" => new MouseClickAction(),
                    "Key Press" => new KeyPressAction(),
                    "Variable Set" => new VariableSetAction(),
                    "Window Control" => new WindowControlAction(),
                    // Prevent infinite recursion by default logic or let user decide? Let's allow but it's weird.
                    "Multi Action" => new MultiAction(), 
                    _ => new IdleAction()
                };
                
                // Initialize Window Control Action List if needed
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
                // Handle nested MultiActions if necessary (complex)
                // For now, assume single level or selected context
            });



            RefreshTargetListCommand = ReactiveCommand.CreateFromTask<WindowControlAction>(async (action) =>            {
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

            // Context Target Refresh Command (For Group)
            RefreshContextTargetCommand = ReactiveCommand.CreateFromTask<SequenceGroup>(async (group) =>
            {
                if (group == null) return;
                
                await Task.Run(() =>
                {
                    List<string> items = new List<string>();
                    if (group.ContextSearchMethod == WindowControlSearchMethod.ProcessName)
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
                        
                        // 현재 시퀀스가 속한 그룹 찾기
                        var parentGroup = FindParentGroup(SelectedSequence);

                        if (parentGroup != null && parentGroup.CoordinateMode == CoordinateMode.WindowRelative)
                        {
                            var winInfo = GetTargetWindowInfo(parentGroup);
                            if (winInfo.HasValue)
                            {
                                // 자동 기준 해상도 설정
                                parentGroup.RefWindowWidth = winInfo.Value.Width;
                                parentGroup.RefWindowHeight = winInfo.Value.Height;

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
                    
                    // Find Parent Group for Context
                    var parentGroup = SelectedGroup ?? FindParentGroup(SelectedSequence);

                    await Task.Run(() => 
                    {
                        var captureSource = ScreenCaptureHelper.GetScreenCapture();
                        var bounds = ScreenCaptureHelper.GetScreenBounds(); // Bounds 정보 획득

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

                        // 2. ROI 설정
                        System.Windows.Rect? searchRoi = null;
                        OpenCvSharp.Rect? drawRoi = null;

                        if (condition.UseRegion && condition.RegionW > 0 && condition.RegionH > 0)
                        {
                            // 절대 좌표 계산: (원래 상대좌표 * 비율) + 현재 창 시작점
                            double rxAbs = condition.RegionX * scaleX + winX;
                            double ryAbs = condition.RegionY * scaleY + winY;
                            
                            // 이미지 로컬 좌표로 변환
                            double rx = rxAbs - bounds.Left;
                            double ry = ryAbs - bounds.Top;

                            double rw = condition.RegionW * scaleX;
                            double rh = condition.RegionH * scaleY;

                            searchRoi = new System.Windows.Rect(rx, ry, rw, rh);
                            drawRoi = new OpenCvSharp.Rect((int)rx, (int)ry, (int)rw, (int)rh);
                        }
                        else if (winInfo.HasValue && parentGroup != null && parentGroup.CoordinateMode == CoordinateMode.WindowRelative)
                        {
                            // [Fallback] ROI 미지정 시, WindowRelative 모드라면 창 전체를 검색 영역으로 자동 설정
                            double rx = winInfo.Value.X - bounds.Left;
                            double ry = winInfo.Value.Y - bounds.Top;
                            double rw = winInfo.Value.Width;
                            double rh = winInfo.Value.Height;

                            searchRoi = new System.Windows.Rect(rx, ry, rw, rh);
                            drawRoi = new OpenCvSharp.Rect((int)rx, (int)ry, (int)rw, (int)rh);
                        }
                        
                        // [Path Resolve] 상대 경로 처리
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
                                // 결과 텍스트는 절대 좌표로 표시 (디버그 정보 포함)
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
                                } catch {}

                                // 마커 크기 스케일링 적용
                                int scaledW = (int)(tW * scaleX);
                                int scaledH = (int)(tH * scaleY);

                                // 중심 -> 좌상단 변환 (이미지 로컬 좌표 기준)
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

            this.WhenActivated(disposables =>
            {
                // 점프 이름 변경 시 ID 자동 동기화 관찰 제거 (이제 ID 직접 바인딩)
                // 하지만 이름 목록이 바뀔 때 JumpTargets를 갱신해야 하는 것은 여전함.
                // 이미 Groups.CollectionChanged에서 처리 중.
                
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

        private void UpdateJumpTargets()
        {
            if (_isLoading) return;

            // 기존 선택된 ID 저장 (선택 복원 시도)
            // 하지만 View 바인딩이 TwoWay라서 Clear() 시 null이 되어버리는 문제가 있으므로
            // 사실상 Clear()를 피하는 게 상책이나, ObservableCollection에서 부분 업데이트는 복잡함.
            // 일단 재생성하되, View에서 바인딩이 끊기지 않도록 하는 것은 NotifyTypeChanges에서 호출을 뺀 것으로 해결됨.
            
            JumpTargets.Clear();
            
            // 1. System Options
            JumpTargets.Add(new JumpTargetViewModel { Id = "(Next Step)", DisplayName = "(Next Step)", IsGroup = false });
            JumpTargets.Add(new JumpTargetViewModel { Id = "(Ignore & Continue)", DisplayName = "(Ignore & Continue)", IsGroup = false });
            JumpTargets.Add(new JumpTargetViewModel { Id = "(Stop Execution)", DisplayName = "(Stop Execution)", IsGroup = false });

            // 2. Groups and Items
            foreach (var group in Groups)
            {
                string groupTargetId = group.Items.Count > 0 ? group.Items[0].Id.ToString() : string.Empty;
                
                string groupIcon = group.IsStartGroup ? "🏁" : "📁";

                JumpTargets.Add(new JumpTargetViewModel 
                { 
                    Id = groupTargetId, 
                    DisplayName = $"{groupIcon} {group.Name}", 
                    IsGroup = true 
                });

                foreach(var item in group.Items)
                {
                    if (!string.IsNullOrEmpty(item.Name))
                    {
                        JumpTargets.Add(new JumpTargetViewModel 
                        { 
                            Id = item.Id.ToString(), 
                            DisplayName = $"   📄 {item.Name}", 
                            IsGroup = false 
                        });
                    }
                }
            }
        }

        private async Task RunSingleStepAsync(SequenceItem item)
        {
            if (item == null) return;

            // 실행 시 부모 그룹의 컨텍스트를 주입한 복사본(혹은 임시 수정본)을 사용해야 함.
            var parentGroup = FindParentGroup(item);
            if (parentGroup != null)
            {
                // SequenceItem의 Context 속성들을 Group 값으로 덮어씀 (메모리상의 객체만 수정)
                item.CoordinateMode = parentGroup.CoordinateMode;
                item.ContextSearchMethod = parentGroup.ContextSearchMethod;
                item.TargetProcessName = parentGroup.TargetProcessName;
                item.ContextWindowState = parentGroup.ContextWindowState;
                item.ProcessNotFoundJumpName = parentGroup.ProcessNotFoundJumpName;
                item.ProcessNotFoundJumpId = parentGroup.ProcessNotFoundJumpId;
                item.RefWindowWidth = parentGroup.RefWindowWidth;
                item.RefWindowHeight = parentGroup.RefWindowHeight;
            }

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
                                // 1. Try Loading as Group List (New Format)
                                var loadedGroups = JsonSerializer.Deserialize<List<SequenceGroup>>(json, options);
                                if (loadedGroups != null && loadedGroups.Count > 0)
                                {
                                    // 첫 번째 요소가 Start Group인지 확인 (JSON 필드 확인)
                                    // 주의: 이전 버전 파일에는 IsStartGroup 필드가 없으므로 false임.
                                    // 마이그레이션: 첫 번째 그룹 이름이 "START"이면 Start Group으로 간주? 
                                    // 아니면 무조건 맨 앞에 새로 추가? -> 새로 추가하는 게 안전함 (기존 로직 보존)
                                    
                                    // 만약 로드된 첫 번째 그룹이 이미 IsStartGroup=true라면 유지.
                                    foreach (var g in loadedGroups) Groups.Add(g);
                                }
                                else
                                {
                                    throw new Exception("Not a group list");
                                }
                            }
                            catch
                            {
                                // 2. Fallback: Try Loading as Flat List (Legacy Format)
                                try 
                                {
                                    var loadedItems = JsonSerializer.Deserialize<List<SequenceItem>>(json, options);
                                    if (loadedItems != null)
                                    {
                                        var defaultGroup = new SequenceGroup { Name = "Default Group" };
                                        
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
                                            defaultGroup.Items.Add(item);
                                        }
                                        Groups.Add(defaultGroup);
                                    }
                                }
                                catch
                                {
                                    // Load Failed
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load recipe: {ex.Message}");
                }

                // [Ensure Start Group]
                if (Groups.Count == 0 || !Groups[0].IsStartGroup)
                {
                    var startGroup = new SequenceGroup { Name = "START", IsStartGroup = true };
                    Groups.Insert(0, startGroup);
                }
                else if (Groups.Count > 0 && Groups[0].IsStartGroup)
                {
                     // 이름 강제 고정 (선택 사항)
                     Groups[0].Name = "START";
                }
                
                LoadVariables();
            }
            finally
            {
                _isLoading = false;
                UpdateJumpTargets();
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
                // [Path Resolve] 저장 전 절대 경로를 상대 경로로 변환
                var recipeDir = Path.GetDirectoryName(currentRecipe.FilePath);
                if (recipeDir != null)
                {
                    foreach (var group in Groups)
                    {
                        foreach (var item in group.Items)
                        {
                            ConvertPathToRelative(item.PreCondition, recipeDir);
                            ConvertPathToRelative(item.PostCondition, recipeDir);
                        }
                    }
                }

                // Save as Group List
                var json = JsonSerializer.Serialize(Groups, GetJsonOptions());
                await File.WriteAllTextAsync(currentRecipe.FilePath, json);
                
                // Save Variables Sidecar
                SaveVariables();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save recipe: {ex.Message}");
            }
        }

        private void ConvertPathToRelative(IMacroCondition? condition, string baseDir)
        {
            if (condition is ImageMatchCondition imgMatch)
            {
                if (!string.IsNullOrEmpty(imgMatch.ImagePath) && Path.IsPathRooted(imgMatch.ImagePath))
                {
                    // 파일이 레시피 폴더와 동일한 위치에 있는지 확인
                    var fileDir = Path.GetDirectoryName(imgMatch.ImagePath);
                    
                    // 주의: 경로 비교 시 정규화 필요할 수 있음 (대소문자 등)
                    // 여기서는 간단히 문자열 비교
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
                PropertyNameCaseInsensitive = true
            };
        }

        // --- Helper Methods ---

        private SequenceGroup? FindParentGroup(SequenceItem? item)
        {
            if (item == null) return null;
            return Groups.FirstOrDefault(g => g.Items.Contains(item));
        }

        private void NotifyTypeChanges()
        {
            this.RaisePropertyChanged(nameof(SelectedPreConditionType));
            this.RaisePropertyChanged(nameof(SelectedActionType));
            this.RaisePropertyChanged(nameof(SelectedPostConditionType));
            // JumpTarget 업데이트 제거 (선택 변경 시 목록 재생성 방지)
            // UpdateJumpTargets(); 
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
                MultiAction => "Multi Action",
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
                "Switch Case" => new SwitchCaseCondition(),
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
                "Switch Case" => new SwitchCaseCondition(),
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
            else if (type == "Multi Action" && !(SelectedSequence.Action is MultiAction))
            {
                SelectedSequence.Action = new MultiAction();
            }
            this.RaisePropertyChanged(nameof(SelectedActionType));
        }

        private void AddGroup()
        {
            var newGroup = new SequenceGroup { Name = $"Group {Groups.Count + 1}" };
            Groups.Add(newGroup);
            SelectedGroup = newGroup;
            SelectedSequence = null;
            // Groups 컬렉션이 변경되므로 생성자에서 구독한 핸들러에 의해 UpdateJumpTargets 호출됨
        }

        private void RemoveGroup()
        {
            if (SelectedGroup != null)
            {
                Groups.Remove(SelectedGroup);
                SelectedGroup = null;
                // Groups 컬렉션이 변경되므로 UpdateJumpTargets 호출됨
            }
        }

        private void AddSequence()
        {
            if (SelectedGroup == null) return;

            var newAction = new IdleAction();
            var newItem = new SequenceItem(newAction)
            {
                Name = $"Step {SelectedGroup.Items.Count + 1}",
                IsEnabled = true
            };

            SelectedGroup.Items.Add(newItem);
            SelectedSequence = newItem;
            UpdateJumpTargets(); // Items 변경 감지용
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
                        
                        condition.ImagePath = newFileName; // Relative Path 

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
            foreach (var group in Groups)
            {
                foreach (var seq in group.Items)
                {
                    if (IsPathMatch(seq.PreCondition, path, currentCondition)) return true;
                    if (IsPathMatch(seq.PostCondition, path, currentCondition)) return true;
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

        private void RemoveSequence()
        {
            if (SelectedSequence != null)
            {
                var parentGroup = FindParentGroup(SelectedSequence);
                if (parentGroup != null)
                {
                    var itemToRemove = SelectedSequence;
                    parentGroup.Items.Remove(itemToRemove);
                    
                    if (itemToRemove.PreCondition is ImageMatchCondition preImg) 
                        DeleteImageIfOrphaned(preImg.ImagePath);
                    if (itemToRemove.PostCondition is ImageMatchCondition postImg)
                        DeleteImageIfOrphaned(postImg.ImagePath);

                    SelectedSequence = null;
                    UpdateJumpTargets(); // Items 변경 감지용
                }
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

        private void MoveGroupUp(SequenceGroup group)
        {
            if (group.IsStartGroup) return;

            int index = Groups.IndexOf(group);
            // Index 1 (after Start Group) cannot move up to 0
            if (index > 1) Groups.Move(index, index - 1);
            // Move는 CollectionChanged 발생함
        }

        private void MoveGroupDown(SequenceGroup group)
        {
            if (group.IsStartGroup) return;

            int index = Groups.IndexOf(group);
            if (index < Groups.Count - 1) Groups.Move(index, index + 1);
            // Move는 CollectionChanged 발생함
        }

        private void MoveSequenceUp(SequenceItem item)
        {
            var parentGroup = FindParentGroup(item);
            if (parentGroup != null)
            {
                int index = parentGroup.Items.IndexOf(item);
                if (index > 0)
                {
                    parentGroup.Items.Move(index, index - 1);
                    UpdateJumpTargets(); // 순서 변경 반영
                }
            }
        }

        private void MoveSequenceDown(SequenceItem item)
        {
            var parentGroup = FindParentGroup(item);
            if (parentGroup != null)
            {
                int index = parentGroup.Items.IndexOf(item);
                if (index < parentGroup.Items.Count - 1)
                {
                    parentGroup.Items.Move(index, index + 1);
                    UpdateJumpTargets(); // 순서 변경 반영
                }
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
                    
                    // 1. ID Mapping Table 생성 (Old ID -> New ID)
                    var idMap = new Dictionary<string, string>();
                    
                    foreach (var item in newGroup.Items)
                    {
                        var oldId = item.Id.ToString();
                        item.ResetId(); // 새 ID 발급
                        var newId = item.Id.ToString();
                        
                        if (!idMap.ContainsKey(oldId))
                        {
                            idMap[oldId] = newId;
                        }
                    }

                    // 2. 내부 점프 참조 보정 (Smart Remapping)
                    // 그룹 내의 아이템끼리 연결된 점프만 새 ID로 교체하고, 외부 점프는 유지.
                    foreach (var item in newGroup.Items)
                    {
                        // Success Jump
                        if (!string.IsNullOrEmpty(item.SuccessJumpId) && idMap.ContainsKey(item.SuccessJumpId))
                        {
                            item.SuccessJumpId = idMap[item.SuccessJumpId];
                        }

                        // Components Fail Jump
                        UpdateComponentJumpId(item.PreCondition, idMap);
                        UpdateComponentJumpId(item.Action, idMap);
                        UpdateComponentJumpId(item.PostCondition, idMap);
                    }
                    
                    // 그룹 레벨의 점프 보정 (ProcessNotFoundJumpId)
                    if (!string.IsNullOrEmpty(newGroup.ProcessNotFoundJumpId) && idMap.ContainsKey(newGroup.ProcessNotFoundJumpId))
                    {
                        newGroup.ProcessNotFoundJumpId = idMap[newGroup.ProcessNotFoundJumpId];
                    }

                    Groups.Add(newGroup);
                    SelectedGroup = newGroup;
                    SelectedSequence = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Group Paste failed: {ex.Message}");
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
                    // ID 재생성 (중복 방지)
                    newItem.ResetId();
                    newItem.Name += " (Copy)";

                    if (SelectedSequence != null)
                    {
                        var parent = FindParentGroup(SelectedSequence);
                        if (parent == SelectedGroup)
                        {
                            int index = parent.Items.IndexOf(SelectedSequence);
                            if (index >= 0) parent.Items.Insert(index + 1, newItem);
                            else parent.Items.Add(newItem);
                        }
                        else
                        {
                            SelectedGroup.Items.Add(newItem);
                        }
                    }
                    else
                    {
                        SelectedGroup.Items.Add(newItem);
                    }
                    
                    SelectedSequence = newItem;
                    UpdateJumpTargets(); // 추가 반영
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Paste failed: {ex.Message}");
            }
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

        #endregion
    }

    public class JumpTargetViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsGroup { get; set; }
    }
}