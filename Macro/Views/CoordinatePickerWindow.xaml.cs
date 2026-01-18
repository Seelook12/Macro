using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace Macro.Views
{
    public partial class CoordinatePickerWindow : Window
    {
        public Point? SelectedPoint { get; private set; }
        private readonly double _screenLeft;
        private readonly double _screenTop;

        public CoordinatePickerWindow(BitmapSource captureImage, double left, double top, double width, double height)
        {
            InitializeComponent();

            _screenLeft = left;
            _screenTop = top;

            // 창 위치와 크기를 물리적 스크린 영역에 맞춤
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;

            CaptureImage.Source = captureImage;
            
            this.KeyDown += CoordinatePickerWindow_KeyDown;
            this.MouseLeftButtonDown += CoordinatePickerWindow_MouseLeftButtonDown;
            
            this.Loaded += (s, e) => this.Focus();
        }

        private void CoordinatePickerWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
            var imgControl = CaptureImage;
            var source = imgControl.Source as BitmapSource;

            if (source != null)
            {
                // 1. 이미지 컨트롤 내에서의 클릭 위치 (WPF Logical Units)
                Point clickPoint = e.GetPosition(imgControl);

                // 2. 이미지 컨트롤의 실제 렌더링 크기 (WPF Logical Units)
                double actualW = imgControl.ActualWidth;
                double actualH = imgControl.ActualHeight;

                if (actualW > 0 && actualH > 0)
                {
                    // 3. 비트맵(물리 픽셀)과 컨트롤(논리 단위) 간의 비율 계산
                    double scaleX = source.PixelWidth / actualW;
                    double scaleY = source.PixelHeight / actualH;

                    // 4. 클릭 위치를 비트맵의 물리 픽셀 좌표로 변환
                    double pixelX = clickPoint.X * scaleX;
                    double pixelY = clickPoint.Y * scaleY;

                    // 5. 전체 가상 스크린(물리 좌표계) 기준 절대 좌표로 변환
                    // 이미지의 (0,0)은 _screenLeft, _screenTop에 해당함
                    SelectedPoint = new Point(_screenLeft + pixelX, _screenTop + pixelY);
                    
                    DialogResult = true;
                    Close();
                    return;
                }
            }

            // Fallback (실패 시 윈도우 기준 좌표라도 반환)
            var winPoint = e.GetPosition(this);
            SelectedPoint = new Point(_screenLeft + winPoint.X, _screenTop + winPoint.Y);
            DialogResult = true;
            Close();
        }
    }
}