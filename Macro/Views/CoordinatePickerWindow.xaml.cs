using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point; // WPF Point 명시

namespace Macro.Views
{
    public partial class CoordinatePickerWindow : Window
    {
        public Point? SelectedPoint { get; private set; }

        public CoordinatePickerWindow(BitmapSource captureImage)
        {
            InitializeComponent();

            // 가상 스크린 전체 영역으로 창 설정 (다중 모니터 대응)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            CaptureImage.Source = captureImage;
            
            this.KeyDown += CoordinatePickerWindow_KeyDown;
            this.MouseLeftButtonDown += CoordinatePickerWindow_MouseLeftButtonDown;
            
            // 로드 시 포커스
            this.Loaded += (s, e) => this.Focus();
        }

        private void CoordinatePickerWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) // 명시적 사용
        {
            if (e.Key == Key.Escape)
            {
                SelectedPoint = null;
                DialogResult = false;
                Close();
            }
        }

        private void CoordinatePickerWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 이미지 기준 좌표가 아니라 화면(Window) 기준 좌표를 가져옴
            SelectedPoint = e.GetPosition(this);
            DialogResult = true;
            Close();
        }
    }
}