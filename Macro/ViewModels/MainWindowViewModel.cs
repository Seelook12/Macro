using ReactiveUI;
using System.Reactive;

namespace Macro.ViewModels
{
    public class MainWindowViewModel : ReactiveObject, IScreen
    {
        // 화면 전환 상태를 관리하는 라우터
        public RoutingState Router { get; } = new RoutingState();

        // 각 화면(기능)의 ViewModel 인스턴스 (상태 보존을 위해 생성자에서 초기화)
        public DashboardViewModel DashboardVM { get; }
        public RecipeViewModel RecipeVM { get; }
        public TeachingViewModel TeachingVM { get; }

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
            // Router.Navigate.Execute(...)를 통해 화면 전환 수행
            GoDashboard = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(DashboardVM));
            GoRecipe = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(RecipeVM));
            GoTeaching = ReactiveCommand.CreateFromObservable(() => Router.Navigate.Execute(TeachingVM));

            // 앱 시작 시 대시보드 화면으로 이동
            Router.Navigate.Execute(DashboardVM);
        }
    }
}
