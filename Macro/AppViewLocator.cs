using ReactiveUI;
using Splat;
using Macro.ViewModels;
using Macro.Views;
using System;

namespace Macro
{
    // ReactiveUI의 RoutedViewHost가 뷰를 찾을 때 사용하는 로케이터
    public class AppViewLocator : IViewLocator
    {
        public IViewFor? ResolveView<T>(T? viewModel, string? contract = null)
        {
            if (viewModel == null) return null;

            // 1. 먼저 Splat 컨테이너(MainUiViewLocator에서 등록한 것)에서 찾아봅니다.
            var viewType = typeof(IViewFor<>).MakeGenericType(viewModel.GetType());
            var view = Locator.Current.GetService(viewType) as IViewFor;

            if (view != null)
            {
                return view;
            }

            // 2. 만약 Splat에서 못 찾았다면(설정 문제 등), 직접 매핑하여 반환합니다. (안전장치)
            return viewModel switch
            {
                DashboardViewModel => new DashboardView(),
                RecipeViewModel => new RecipeView(),
                TeachingViewModel => new TeachingView(),
                VariableManagerViewModel => new VariableManagerView(),
                _ => throw new ArgumentOutOfRangeException(nameof(viewModel), $"뷰를 찾을 수 없습니다: {viewModel.GetType().Name}")
            };
        }
    }
}