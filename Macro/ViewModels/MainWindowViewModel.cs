using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Macro.Utils;
using System;

namespace Macro.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen, IDisposable
    {
        private string _currentRecipeName = "선택된 레시피 없음";
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        // 화면 전환 상태를 관리하는 라우터
        public RoutingState Router { get; } = new RoutingState();

        // 각 화면(기능)의 ViewModel 인스턴스 (상태 보존을 위해 생성자에서 초기화)
        public DashboardViewModel DashboardVM { get; }
        public RecipeViewModel RecipeVM { get; }
        public TeachingViewModel TeachingVM { get; }
        public VariableManagerViewModel VariableManagerVM { get; }

        public string CurrentRecipeName
        {
            get => _currentRecipeName;
            set => this.RaiseAndSetIfChanged(ref _currentRecipeName, value);
        }

        // 네비게이션 명령어
        public ReactiveCommand<Unit, IRoutableViewModel> GoDashboard { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> GoRecipe { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> GoTeaching { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> GoVariableManager { get; }

        public MainWindowViewModel()
        {
            // 하위 ViewModel 초기화 (HostScreen으로 자신(this)을 전달)
            DashboardVM = new DashboardViewModel(this);
            RecipeVM = new RecipeViewModel(this);
            TeachingVM = new TeachingViewModel(this);
            // TeachingVM의 변수 컬렉션을 공유하여 데이터 동기화
            VariableManagerVM = new VariableManagerViewModel(this, TeachingVM.DefinedVariables, TeachingVM);

            // 네비게이션 커맨드 설정
            GoDashboard = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(DashboardVM));
            GoRecipe = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(RecipeVM));
            GoTeaching = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(TeachingVM));
            GoVariableManager = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(VariableManagerVM));

            foreach (var cmd in new IHandleObservableErrors[] { GoDashboard, GoRecipe, GoTeaching, GoVariableManager })
            {
                cmd.ThrownExceptions.Subscribe(ex =>
                    System.Diagnostics.Debug.WriteLine($"[Navigation Error] {ex.Message}"));
            }

            // RecipeManager 구독하여 현재 레시피 이름 업데이트
            RecipeManager.Instance.WhenAnyValue(x => x.CurrentRecipe)
                .Subscribe(recipe =>
                {
                    CurrentRecipeName = recipe != null ? $"현재 레시피: {recipe.FileName}" : "선택된 레시피 없음";
                })
                .DisposeWith(_disposables);

            // 앱 시작 시 대시보드 화면으로 이동
            Router.Navigate.Execute(DashboardVM);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        #region Hotkey Settings

        public void SetupHotkeys()
        {
            var hotkey = Macro.Services.HotkeyService.Instance;

            // F5: Start (VK_F5 = 0x74)
            hotkey.RegisterHotkey(Macro.Services.HotkeyService.HOTKEY_ID_START, 0, 0x74);

            // F6: Stop (VK_F6 = 0x75)
            hotkey.RegisterHotkey(Macro.Services.HotkeyService.HOTKEY_ID_STOP, 0, 0x75);

            // F7: Pause (VK_F7 = 0x76)
            hotkey.RegisterHotkey(Macro.Services.HotkeyService.HOTKEY_ID_PAUSE, 0, 0x76);

            hotkey.HotkeyPressed += id =>
            {
                if (id == Macro.Services.HotkeyService.HOTKEY_ID_START) // F5 (Start / Resume)
                {
                    DashboardVM.RunCommand.Execute().Subscribe();
                }
                else if (id == Macro.Services.HotkeyService.HOTKEY_ID_STOP) // F6 (Stop)
                {
                    DashboardVM.StopCommand.Execute().Subscribe();
                }
                else if (id == Macro.Services.HotkeyService.HOTKEY_ID_PAUSE) // F7 (Pause)
                {
                    DashboardVM.PauseCommand.Execute().Subscribe();
                }
            };
        }

        #endregion
    }
}
