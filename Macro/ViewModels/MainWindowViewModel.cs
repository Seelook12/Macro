using ReactiveUI;
using System.Reactive;
using Macro.Utils;
using System;

namespace Macro.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen
    {
        private string _currentRecipeName = "선택된 레시피 없음";

        // 화면 전환 상태를 관리하는 라우터
        public RoutingState Router { get; } = new RoutingState();

        // 각 화면(기능)의 ViewModel 인스턴스 (상태 보존을 위해 생성자에서 초기화)
        public DashboardViewModel DashboardVM { get; }
        public RecipeViewModel RecipeVM { get; }
        public TeachingViewModel TeachingVM { get; }

        public string CurrentRecipeName
        {
            get => _currentRecipeName;
            set => this.RaiseAndSetIfChanged(ref _currentRecipeName, value);
        }

        // 네비게이션 명령어
        public ReactiveCommand<Unit, IRoutableViewModel> GoDashboard { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> GoRecipe { get; }
        public ReactiveCommand<Unit, IRoutableViewModel> GoTeaching { get; }

        public MainWindowViewModel()
        {
            // 하위 ViewModel 초기화 (HostScreen으로 자신(this)을 전달)
            DashboardVM = new DashboardViewModel(this);
            RecipeVM = new RecipeViewModel(this);
            TeachingVM = new TeachingViewModel(this);

            // 네비게이션 커맨드 설정
            GoDashboard = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(DashboardVM));
            GoRecipe = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(RecipeVM));
            GoTeaching = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(TeachingVM));

            // RecipeManager 구독하여 현재 레시피 이름 업데이트
            RecipeManager.Instance.WhenAnyValue(x => x.CurrentRecipe)
                .Subscribe(recipe => 
                {
                    CurrentRecipeName = recipe != null ? $"현재 레시피: {recipe.FileName}" : "선택된 레시피 없음";
                });

            // 앱 시작 시 대시보드 화면으로 이동
            Router.Navigate.Execute(DashboardVM);
        }
    }
}
