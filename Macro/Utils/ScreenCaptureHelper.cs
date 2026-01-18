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

        public static (int Left, int Top, int Width, int Height) GetScreenBounds()
        {
            int minX = 0;
            int minY = 0;
            int maxX = 0;
            int maxY = 0;

            // System.Windows.Forms.Screen을 사용하여 전체 모니터 영역(물리 픽셀) 계산
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var b = screen.Bounds;
                if (b.Left < minX) minX = b.Left;
                if (b.Top < minY) minY = b.Top;
                if (b.Right > maxX) maxX = b.Right;
                if (b.Bottom > maxY) maxY = b.Bottom;
            }

            return (minX, minY, maxX - minX, maxY - minY);
        }

        public static BitmapSource GetScreenCapture()
        {
            var bounds = GetScreenBounds();

            if (bounds.Width <= 0 || bounds.Height <= 0) return null;

            using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    // 전체 가상 스크린 영역 캡처 (물리 픽셀 기준)
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size);
                }

                // 3. Bitmap -> BitmapSource 변환
                IntPtr hBitmap = bmp.GetHbitmap();
                try
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    bitmapSource.Freeze();
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
