using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace Macro.Views
{
    public partial class RegionPickerWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging;
        private readonly double _screenLeft;
        private readonly double _screenTop;

        public Rect SelectedRegion { get; private set; }

        public RegionPickerWindow(BitmapSource captureImage, double left, double top, double width, double height)
        {
            InitializeComponent();

            _screenLeft = left;
            _screenTop = top;

            // DPI Scale Calculation
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;
            
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                var source = System.Windows.PresentationSource.FromVisual(mainWindow);
                if (source?.CompositionTarget != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }
            }

            // Convert Physical to Logical
            this.Left = left / dpiScaleX;
            this.Top = top / dpiScaleY;
            this.Width = width / dpiScaleX;
            this.Height = height / dpiScaleY;

            CaptureImage.Source = captureImage;
            
            this.KeyDown += RegionPickerWindow_KeyDown;
            this.MouseLeftButtonDown += RegionPickerWindow_MouseLeftButtonDown;
            this.MouseMove += RegionPickerWindow_MouseMove;
            this.MouseLeftButtonUp += RegionPickerWindow_MouseLeftButtonUp;
            
            this.Loaded += (s, e) => this.Focus();
        }

        private void RegionPickerWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void RegionPickerWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 드래그 시작점 (윈도우 기준 논리 좌표)
            // 나중에 물리 픽셀로 변환하기 위해 이미지 컨트롤 기준 좌표도 필요할 수 있으나,
            // MouseMove 시각적 피드백을 위해 우선 this 기준 사용
            _startPoint = e.GetPosition(this);
            _isDragging = true;
            
            Canvas.SetLeft(SelectionRect, _startPoint.X); // Canvas는 윈도우 내부이므로 논리 좌표(0,0 기준) 사용
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;
        }

        private void RegionPickerWindow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging) return;

            var currentPoint = e.GetPosition(this);

            // 시각적 피드백 (윈도우 내부 논리 좌표)
            // 윈도우의 (0,0)은 _screenLeft, _screenTop에 해당하지만,
            // Canvas 내부 좌표계는 (0,0)부터 시작함.
            // e.GetPosition(this)는 (0,0) ~ (Width, Height) 범위를 반환하므로 그대로 사용 가능.
            
            // 주의: CoordinatePickerWindow에서는 Screen 좌표 변환을 위해 복잡한 식을 썼지만,
            // 여기서는 시각적 사각형 그리기를 위해 윈도우 로컬 좌표를 써야 함.

            // 아, _startPoint는 e.GetPosition(this)였음.
            // this.Left가 _screenLeft이므로, e.GetPosition(this)는 (0,0)이 좌상단임.
            // 맞음.

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(_startPoint.X - currentPoint.X);
            double h = Math.Abs(_startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = w;
            SelectionRect.Height = h;
        }

        private void RegionPickerWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;

            var endPoint = e.GetPosition(this);
            
            // 1. 윈도우 기준 논리 좌표 (Logical Rect)
            double lx = Math.Min(_startPoint.X, endPoint.X);
            double ly = Math.Min(_startPoint.Y, endPoint.Y);
            double lw = Math.Abs(_startPoint.X - endPoint.X);
            double lh = Math.Abs(_startPoint.Y - endPoint.Y);

            if (lw > 0 && lh > 0)
            {
                // 2. 물리 픽셀로 변환
                var imgControl = CaptureImage;
                var source = imgControl.Source as BitmapSource;
                
                if (source != null && imgControl.ActualWidth > 0 && imgControl.ActualHeight > 0)
                {
                    // 배율 계산
                    double scaleX = source.PixelWidth / imgControl.ActualWidth;
                    double scaleY = source.PixelHeight / imgControl.ActualHeight;

                    // 이미지 컨트롤 내부 좌표로 변환 (현재 Window 전체가 이미지이므로 Window좌표 == 이미지좌표)
                    // 정확히는 imgControl이 Window 전체를 채우고 있다고 가정 (Grid 등 여백 없어야 함)
                    // XAML 구조상 Grid 안에 Viewbox나 Image가 있을 수 있음.
                    // Image가 Stretch="Fill" 또는 크기가 Window와 같다면 OK.
                    // 안전하게 Image 컨트롤 기준으로 다시 계산.
                    
                    Point startOnImg = TranslatePoint(_startPoint, this, imgControl);
                    Point endOnImg = TranslatePoint(endPoint, this, imgControl);

                    double ix = Math.Min(startOnImg.X, endOnImg.X);
                    double iy = Math.Min(startOnImg.Y, endOnImg.Y);
                    double iw = Math.Abs(startOnImg.X - endOnImg.X);
                    double ih = Math.Abs(startOnImg.Y - endOnImg.Y);

                    // 물리 픽셀 변환
                    double px = ix * scaleX;
                    double py = iy * scaleY;
                    double pw = iw * scaleX;
                    double ph = ih * scaleY;

                    // 3. 스크린 절대 좌표로 오프셋 적용
                    SelectedRegion = new Rect(_screenLeft + px, _screenTop + py, pw, ph);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    // Fallback
                    SelectedRegion = new Rect(_screenLeft + lx, _screenTop + ly, lw, lh);
                    DialogResult = true;
                    Close();
                }
            }
            else
            {
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }

        private Point TranslatePoint(Point p, UIElement from, UIElement to)
        {
            return from.TranslatePoint(p, to);
        }
    }
}