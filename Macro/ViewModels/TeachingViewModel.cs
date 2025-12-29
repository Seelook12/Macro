using ReactiveUI;

namespace Macro.ViewModels
{
    public class TeachingViewModel : ReactiveObject, IRoutableViewModel
    {
        public string UrlPathSegment => "Teaching";
        public IScreen HostScreen { get; }

        public TeachingViewModel(IScreen screen)
        {
            HostScreen = screen;
        }
    }
}