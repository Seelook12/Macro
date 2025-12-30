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
        public ReactiveCommand<Unit, Unit> StopCommand { get; }

        // Properties
        public ObservableCollection<string> Logs => _engineService.Logs;

        private readonly ObservableAsPropertyHelper<bool> _isRunning;
        public bool IsRunning => _isRunning.Value;

        private readonly ObservableAsPropertyHelper<string> _statusMessage;
        public string StatusMessage => _statusMessage.Value;

        private readonly ObservableAsPropertyHelper<string> _currentStepName;
        public string CurrentStepName => _currentStepName.Value;

        private readonly ObservableAsPropertyHelper<int> _currentStepIndex;
        public int CurrentStepIndex => _currentStepIndex.Value;

        private readonly ObservableAsPropertyHelper<int> _totalStepCount;
        public int TotalStepCount => _totalStepCount.Value;


        public DashboardViewModel(IScreen screen)
        {
            HostScreen = screen;
            _engineService = MacroEngineService.Instance;

            // IsRunning 상태 동기화
            _isRunning = _engineService
                .WhenAnyValue(x => x.IsRunning)
                .ToProperty(this, x => x.IsRunning);

            // 상태 메시지 동기화
            _statusMessage = _engineService
                .WhenAnyValue(x => x.IsRunning)
                .Select(running => running ? "실행 중..." : "대기")
                .ToProperty(this, x => x.StatusMessage);

            _currentStepName = _engineService
                .WhenAnyValue(x => x.CurrentStepName)
                .ToProperty(this, x => x.CurrentStepName);

            _currentStepIndex = _engineService
                .WhenAnyValue(x => x.CurrentStepIndex)
                .ToProperty(this, x => x.CurrentStepIndex);

            _totalStepCount = _engineService
                .WhenAnyValue(x => x.TotalStepCount)
                .ToProperty(this, x => x.TotalStepCount);

            // RunCommand: 실행 중이 아닐 때만 가능
            var canRun = _engineService.WhenAnyValue(x => x.IsRunning).Select(running => !running);
            
            RunCommand = ReactiveCommand.CreateFromTask(async () =>
            {
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

                // 3. 파일 로드 및 파싱
                List<SequenceItem>? sequences = null;
                try
                {
                    var json = await File.ReadAllTextAsync(currentRecipe.FilePath);
                    if (string.IsNullOrWhiteSpace(json) || json == "{{}}")
                    {
                        System.Windows.MessageBox.Show("레시피 파일이 비어있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    sequences = JsonSerializer.Deserialize<List<SequenceItem>>(json, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"레시피 로드 중 오류가 발생했습니다.\n{{ex.Message}}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4. 실행
                if (sequences != null && sequences.Count > 0)
                {
                    await _engineService.RunAsync(sequences);
                }
                else
                {
                    System.Windows.MessageBox.Show("실행할 시퀀스가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                }

            }, canRun);

            // StopCommand: 실행 중일 때만 가능
            var canStop = _engineService.WhenAnyValue(x => x.IsRunning);
            
            StopCommand = ReactiveCommand.Create(() =>
            {
                _engineService.Stop();
            }, canStop);
        }
    }
}