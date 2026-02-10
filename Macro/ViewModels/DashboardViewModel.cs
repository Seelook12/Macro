using Macro.Services;
using Macro.Utils;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;
using Macro.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Macro.ViewModels
{
    public class DashboardViewModel : ReactiveObject, IRoutableViewModel
    {
        // URL 경로 세그먼트 (라우팅 식별자)
        public string UrlPathSegment => "Dashboard";

        // 호스트 스크린 (메인 윈도우)
        public IScreen HostScreen { get; }

        // Services
        private readonly MacroEngineService _engineService;

        // Commands
        public ReactiveCommand<Unit, Unit> RunCommand { get; }
        public ReactiveCommand<Unit, Unit> PauseCommand { get; }
        public ReactiveCommand<Unit, Unit> StopCommand { get; }

        // Properties
        public ObservableCollection<string> Logs => _engineService.Logs;

        private readonly ObservableAsPropertyHelper<bool> _isRunning;
        public bool IsRunning => _isRunning.Value;

        private readonly ObservableAsPropertyHelper<bool> _isPaused;
        public bool IsPaused => _isPaused.Value;

        private readonly ObservableAsPropertyHelper<string> _statusMessage;
        public string StatusMessage => _statusMessage.Value;

        private readonly ObservableAsPropertyHelper<string> _currentStepName;
        public string CurrentStepName => _currentStepName.Value;

        private readonly ObservableAsPropertyHelper<int> _currentStepIndex;
        public int CurrentStepIndex => _currentStepIndex.Value;

        private readonly ObservableAsPropertyHelper<int> _totalStepCount;
        public int TotalStepCount => _totalStepCount.Value;

        private ObservableCollection<TimeoutStatus> _activeTimeouts = new ObservableCollection<TimeoutStatus>();
        public ObservableCollection<TimeoutStatus> ActiveTimeouts
        {
            get => _activeTimeouts;
            set => this.RaiseAndSetIfChanged(ref _activeTimeouts, value);
        }
        
        // Detailed Step Status Properties
        private string _preConditionStatus = "대기";
        public string PreConditionStatus
        {
            get => _preConditionStatus;
            set => this.RaiseAndSetIfChanged(ref _preConditionStatus, value);
        }

        private string _actionStatus = "대기";
        public string ActionStatus
        {
            get => _actionStatus;
            set => this.RaiseAndSetIfChanged(ref _actionStatus, value);
        }

        private string _postConditionStatus = "대기";
        public string PostConditionStatus
        {
            get => _postConditionStatus;
            set => this.RaiseAndSetIfChanged(ref _postConditionStatus, value);
        }

        public DashboardViewModel(IScreen screen)
        {
            HostScreen = screen;
            _engineService = MacroEngineService.Instance;

            // Timer for Active Timeouts & Step Status Update (100ms for smoother delay timer)
            Observable.Interval(TimeSpan.FromMilliseconds(100))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    if (IsRunning)
                    {
                        // 1. Update Timeouts
                        var statusList = _engineService.GetActiveTimeouts();
                        ActiveTimeouts = new ObservableCollection<TimeoutStatus>(statusList);

                        // 2. Update Step Status
                        UpdateStepStatus();
                    }
                    else
                    {
                        if (ActiveTimeouts.Count > 0) ActiveTimeouts.Clear();
                        PreConditionStatus = "대기";
                        ActionStatus = "대기";
                        PostConditionStatus = "대기";
                    }
                });

            // IsRunning 상태 동기화
            _isRunning = _engineService
                .WhenAnyValue(x => x.IsRunning)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsRunning);

            // IsPaused 상태 동기화
            _isPaused = _engineService
                .WhenAnyValue(x => x.IsPaused)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsPaused);

            // 상태 메시지 동기화
            _statusMessage = _engineService
                .WhenAnyValue(x => x.IsRunning, x => x.IsPaused)
                .Select(t => t.Item1 ? (t.Item2 ? "일시정지" : "실행 중...") : "대기")
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.StatusMessage);

            _currentStepName = _engineService
                .WhenAnyValue(x => x.CurrentStepName)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CurrentStepName);

            _currentStepIndex = _engineService
                .WhenAnyValue(x => x.CurrentStepIndex)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.CurrentStepIndex);

            _totalStepCount = _engineService
                .WhenAnyValue(x => x.TotalStepCount)
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.TotalStepCount);

            // RunCommand: 실행 중이 아닐 때 OR (실행 중 AND 일시정지 중)일 때 가능
            var canRun = _engineService.WhenAnyValue(x => x.IsRunning, x => x.IsPaused)
                .Select(t => !t.Item1 || t.Item2);
            
            RunCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                // 이미 실행 중이고 일시정지 상태라면 -> Resume
                if (_engineService.IsRunning && _engineService.IsPaused)
                {
                    _engineService.Resume();
                    return;
                }

                var currentRecipe = RecipeManager.Instance.CurrentRecipe;
                
                // 1. 레시피 선택 여부 확인
                if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath))
                {
                    System.Windows.MessageBox.Show("실행할 레시피가 선택되지 않았습니다.\n레시피 메뉴에서 레시피를 선택해주세요.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. 파일 존재 여부 확인
                if (!File.Exists(currentRecipe.FilePath))
                {
                    System.Windows.MessageBox.Show($"레시피 파일을 찾을 수 없습니다.\n경로: {currentRecipe.FilePath}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 3. 파일 로드 및 파싱 (그룹 지원)
                List<SequenceItem> finalSequence = new List<SequenceItem>();
                try
                {
                    var json = await File.ReadAllTextAsync(currentRecipe.FilePath);
                    if (string.IsNullOrWhiteSpace(json) || json == "{{}}")
                    {
                        System.Windows.MessageBox.Show("레시피 파일이 비어있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true
                    };

                    try
                    {
                        // A. Try Loading as Group List (New Format)
                        var loadedGroups = JsonSerializer.Deserialize<List<SequenceGroup>>(json, options);
                        
                        // Check if it's really a group list (check first item)
                        if (loadedGroups != null && loadedGroups.Count > 0)
                        {
                            // Use RecipeCompiler to flatten the group tree
                            finalSequence = RecipeCompiler.Instance.Compile(loadedGroups);
                        }
                        else
                        {
                            throw new Exception("Not a group list");
                        }
                    }
                    catch
                    {
                        // B. Fallback: Try Loading as Flat List (Legacy Format)
                        var loadedItems = JsonSerializer.Deserialize<List<SequenceItem>>(json, options);
                        if (loadedItems != null)
                        {
                            finalSequence.AddRange(loadedItems);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"레시피 로드 중 오류가 발생했습니다.\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4. 실행
                if (finalSequence.Count > 0)
                {
                    await _engineService.RunAsync(finalSequence);
                }
                else
                {
                    System.Windows.MessageBox.Show("실행할 시퀀스가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }

            }, canRun);

            // PauseCommand: 실행 중이고 일시정지가 아닐 때 가능
            var canPause = _engineService.WhenAnyValue(x => x.IsRunning, x => x.IsPaused)
                .Select(t => t.Item1 && !t.Item2);

            PauseCommand = ReactiveCommand.Create(() =>
            {
                _engineService.Pause();
            }, canPause);

            // StopCommand: 실행 중일 때만 가능
            var canStop = _engineService.WhenAnyValue(x => x.IsRunning);
            
            StopCommand = ReactiveCommand.Create(() =>
            {
                _engineService.Stop();
            }, canStop);
        }

        private void UpdateStepStatus()
        {
            var item = _engineService.CurrentSequenceItem;
            var phase = _engineService.CurrentPhase;

            if (item == null) return;

            PreConditionStatus = GetComponentStatus(item.PreCondition, phase, MacroEngineService.ExecutionPhase.PreCondition, "조건 확인");
            ActionStatus = GetComponentStatus(item.Action, phase, MacroEngineService.ExecutionPhase.Action, "동작 실행");
            PostConditionStatus = GetComponentStatus(item.PostCondition, phase, MacroEngineService.ExecutionPhase.PostCondition, "완료 확인");
        }

        private string GetComponentStatus(object? component, MacroEngineService.ExecutionPhase currentPhase, MacroEngineService.ExecutionPhase targetPhase, string defaultLabel)
        {
            if (component == null) return "-";

            // Determine execution state relative to current phase
            bool isPast = currentPhase > targetPhase;
            bool isCurrent = currentPhase == targetPhase;

            // Type Check
            string typeName = component.GetType().Name.Replace("Condition", "").Replace("Action", "");
            
            // Special Handling for Delay
            if (component is DelayCondition delay)
            {
                if (isCurrent && delay.StartTime.HasValue)
                {
                    var elapsed = (DateTime.Now - delay.StartTime.Value).TotalMilliseconds;
                    // Use RuntimeDelayMs if set (non-zero), otherwise fallback to DelayTimeMs
                    double totalMs = delay.RuntimeDelayMs > 0 ? delay.RuntimeDelayMs : delay.DelayTimeMs;
                    
                    var remaining = Math.Max(0, totalMs - elapsed);
                    return $"{typeName}: {elapsed/1000.0:F1}s / {totalMs/1000.0:F1}s";
                }
                else if (isPast)
                {
                     return $"{typeName}: 완료";
                }
                else
                {
                     return $"{typeName}: 대기";
                }
            }

            // General Handling
            if (isCurrent) return $"{typeName}: 진행 중...";
            if (isPast) return $"{typeName}: 완료";
            
            return $"{typeName}: 대기";
        }
    }
}