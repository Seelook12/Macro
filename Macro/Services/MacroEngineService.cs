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
    }
}
