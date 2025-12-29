using System.Windows;
using ReactiveUI;
using Macro.ViewModels;
using Macro.Views;

namespace Macro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 참조 코드의 MainUiVIewLocator.Register() 패턴 적용
            MainUiViewLocator.Register();

            // 2. 메인 뷰모델 생성
            var mainViewModel = new MainWindowViewModel();

            // 2. 메인 윈도우 생성 및 뷰모델 연결

            // 3. 메인 윈도우 생성 및 뷰모델 연결
            var mainWindow = new MainWindow
            {
                ViewModel = mainViewModel,
                DataContext = mainViewModel // 바인딩이 확실히 동작하도록 DataContext를 명시적으로 설정
            };

            // 4. 윈도우 표시
            mainWindow.Show();
        }
    }
}