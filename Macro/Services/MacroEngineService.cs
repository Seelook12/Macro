using Macro.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace Macro.Services
{
    public class MacroEngineService : ReactiveObject
    {
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

            AddLog("=== 매크로 실행 시작 ===");

            try
            {
                // Run on background thread to keep UI responsive
                await Task.Run(async () =>
                {
                    int stepIndex = 1;
                    foreach (var item in sequences)
                    {
                        token.ThrowIfCancellationRequested();

                        if (!item.IsEnabled)
                        {
                            AddLog($"[Step {stepIndex}] '{item.Name}' 스킵 (비활성화됨)");
                            stepIndex++;
                            continue;
                        }

                        AddLog($"[Step {stepIndex}] '{item.Name}' 처리 시작");

                        // 1. PreCondition
                        if (item.PreCondition != null)
                        {
                            AddLog($"  - 조건 확인 중: {GetTypeName(item.PreCondition)}");
                            bool check = await item.PreCondition.CheckAsync();
                            if (!check)
                            {
                                throw new Exception($"PreCondition 실패: {item.Name}");
                            }
                        }

                        // 2. Action
                        token.ThrowIfCancellationRequested();
                        AddLog($"  - 동작 실행 중: {GetTypeName(item.Action)}");
                        await item.Action.ExecuteAsync();

                        // 3. PostCondition
                        if (item.PostCondition != null)
                        {
                            token.ThrowIfCancellationRequested();
                            AddLog($"  - 결과 확인 중: {GetTypeName(item.PostCondition)}");
                            bool check = await item.PostCondition.CheckAsync();
                            if (!check)
                            {
                                throw new Exception($"PostCondition 실패: {item.Name}");
                            }
                        }

                        // Short delay to prevent tight loops
                        await Task.Delay(100, token);
                        stepIndex++;
                    }
                }, token);

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
                _cts?.Dispose();
                _cts = null;
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

        private void AddLog(string message)
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
