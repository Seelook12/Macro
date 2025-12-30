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
            // 주 모니터의 해상도 가져오기 (멀티 모니터 대응이 필요하면 System.Windows.Forms.Screen.AllScreens 사용 필요)
            // 여기서는 심플하게 PrimaryScreen 크기만큼 캡처
            int screenLeft = (int)SystemParameters.VirtualScreenLeft;
            int screenTop = (int)SystemParameters.VirtualScreenTop;
            int screenWidth = (int)SystemParameters.VirtualScreenWidth;
            int screenHeight = (int)SystemParameters.VirtualScreenHeight;

            using (Bitmap bmp = new Bitmap(screenWidth, screenHeight))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(screenLeft, screenTop, 0, 0, bmp.Size);
                }

                // Bitmap -> BitmapSource 변환
                IntPtr hBitmap = bmp.GetHbitmap();
                try
                {
                    return Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap); // 메모리 누수 방지
                }
            }
        }
    }
}
