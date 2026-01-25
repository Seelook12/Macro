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
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.IsRunning);

            // 상태 메시지 동기화
            _statusMessage = _engineService
                .WhenAnyValue(x => x.IsRunning)
                .Select(running => running ? "실행 중..." : "대기")
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
                            foreach (var group in loadedGroups)
                            {
                                FlattenNodeRecursive(group, finalSequence, null, new Dictionary<string, System.Windows.Point>());
                            }
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

            // StopCommand: 실행 중일 때만 가능
            var canStop = _engineService.WhenAnyValue(x => x.IsRunning);
            
            StopCommand = ReactiveCommand.Create(() =>
            {
                _engineService.Stop();
            }, canStop);
        }

        private void FlattenNodeRecursive(ISequenceTreeNode node, List<SequenceItem> result, SequenceGroup? parentGroupContext, Dictionary<string, System.Windows.Point> scopeVariables)
        {
            if (node is SequenceGroup group)
            {
                // [Scope Management]
                // Create a new scope for this group, inheriting from parent scope
                var currentScope = new Dictionary<string, System.Windows.Point>(scopeVariables ?? new Dictionary<string, System.Windows.Point>());
                
                // Merge current group's variables (overwrite parent's if name collides)
                if (group.Variables != null)
                {
                    foreach (var v in group.Variables)
                    {
                        currentScope[v.Name] = new System.Windows.Point(v.X, v.Y);
                    }
                }

                // [Int Variable Injection]
                // Initialize Group Int Variables into Global Variable Context
                // Note: Since VariableSetAction uses global context, we pre-load them here.
                // Warning: Last-write wins if names collide across parallel branches, but for sequential flattening it's okay.
                if (group.IntVariables != null)
                {
                    foreach (var iv in group.IntVariables)
                    {
                        // Use UpdateVariable to ensure persistence and runtime update
                        _engineService.UpdateVariable(iv.Name, iv.Value.ToString());
                    }
                }

                // [Inheritance Logic]
                if (group.CoordinateMode == CoordinateMode.ParentRelative && parentGroupContext != null)
                {
                    group.CoordinateMode = parentGroupContext.CoordinateMode; // Resolve to actual mode (Global/WindowRelative)
                    group.ContextSearchMethod = parentGroupContext.ContextSearchMethod;
                    group.TargetProcessName = parentGroupContext.TargetProcessName;
                    group.ContextWindowState = parentGroupContext.ContextWindowState;
                    group.ProcessNotFoundJumpName = parentGroupContext.ProcessNotFoundJumpName;
                    group.ProcessNotFoundJumpId = parentGroupContext.ProcessNotFoundJumpId;
                    group.RefWindowWidth = parentGroupContext.RefWindowWidth;
                    group.RefWindowHeight = parentGroupContext.RefWindowHeight;
                }
                
                // _engineService.AddLog($"[Debug] Flatten Group: {group.Name}, Mode: {group.CoordinateMode}, RefW: {group.RefWindowWidth}");
                _engineService.AddLog($"[Debug] Flatten Group: {group.Name}, Mode: {group.CoordinateMode}, RefW: {group.RefWindowWidth}");

                if (group.IsStartGroup)
                {
                    if (!string.IsNullOrEmpty(group.StartJumpId))
                    {
                        var initItem = new SequenceItem(new IdleAction { DelayTimeMs = 0 })
                        {
                            Name = "Initialize (Start)",
                            SuccessJumpId = group.StartJumpId
                        };
                        result.Add(initItem);
                    }
                    return;
                }

                foreach (var child in group.Nodes)
                {
                    FlattenNodeRecursive(child, result, group, currentScope);
                }
            }
            else if (node is SequenceItem item)
            {
                if (parentGroupContext != null)
                {
                    item.Name = $"{parentGroupContext.Name}_{item.Name}";
                    
                    item.CoordinateMode = parentGroupContext.CoordinateMode;
                    item.ContextSearchMethod = parentGroupContext.ContextSearchMethod;
                    item.TargetProcessName = parentGroupContext.TargetProcessName;
                    item.ContextWindowState = parentGroupContext.ContextWindowState;
                    item.ProcessNotFoundJumpName = parentGroupContext.ProcessNotFoundJumpName;
                    item.ProcessNotFoundJumpId = parentGroupContext.ProcessNotFoundJumpId;
                    item.RefWindowWidth = parentGroupContext.RefWindowWidth;
                    item.RefWindowHeight = parentGroupContext.RefWindowHeight;

                    // [Group Post-Condition Injection]
                    // If this is the End step of a group, and the group has a Post-Condition,
                    // we attach the group's condition to this End step.
                    if (item.IsGroupEnd && parentGroupContext.PostCondition != null)
                    {
                        item.PostCondition = parentGroupContext.PostCondition;
                    }
                }
                
                // _engineService.AddLog($"[Debug] Flatten Item: {item.Name}, RefW: {item.RefWindowWidth}");
                _engineService.AddLog($"[Debug] Flatten Item: {item.Name}, RefW: {item.RefWindowWidth}");

                // [Variable Injection]
                if (item.Action is MouseClickAction mouseAction)
                {
                    mouseAction.RuntimeContextVariables = new Dictionary<string, System.Windows.Point>(scopeVariables);
                }

                result.Add(item);
            }
        }
    }
}