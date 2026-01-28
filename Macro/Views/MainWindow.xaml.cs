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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // 윈도우 핸들(HWND)을 가져와 HotkeyService 초기화
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Macro.Services.HotkeyService.Instance.Init(handle);

            // 단축키 설정 호출
            if (ViewModel is MainWindowViewModel vm)
            {
                vm.SetupHotkeys();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 등록된 단축키 해제
            Macro.Services.HotkeyService.Instance.UnregisterHotkey(9001);
            Macro.Services.HotkeyService.Instance.UnregisterHotkey(9002);
            Macro.Services.HotkeyService.Instance.UnregisterHotkey(9003);

            base.OnClosed(e);
        }
    }
}