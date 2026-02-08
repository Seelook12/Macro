using Macro.Models;
using Macro.Utils;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace Macro.Services
{
    public class MacroEngineService : ReactiveObject
    {
        // Custom Exception to carry jump info
        private class ComponentFailureException : Exception
        {
            public string FailJumpName { get; }
            public string FailJumpId { get; }
            public ComponentFailureException(string message, string failJumpName, string failJumpId) : base(message)
            {
                FailJumpName = failJumpName;
                FailJumpId = failJumpId;
            }
        }

        private class ForceJumpException : Exception
        {
            public string JumpId { get; }
            public ForceJumpException(string jumpId) : base("Force Jump")
            {
                JumpId = jumpId;
            }
        }

        // Singleton Implementation
        private static readonly Lazy<MacroEngineService> _instance =
            new Lazy<MacroEngineService>(() => new MacroEngineService());
        public static MacroEngineService Instance => _instance.Value;

        private bool _isRunning;
        private bool _isPaused;
        private CancellationTokenSource? _cts;

        private string _currentLogFilePath = string.Empty;
        private object _logLock = new object();

        // Constructor
        private MacroEngineService()
        {
            Logs = new ObservableCollection<string>();
            InitializeLogFile();
        }

        private void InitializeLogFile()
        {
            try
            {
                string logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!System.IO.Directory.Exists(logDir))
                {
                    System.IO.Directory.CreateDirectory(logDir);
                }

                string fileName = $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                _currentLogFilePath = System.IO.Path.Combine(logDir, fileName);
                
                // 파일 생성 및 헤더 작성
                System.IO.File.WriteAllText(_currentLogFilePath, $"=== Log Started at {DateTime.Now} ==={Environment.NewLine}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize log file: {ex.Message}");
            }
        }

        // Properties
        public bool IsRunning
        {
            get => _isRunning;
            private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set => this.RaiseAndSetIfChanged(ref _isPaused, value);
        }

        public ObservableCollection<string> Logs { get; }

        public ConcurrentDictionary<string, string> Variables { get; } = new ConcurrentDictionary<string, string>();

        private string _currentStepName = string.Empty;
        public string CurrentStepName
        {
            get => _currentStepName;
            private set => RxApp.MainThreadScheduler.Schedule(() => this.RaiseAndSetIfChanged(ref _currentStepName, value));
        }

        private int _currentStepIndex;
        public int CurrentStepIndex
        {
            get => _currentStepIndex;
            private set => RxApp.MainThreadScheduler.Schedule(() => this.RaiseAndSetIfChanged(ref _currentStepIndex, value));
        }

        private int _totalStepCount;
        public int TotalStepCount
        {
            get => _totalStepCount;
            private set => RxApp.MainThreadScheduler.Schedule(() => this.RaiseAndSetIfChanged(ref _totalStepCount, value));
        }

        public enum ExecutionPhase
        {
            None,
            PreCondition,
            Action,
            PostCondition
        }

        private ExecutionPhase _currentPhase = ExecutionPhase.None;
        public ExecutionPhase CurrentPhase
        {
            get => _currentPhase;
            private set => RxApp.MainThreadScheduler.Schedule(() => this.RaiseAndSetIfChanged(ref _currentPhase, value));
        }

        private SequenceItem? _currentSequenceItem;
        public SequenceItem? CurrentSequenceItem => _currentSequenceItem; // Expose for ViewModel

        // Methods
        public List<TimeoutStatus> GetActiveTimeouts()
        {
            var results = new List<TimeoutStatus>();
            if (_currentSequenceItem == null || !IsRunning) return results;

            // 1. Traverse PreCondition Wrappers (Group Timeouts)
            var current = _currentSequenceItem.PreCondition;
            while (current != null)
            {
                if (current is TimeoutCheckCondition timeout)
                {
                    if (Variables.TryGetValue(timeout.StartTimeVariableName, out var tickStr) && long.TryParse(tickStr, out var startTicks))
                    {
                        var elapsedMs = TimeSpan.FromTicks(DateTime.Now.Ticks - startTicks).TotalMilliseconds;
                        var remainingMs = timeout.TimeoutMs - elapsedMs;
                        var progress = Math.Clamp(elapsedMs / timeout.TimeoutMs, 0.0, 1.0);

                        results.Add(new TimeoutStatus
                        {
                            GroupName = timeout.GroupName,
                            StepName = _currentSequenceItem.Name,
                            TotalMs = timeout.TimeoutMs,
                            ElapsedMs = elapsedMs,
                            RemainingMs = remainingMs,
                            Progress = progress,
                            IsExpired = remainingMs <= 0
                        });
                    }
                    current = timeout.Inner;
                }
                else
                {
                    break;
                }
            }
            // Note: DelayCondition tracking removed from here as per user request.
            
            return results;
        }

        public async Task RunAsync(IEnumerable<SequenceItem> sequences)
        {
            if (IsRunning)
            {
                AddLog("이미 실행 중입니다.");
                return;
            }

            IsRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Variables.Clear();
            ImageSearchService.ClearCache(); // [Fix] Clear image cache before execution to ensure fresh template loading

            // Load Variable Defaults from .vars.json
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe != null && !string.IsNullOrEmpty(currentRecipe.FilePath))
            {
                var varsPath = System.IO.Path.ChangeExtension(currentRecipe.FilePath, ".vars.json");
                if (System.IO.File.Exists(varsPath))
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(varsPath);
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var defs = System.Text.Json.JsonSerializer.Deserialize<List<VariableDefinition>>(json, options);
                        if (defs != null)
                        {
                            foreach (var d in defs)
                            {
                                if (!string.IsNullOrEmpty(d.Name))
                                {
                                    Variables[d.Name] = d.DefaultValue ?? string.Empty;
                                }
                            }
                        }
                        AddLog($"변수 초기화 완료: {Variables.Count}개 로드됨.");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"변수 파일 로드 실패: {ex.Message}");
                    }
                }
            }

            AddLog("=== 매크로 실행 시작 ===");

            try
            {
                // Run on background thread to keep UI responsive
                await Task.Run(async () =>
                {
                    var sequenceList = new List<SequenceItem>(sequences);
                    int currentIndex = 0;
                    TotalStepCount = sequenceList.Count;

                    while (currentIndex < sequenceList.Count)
                    {
                        if (token.IsCancellationRequested) break;

                        // Paused Check
                        while (IsPaused)
                        {
                            if (token.IsCancellationRequested) break;
                            await Task.Delay(100, token);
                        }
                        if (token.IsCancellationRequested) break;

                        var item = sequenceList[currentIndex];
                        _currentSequenceItem = item;
                        int stepIndex = currentIndex + 1;
                        
                        CurrentStepIndex = stepIndex;
                        CurrentStepName = item.Name;

                        if (!item.IsEnabled)
                        {
                            AddLog($"[Step {stepIndex}] '{item.Name}' 스킵 (비활성화됨)");
                            currentIndex++;
                            continue;
                        }

                        AddLog($"[Step {stepIndex}] '{item.Name}' 처리 시작");

                        bool allRepeatsSuccess = true;
                        string? jumpTargetName = null;
                        string? jumpTargetId = null;

                        for (int repeat = 1; repeat <= item.RepeatCount; repeat++)
                        {
                            if (token.IsCancellationRequested) break;

                            // Paused Check (Inner Loop)
                            while (IsPaused)
                            {
                                if (token.IsCancellationRequested) break;
                                await Task.Delay(100, token);
                            }
                            if (token.IsCancellationRequested) break;

                            if (item.RepeatCount > 1)
                            {
                                AddLog($"  - 반복 실행 중 ({repeat}/{item.RepeatCount})");
                            }
                        
                            int retryAttempt = 0;
                            bool stepSuccess = false;
                            System.Windows.Point? foundPoint = null;

                            while (!stepSuccess)
                            {
                                if (token.IsCancellationRequested) break;

                                try
                                {
                                    // 0. Coordinate Setup
                                    if (item.CoordinateMode == CoordinateMode.WindowRelative)
                                    {
                                        try 
                                        {
                                            ConfigureRelativeCoordinates(item);
                                        }
                                        catch (Exception ex)
                                        {
                                             if (!string.IsNullOrEmpty(item.ProcessNotFoundJumpName))
                                             {
                                                 throw new ComponentFailureException($"Target Process Error: {ex.Message}", item.ProcessNotFoundJumpName, "");
                                             }
                                             throw;
                                        }
                                    }
                                    else
                                    {
                                        ResetCoordinates(item);
                                    }

                                    // 1. PreCondition
                                    if (item.PreCondition != null)
                                                            {
                                                                CurrentPhase = ExecutionPhase.PreCondition;
                                                                AddLog($"    - 조건 확인 중: {GetTypeName(item.PreCondition)} (시도 {retryAttempt + 1})");
                                                                bool check = await item.PreCondition.CheckAsync(token);
                                                                if (!check)
                                                                {
                                                                    throw new ComponentFailureException($"PreCondition 실패: {item.Name}", item.PreCondition.FailJumpName, item.PreCondition.FailJumpId);
                                                                }
                                                                foundPoint = item.PreCondition.FoundPoint;

                                                                // [Switch Case] 강제 분기 확인
                                                                var forceJumpId = item.PreCondition.GetForceJumpId();
                                                                if (forceJumpId.HasValue)
                                                                {
                                                                    throw new ForceJumpException(forceJumpId.Value.ToString());
                                                                }
                                                            }
                        
                                    // 2. Action 실행 전 데이터 주입 (GrayChangeCondition 등)
                                    if (item.PostCondition is GrayChangeCondition gcc)
                                    {
                                        // Dispatcher.Invoke 제거: 데드락 방지 및 성능 향상
                                        var captureData = await Task.Run(() => 
                                        {
                                            var bmp = ScreenCaptureHelper.GetScreenCapture();
                                            var bounds = ScreenCaptureHelper.GetScreenBounds();
                                            bmp?.Freeze();
                                            return new { Image = bmp, Bounds = bounds };
                                        });

                                        if (captureData?.Image != null)
                                        {
                                            // 해결책: GrayChangeCondition에 `UpdateReferenceValue(capture, bounds)` 메서드 추가.
                                            gcc.UpdateReferenceValue(captureData.Image, captureData.Bounds);
                                            
                                            AddLog($"    - [Gray] 기준값 측정 완료: {gcc.ReferenceValue:F2}");
                                        }
                                    }
                        
                                    // 2. Action 실행
                                    if (token.IsCancellationRequested) break;
                                    CurrentPhase = ExecutionPhase.Action;
                                    AddLog($"    - 동작 실행 중: {GetTypeName(item.Action)}");
                                    try
                                    {
                                        await item.Action.ExecuteAsync(token, foundPoint);
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new ComponentFailureException($"Action 실행 실패: {ex.Message}", item.Action.FailJumpName, item.Action.FailJumpId);
                                    }
                        
                                    // 3. PostCondition
                                    if (item.PostCondition != null)
                                    {
                                        if (token.IsCancellationRequested) break;
                                        CurrentPhase = ExecutionPhase.PostCondition;
                                        AddLog($"    - 결과 확인 중: {GetTypeName(item.PostCondition)}");
                                        bool check = await item.PostCondition.CheckAsync(token);
                                        if (!check)
                                        {
                                            throw new ComponentFailureException($"PostCondition 실패: {item.Name}", item.PostCondition.FailJumpName, item.PostCondition.FailJumpId);
                                        }

                                        // [Fix] PostCondition SwitchCase 강제 분기 확인
                                        var forceJumpId = item.PostCondition.GetForceJumpId();
                                        
                                        // [Debug] SwitchCase 로그
                                        if (item.PostCondition is SwitchCaseCondition sc)
                                        {
                                            if (Variables.TryGetValue(sc.TargetVariableName, out var valStr))
                                            {
                                                if (int.TryParse(valStr, out int valInt))
                                                {
                                                    AddLog($"    [SwitchCase] Var '{sc.TargetVariableName}'={valInt}. Checking {sc.Cases.Count} cases. JumpId: {(forceJumpId.HasValue ? forceJumpId.ToString() : "None")}");
                                                }
                                                else
                                                {
                                                    AddLog($"    [SwitchCase] Warning: Var '{sc.TargetVariableName}' value '{valStr}' is not an integer.");
                                                }
                                            }
                                            else
                                            {
                                                AddLog($"    [SwitchCase] Warning: Variable '{sc.TargetVariableName}' not found in runtime variables.");
                                            }
                                        }

                                        if (forceJumpId.HasValue)
                                        {
                                            throw new ForceJumpException(forceJumpId.Value.ToString());
                                        }
                                    }

                                    stepSuccess = true;
                                }
                                catch (ForceJumpException fjex)
                                {
                                    AddLog($"    -> 분기 조건 만족: {fjex.JumpId}");
                                    allRepeatsSuccess = false; // 성공으로 치지 않고 분기 처리
                                    jumpTargetId = fjex.JumpId;
                                    break; 
                                }
                                catch (ComponentFailureException cfex)
                                {
                                    if (retryAttempt < item.RetryCount)
                                    {
                                        retryAttempt++;
                                        AddLog($"    !!! 실패: {cfex.Message}. 재시도 중... ({retryAttempt}/{item.RetryCount})");
                                        await Task.Delay(item.RetryDelayMs, token);
                                    }
                                    else
                                    {
                                        AddLog($"    !!! [Step {stepIndex}] 최종 실패: {cfex.Message}");
                                        allRepeatsSuccess = false;
                                        jumpTargetName = cfex.FailJumpName;
                                        jumpTargetId = cfex.FailJumpId;
                                        break;
                                    }
                                }
                                catch (Exception ex) when (!(ex is OperationCanceledException))
                                {
                                    if (retryAttempt < item.RetryCount)
                                    {
                                        retryAttempt++;
                                        AddLog($"    !!! 예상치 못한 오류: {ex.Message}. 재시도 중... ({retryAttempt}/{item.RetryCount})");
                                        await Task.Delay(item.RetryDelayMs, token);
                                    }
                                    else
                                    {
                                        AddLog($"    !!! [Step {stepIndex}] 중단: {ex.Message}");
                                        throw;
                                    }
                                }
                            }

                            if (!allRepeatsSuccess || token.IsCancellationRequested) break;
                        }
                        if (token.IsCancellationRequested) break;

                        // 결과에 따른 흐름 제어
                        if (allRepeatsSuccess)
                        {
                            jumpTargetName = item.SuccessJumpName;
                            jumpTargetId = item.SuccessJumpId;

                            if ((string.IsNullOrEmpty(jumpTargetName) || jumpTargetName == "(Next Step)") && string.IsNullOrEmpty(jumpTargetId))
                            {
                                AddLog($"    -> 성공: 다음 스텝으로 진행");
                                currentIndex++;
                                jumpTargetName = null;
                                jumpTargetId = null;
                            }
                            else if (jumpTargetName == "(Stop Execution)" || jumpTargetId == "(Stop Execution)")
                            {
                                AddLog($"    -> 성공했으나 중지 설정됨: 실행을 중단합니다.");
                                break;
                            }
                        }
                        else
                        {
                            // jumpTargetName/Id는 이미 ComponentFailureException에서 가져옴
                            if ((string.IsNullOrEmpty(jumpTargetName) || jumpTargetName == "(Stop Execution)") && string.IsNullOrEmpty(jumpTargetId))
                            {
                                throw new Exception($"[Step {stepIndex}] 최종 실패로 인해 실행을 중단합니다.");
                            }
                            else if (jumpTargetName == "(Next Step)" || jumpTargetName == "(Ignore & Continue)" || jumpTargetId == "(Next Step)")
                            {
                                AddLog($"    -> 실패했으나 무시하고 다음 스텝으로 진행합니다.");
                                currentIndex++;
                                jumpTargetName = null;
                                jumpTargetId = null;
                            }
                        }

                        // Special Handling for Stop Execution
                        if (jumpTargetId == "(Stop Execution)" || jumpTargetName == "(Stop Execution)")
                        {
                            AddLog($"    -> 중지 설정됨: 실행을 중단합니다.");
                            break;
                        }

                        // ID 또는 이름 기반 점프 처리 (위에서 currentIndex가 변경되지 않은 경우)
                        if (!string.IsNullOrEmpty(jumpTargetId) || !string.IsNullOrEmpty(jumpTargetName))
                        {
                            int targetIndex = -1;
                            
                            // 1. ID로 먼저 검색
                            if (!string.IsNullOrEmpty(jumpTargetId))
                            {
                                targetIndex = sequenceList.FindIndex(s => s.Id.ToString() == jumpTargetId);
                            }

                            // 2. ID로 못 찾으면 이름으로 검색 (호환성)
                            if (targetIndex == -1 && !string.IsNullOrEmpty(jumpTargetName))
                            {
                                targetIndex = sequenceList.FindIndex(s => s.Name == jumpTargetName);
                            }

                            if (targetIndex != -1)
                            {
                                AddLog($"    -> 점프: '{sequenceList[targetIndex].Name}'(으)로 이동 (Index: {targetIndex + 1})");
                                currentIndex = targetIndex;
                            }
                            else
                            {
                                AddLog($"    !!! 경고: 점프 대상 '{jumpTargetId ?? jumpTargetName}'을(를) 찾을 수 없습니다. 다음 스텝으로 진행합니다.");
                                currentIndex++;
                            }
                        }

                        // Short delay to prevent tight loops
                        await Task.Delay(100, token);
                    }
                }, token);

                if (token.IsCancellationRequested)
                    AddLog("=== 매크로 실행 중지됨 (사용자 취소) ===");
                else
                    AddLog("=== 매크로 실행 완료 (성공) ===");
            }
            catch (OperationCanceledException)
            {
                AddLog("=== 매크로 실행 중지됨 (사용자 취소) ===");
            }
            catch (Exception ex)
            {
                AddLog($"!!! 에러 발생: {ex.Message}");
                AddLog("=== 매크로 실행 중단 (오류) ===");
            }
            finally
            {
                IsRunning = false;
                IsPaused = false;
                CurrentStepName = string.Empty;
                CurrentStepIndex = 0;
                TotalStepCount = 0;
                
                var oldCts = _cts;
                _cts = null;
                oldCts?.Dispose();
            }
        }

        public async Task RunSingleStepAsync(SequenceItem item)
        {
            if (IsRunning)
            {
                AddLog("전체 매크로가 실행 중입니다. 개별 실행 불가.");
                return;
            }

            AddLog($"[단일 실행] '{item.Name}'");

            try
            {
                await Task.Run(async () =>
                {
                    // 1. 좌표계 설정
                    if (item.CoordinateMode == CoordinateMode.WindowRelative)
                    {
                        ConfigureRelativeCoordinates(item);
                    }
                    else
                    {
                        ResetCoordinates(item);
                    }

                    // 2. PreCondition
                    System.Windows.Point? foundPoint = null;
                    if (item.PreCondition != null)
                    {
                        AddLog($"  - 조건 확인: {GetTypeName(item.PreCondition)}");
                        bool check = await item.PreCondition.CheckAsync(CancellationToken.None);
                        if (check)
                        {
                            foundPoint = item.PreCondition.FoundPoint;
                            AddLog("  -> 조건 만족");
                        }
                        else
                        {
                            AddLog("  -> 조건 불만족으로 인해 실행을 중단합니다.");
                            return;
                        }
                    }

                    // 3. Action 실행 전 데이터 주입 (GrayChangeCondition)
                    if (item.PostCondition is GrayChangeCondition gcc)
                    {
                        var captureData = await Task.Run(() => 
                        {
                            var bmp = ScreenCaptureHelper.GetScreenCapture();
                            var bounds = ScreenCaptureHelper.GetScreenBounds();
                            bmp?.Freeze();
                            return new { Image = bmp, Bounds = bounds };
                        });
                
                        if (captureData?.Image != null)
                        {
                            // GrayChangeCondition ReferenceValue 측정
                            gcc.UpdateReferenceValue(captureData.Image, captureData.Bounds);
                        }
                    }

                    // 4. Action
                    AddLog($"  - 동작 실행: {GetTypeName(item.Action)}");
                    await item.Action.ExecuteAsync(CancellationToken.None, foundPoint);

                    // 5. PostCondition
                    if (item.PostCondition != null)
                    {
                        AddLog($"  - 결과 확인: {GetTypeName(item.PostCondition)}");
                        bool check = await item.PostCondition.CheckAsync(CancellationToken.None);
                        AddLog(check ? "  -> 결과 성공" : "  -> 결과 실패");
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"!!! 단일 실행 오류: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                AddLog("중지 요청 확인... (정지 중)");
                _cts.Cancel();
            }
        }

        public void Pause()
        {
            if (IsRunning && !IsPaused)
            {
                IsPaused = true;
                AddLog("=== 일시정지 ===");
            }
        }

        public void Resume()
        {
            if (IsRunning && IsPaused)
            {
                IsPaused = false;
                AddLog("=== 재개 ===");
            }
        }

        public void AddLog(string message)
        {
            string timeStampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";

            // 1. File Logging
            if (!string.IsNullOrEmpty(_currentLogFilePath))
            {
                lock (_logLock)
                {
                    try
                    {
                        System.IO.File.AppendAllText(_currentLogFilePath, timeStampedMessage + Environment.NewLine);
                    }
                    catch { /* Ignore file errors to prevent app crash */ }
                }
            }

            // 2. UI Logging
            // Ensure UI update happens on the main thread
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                Logs.Add(timeStampedMessage);
                if (Logs.Count > 1000)
                {
                    Logs.RemoveAt(0);
                }
            });
        }

        public void UpdateVariable(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;

            // 1. Update Runtime Memory
            Variables[name] = value;

            // 2. Persist to File
            RecipeManager.Instance.UpdateVariableValue(name, value);
        }

        private string GetTypeName(object obj)
        {
            return obj.GetType().Name;
        }

        private void ResetCoordinates(SequenceItem item)
        {
            if (item.PreCondition is ISupportCoordinateTransform pre) pre.SetTransform(1.0, 1.0, 0, 0);
            if (item.Action is ISupportCoordinateTransform act) act.SetTransform(1.0, 1.0, 0, 0);
            if (item.PostCondition is ISupportCoordinateTransform post) post.SetTransform(1.0, 1.0, 0, 0);
        }

        private void ConfigureRelativeCoordinates(SequenceItem item)
        {
            // [Target Source Resolve]
            string targetName = string.Empty;
            if (item.TargetNameSource == ProcessNameSource.Variable)
            {
                if (!string.IsNullOrEmpty(item.TargetProcessNameVariable) && 
                    Variables.TryGetValue(item.TargetProcessNameVariable, out var val))
                {
                    targetName = val;
                }
            }
            else
            {
                targetName = item.TargetProcessName;
            }

            if (string.IsNullOrEmpty(targetName))
                throw new Exception("Target Name is empty (or variable not found).");

            IntPtr hWnd = IntPtr.Zero;

            if (item.ContextSearchMethod == WindowControlSearchMethod.ProcessName)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(targetName);
                if (processes.Length == 0)
                    throw new Exception($"Process '{targetName}' not found.");

                // Main Window가 있는 첫 번째 프로세스 찾기
                foreach (var p in processes)
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        hWnd = p.MainWindowHandle;
                        break;
                    }
                }
                
                if (hWnd == IntPtr.Zero)
                    throw new Exception($"Process '{targetName}' found but has no main window.");
            }
            else // WindowTitle
            {
                hWnd = InputHelper.FindWindowByTitle(targetName);
                if (hWnd == IntPtr.Zero)
                    throw new Exception($"Window with title containing '{targetName}' not found.");
            }

            AddLog($"[Debug] 타겟 윈도우 설정: '{targetName}' (hWnd: {hWnd})");

            // Apply Window State
            int nCmdShow = InputHelper.SW_RESTORE;
            bool needStateChange = false;

            switch (item.ContextWindowState)
            {
                case WindowControlState.Maximize:
                    if (!InputHelper.IsZoomed(hWnd))
                    {
                        nCmdShow = InputHelper.SW_SHOWMAXIMIZED;
                        needStateChange = true;
                    }
                    break;
                case WindowControlState.Minimize:
                    if (!InputHelper.IsIconic(hWnd))
                    {
                        nCmdShow = InputHelper.SW_SHOWMINIMIZED;
                        needStateChange = true;
                    }
                    break;
                case WindowControlState.Restore:
                    if (InputHelper.IsZoomed(hWnd) || InputHelper.IsIconic(hWnd))
                    {
                        nCmdShow = InputHelper.SW_RESTORE;
                        needStateChange = true;
                    }
                    break;
            }

            if (needStateChange)
            {
                AddLog($"[Debug] 윈도우 상태 변경 시도: {item.ContextWindowState}");
                InputHelper.ShowWindow(hWnd, nCmdShow);
                Thread.Sleep(500); // Wait for animation
            }

            if (item.ContextWindowState != WindowControlState.Minimize)
            {
                InputHelper.SetForegroundWindow(hWnd);
            }

            // Get Client Rect
            if (!InputHelper.GetClientRect(hWnd, out var clientRect))
                throw new Exception("Failed to get client rect.");

            // Get Client Origin in Screen Coords
            InputHelper.POINT pt = new InputHelper.POINT { X = 0, Y = 0 };
            InputHelper.ClientToScreen(hWnd, ref pt);

            double scaleX = 1.0;
            double scaleY = 1.0;

            if (item.RefWindowWidth > 0 && item.RefWindowHeight > 0)
            {
                if (clientRect.Width > 0 && clientRect.Height > 0)
                {
                    scaleX = (double)clientRect.Width / item.RefWindowWidth;
                    scaleY = (double)clientRect.Height / item.RefWindowHeight;
                }
            }
            else
            {
                AddLog($"[Warning] 기준 해상도(RefWindow)가 설정되지 않았습니다 (0x0). 스케일링이 적용되지 않습니다. 그룹 설정을 확인하세요.");
            }

            AddLog($"[Debug] 좌표 변환: ClientRect={clientRect.Width}x{clientRect.Height}, Ref={item.RefWindowWidth}x{item.RefWindowHeight}, Scale={scaleX:F4}x{scaleY:F4}, Offset={pt.X},{pt.Y}");

            // Apply to all components
            if (item.PreCondition is ISupportCoordinateTransform pre) 
            {
                pre.SetTransform(scaleX, scaleY, pt.X, pt.Y);
                
                // [Fix] Handle wrapped conditions (TimeoutCheckCondition) for SetContextSize
                var current = item.PreCondition;
                while (current != null)
                {
                    if (current is ImageMatchCondition img) { img.SetContextSize(clientRect.Width, clientRect.Height); break; }
                    if (current is TimeoutCheckCondition timeout) current = timeout.Inner;
                    else break;
                }
            }
            
            if (item.Action is ISupportCoordinateTransform act) 
            {
                act.SetTransform(scaleX, scaleY, pt.X, pt.Y);
            }
            
            if (item.PostCondition is ISupportCoordinateTransform post) 
            {
                post.SetTransform(scaleX, scaleY, pt.X, pt.Y);
                
                // [Fix] Handle wrapped conditions
                var current = item.PostCondition;
                while (current != null)
                {
                    if (current is ImageMatchCondition img) { img.SetContextSize(clientRect.Width, clientRect.Height); break; }
                    if (current is TimeoutCheckCondition timeout) current = timeout.Inner;
                    else break;
                }
            }
        }
    }
}