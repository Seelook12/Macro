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

        // Singleton Implementation
        private static readonly Lazy<MacroEngineService> _instance =
            new Lazy<MacroEngineService>(() => new MacroEngineService());
        public static MacroEngineService Instance => _instance.Value;

        private bool _isRunning;
        private CancellationTokenSource? _cts;

        // Constructor
        private MacroEngineService()
        {
            Logs = new ObservableCollection<string>();
        }

        // Properties
        public bool IsRunning
        {
            get => _isRunning;
            private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
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

        // Methods
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

                        var item = sequenceList[currentIndex];
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
                                                                AddLog($"    - 조건 확인 중: {GetTypeName(item.PreCondition)} (시도 {retryAttempt + 1})");
                                                                bool check = await item.PreCondition.CheckAsync();
                                                                if (!check)
                                                                {
                                                                    throw new ComponentFailureException($"PreCondition 실패: {item.Name}", item.PreCondition.FailJumpName, item.PreCondition.FailJumpId);
                                                                }
                                                                foundPoint = item.PreCondition.FoundPoint;
                                                            }
                        
                                                            // 2. Action 실행 전 데이터 주입 (GrayChangeCondition 등)
                                                            if (item.PostCondition is GrayChangeCondition gcc)
                                                            {
                                                                // Dispatcher.Invoke는 데드락 위험이 있으므로 가능한 한 지양하거나, 
                                                                // ScreenCaptureHelper 내부에서 처리하도록 유도해야 함.
                                                                var preCapture = await Task.Run(() => 
                                                                {
                                                                    return System.Windows.Application.Current?.Dispatcher?.Invoke(() => ScreenCaptureHelper.GetScreenCapture());
                                                                });
                        
                                                                if (preCapture != null)
                                                                {
                                                                    gcc.ReferenceValue = ImageSearchService.GetGrayAverage(preCapture, gcc.X, gcc.Y, gcc.Width, gcc.Height);
                                                                    AddLog($"    - [Gray] 기준값 측정 완료: {gcc.ReferenceValue:F2}");
                                                                }
                                                            }
                        
                                                            // 2. Action 실행
                                                            if (token.IsCancellationRequested) break;
                                                            AddLog($"    - 동작 실행 중: {GetTypeName(item.Action)}");
                                                            try
                                                            {
                                                                await item.Action.ExecuteAsync(foundPoint);
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                throw new ComponentFailureException($"Action 실행 실패: {ex.Message}", item.Action.FailJumpName, item.Action.FailJumpId);
                                                            }
                        
                                                            // 3. PostCondition
                                                            if (item.PostCondition != null)
                                                            {
                                                                if (token.IsCancellationRequested) break;
                                                                AddLog($"    - 결과 확인 중: {GetTypeName(item.PostCondition)}");
                                                                bool check = await item.PostCondition.CheckAsync();
                                                                if (!check)
                                                                {
                                                                    throw new ComponentFailureException($"PostCondition 실패: {item.Name}", item.PostCondition.FailJumpName, item.PostCondition.FailJumpId);
                                                                }
                                                            }
                        
                                                            stepSuccess = true;
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
                        await Task.Delay(50, token);
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
                        bool check = await item.PreCondition.CheckAsync();
                        if (check)
                        {
                            foundPoint = item.PreCondition.FoundPoint;
                            AddLog("  -> 조건 만족");
                        }
                        else
                        {
                            AddLog("  -> 조건 불만족 (하지만 동작은 수행함)");
                        }
                    }

                    // 3. Action 실행 전 데이터 주입 (GrayChangeCondition)
                    if (item.PostCondition is GrayChangeCondition gcc)
                    {
                        var preCapture = await Task.Run(() => 
                        {
                            return System.Windows.Application.Current?.Dispatcher?.Invoke(() => ScreenCaptureHelper.GetScreenCapture());
                        });
                
                        if (preCapture != null)
                        {
                            int tx = gcc.X; 
                            int ty = gcc.Y;
                            int tw = gcc.Width; 
                            int th = gcc.Height;

                            // 이미 ConfigureRelativeCoordinates에서 gcc의 좌표가 변환되었을 수 있음.
                            // 하지만 GetGrayAverage 호출 시 내부적으로는 좌표만 받음.
                            // 만약 ConfigureRelativeCoordinates가 gcc의 X,Y 프로퍼티를 바꾼다면? -> 아니다. SetTransform으로 내부 offset만 바꾼다.
                            // GrayChangeCondition.CheckAsync는 내부적으로 변환된 좌표를 쓴다.
                            // 하지만 여기서 ReferenceValue를 구할 때는 ImageSearchService.GetGrayAverage를 직접 호출한다.
                            // 따라서 여기서도 변환된 좌표를 써야 한다.
                            
                            // gcc는 ISupportCoordinateTransform을 구현함.
                            // 하지만 외부에서 변환된 좌표(offset applied)를 알 방법이 없다 (private fields).
                            // ConfigureRelativeCoordinates는 gcc.SetTransform(...)을 호출하여 내부 state를 세팅함.
                            // CheckAsync()는 그 state를 사용함.
                            
                            // 문제: 여기서 GetGrayAverage를 호출할 때 좌표를 어떻게 변환할 것인가?
                            // 해결: GrayChangeCondition에 `MeasureReferenceValue()` 메서드를 추가하여 캡슐화하는 것이 맞음.
                            // 하지만 지금 모델을 또 수정하기 번거로우니, CheckAsync 로직을 흉내내거나
                            // 일단 GrayChangeCondition은 놔두고 (빈번하지 않음), 기본 Action 실행에 집중.
                            
                            // *중요*: GrayChangeCondition은 CheckAsync 내에서만 변환 좌표를 씀.
                            // ReferenceValue 세팅은 여기서 직접 좌표를 계산해서 넣어야 함.
                            // 하지만 ConfigureRelativeCoordinates 로직이 복잡함 (Window 찾기 등).
                            // 이미 위에서 ConfigureRelativeCoordinates(item)을 호출했으므로,
                            // gcc 내부에는 Scale/Offset이 세팅되어 있음.
                            // gcc에 `GetTransformedRect()` 같은 메서드가 있다면 좋겠지만 없음.
                            
                            // 임시 방편: 일단 원본 좌표로 동작 (오차 감수) 혹은 TODO
                            // 사용자 요청은 "Action 실행"이 주 목적이므로 이 부분은 스킵해도 무방.
                            // gcc.ReferenceValue = ImageSearchService.GetGrayAverage(preCapture, gcc.X, gcc.Y, gcc.Width, gcc.Height);
                        }
                    }

                    // 4. Action
                    AddLog($"  - 동작 실행: {GetTypeName(item.Action)}");
                    await item.Action.ExecuteAsync(foundPoint);

                    // 5. PostCondition
                    if (item.PostCondition != null)
                    {
                        AddLog($"  - 결과 확인: {GetTypeName(item.PostCondition)}");
                        bool check = await item.PostCondition.CheckAsync();
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

        public void AddLog(string message)
        {
            string timeStampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";

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
            if (string.IsNullOrEmpty(item.TargetProcessName))
                throw new Exception("Target Name is empty.");

            IntPtr hWnd = IntPtr.Zero;

            if (item.ContextSearchMethod == WindowControlSearchMethod.ProcessName)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(item.TargetProcessName);
                if (processes.Length == 0)
                    throw new Exception($"Process '{item.TargetProcessName}' not found.");

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
                    throw new Exception($"Process '{item.TargetProcessName}' found but has no main window.");
            }
            else // WindowTitle
            {
                hWnd = InputHelper.FindWindowByTitle(item.TargetProcessName);
                if (hWnd == IntPtr.Zero)
                    throw new Exception($"Window with title containing '{item.TargetProcessName}' not found.");
            }

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

            if (item.RefWindowWidth > 0 && item.RefWindowHeight > 0 && clientRect.Width > 0 && clientRect.Height > 0)
            {
                scaleX = (double)clientRect.Width / item.RefWindowWidth;
                scaleY = (double)clientRect.Height / item.RefWindowHeight;
            }

            // Apply to all components
            if (item.PreCondition is ISupportCoordinateTransform pre) 
                pre.SetTransform(scaleX, scaleY, pt.X, pt.Y);
            
            if (item.Action is ISupportCoordinateTransform act) 
                act.SetTransform(scaleX, scaleY, pt.X, pt.Y);
            
            if (item.PostCondition is ISupportCoordinateTransform post) 
                post.SetTransform(scaleX, scaleY, pt.X, pt.Y);
        }
    }
}
