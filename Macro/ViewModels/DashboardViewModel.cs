using ReactiveUI;

namespace Macro.ViewModels
{
    public class DashboardViewModel : ReactiveObject, IRoutableViewModel
    {
        // URL 경로 세그먼트 (라우팅 식별자)
        public string UrlPathSegment => "Dashboard";

        // 호스트 스크린 (메인 윈도우)
        public IScreen HostScreen { get; }

        public DashboardViewModel(IScreen screen)
        {
            HostScreen = screen;
        }
    }
}
