using ReactiveUI;
using Macro.ViewModels;
using System.Reactive.Linq;
using System;

namespace Macro.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();

            // RoutedViewHost가 뷰를 찾을 수 있도록 커스텀 로케이터를 연결합니다.
            // 이 로케이터는 Splat을 먼저 찾아보고, 없으면 직접 생성합니다.
            RoutedViewHost.ViewLocator = new AppViewLocator();
        }
    }
}