using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Macro.Utils
{
    public static class ScreenCaptureHelper
    {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource GetScreenCapture()
        {
            // 1. DPI 배율 가져오기
            double scaleX = 1.0;
            double scaleY = 1.0;

            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                var source = System.Windows.PresentationSource.FromVisual(mainWindow);
                if (source != null && source.CompositionTarget != null)
                {
                    scaleX = source.CompositionTarget.TransformToDevice.M11;
                    scaleY = source.CompositionTarget.TransformToDevice.M22;
                }
            }

            // 2. 가상 스크린 영역 (DIP -> Pixel 변환)
            int screenLeft = (int)(SystemParameters.VirtualScreenLeft * scaleX);
            int screenTop = (int)(SystemParameters.VirtualScreenTop * scaleY);
            int screenWidth = (int)(SystemParameters.VirtualScreenWidth * scaleX);
            int screenHeight = (int)(SystemParameters.VirtualScreenHeight * scaleY);

            using (Bitmap bmp = new Bitmap(screenWidth, screenHeight))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    // 가상 스크린 전체를 물리 픽셀 단위로 캡처
                    g.CopyFromScreen(screenLeft, screenTop, 0, 0, bmp.Size);
                }

                // 3. Bitmap -> BitmapSource 변환 (DPI 정보 포함)
                IntPtr hBitmap = bmp.GetHbitmap();
                try
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    // 생성된 BitmapSource에 DPI 정보가 누락되었을 수 있으므로 명시적 생성 권장
                    // (단, CreateBitmapSourceFromHBitmap 결과가 충분하면 그대로 사용)
                    return bitmapSource;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }
    }
}
