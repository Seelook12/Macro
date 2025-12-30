using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point; // WPF Point 명시

namespace Macro.Views
{
    public partial class RegionPickerWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging;

        public Rect SelectedRegion { get; private set; }

        public RegionPickerWindow(BitmapSource captureImage)
        {
            InitializeComponent();

            // 가상 스크린 전체 영역으로 창 설정 (다중 모니터 대응)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            CaptureImage.Source = captureImage;
            
            this.KeyDown += RegionPickerWindow_KeyDown;
            this.MouseLeftButtonDown += RegionPickerWindow_MouseLeftButtonDown;
            this.MouseMove += RegionPickerWindow_MouseMove;
            this.MouseLeftButtonUp += RegionPickerWindow_MouseLeftButtonUp;
            
            this.Loaded += (s, e) => this.Focus();
        }

        private void RegionPickerWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) // 명시적 사용
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void RegionPickerWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(this);
            _isDragging = true;
            
            // 사각형 초기화
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;
        }

        private void RegionPickerWindow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) // 명시적 사용
        {
            if (!_isDragging) return;

            var currentPoint = e.GetPosition(this);

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
            
            // 최종 영역 계산
            double x = Math.Min(_startPoint.X, endPoint.X);
            double y = Math.Min(_startPoint.Y, endPoint.Y);
            double w = Math.Abs(_startPoint.X - endPoint.X);
            double h = Math.Abs(_startPoint.Y - endPoint.Y);

            if (w > 0 && h > 0)
            {
                SelectedRegion = new Rect(x, y, w, h);
                DialogResult = true;
                Close();
            }
            else
            {
                // 너무 작으면 취소 혹은 무시
                SelectionRect.Visibility = Visibility.Collapsed;
            }
        }
    }
}