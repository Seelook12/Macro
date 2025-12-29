using ReactiveUI;
using Splat;
using Macro.ViewModels;
using Macro.Views;

namespace Macro
{
    public static class MainUiViewLocator
    {
        public static void Register()
        {
            // Splat의 서비스 로케이터에 View와 ViewModel 매핑 등록
            // ReactiveUI의 RoutedViewHost와 ViewModelViewHost는 Locator.Current를 참조하여 뷰를 찾습니다.
            
            Locator.CurrentMutable.Register(() => new DashboardView(), typeof(IViewFor<DashboardViewModel>));
            Locator.CurrentMutable.Register(() => new RecipeView(), typeof(IViewFor<RecipeViewModel>));
            Locator.CurrentMutable.Register(() => new TeachingView(), typeof(IViewFor<TeachingViewModel>));
        }
    }
}
