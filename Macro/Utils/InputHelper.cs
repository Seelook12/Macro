using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Macro.Utils
{
    public static class InputHelper
    {
        #region Win32 API Imports

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // Mouse Event Constants
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        // Keyboard Event Constants
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        #endregion

        /// <summary>
        /// 현재 마우스의 모니터 기준 절대 좌표를 반환합니다.
        /// </summary>
        public static (int X, int Y) GetMousePosition()
        {
            if (GetCursorPos(out POINT lpPoint))
            {
                return (lpPoint.X, lpPoint.Y);
            }
            return (0, 0);
        }

        /// <summary>
        /// 마우스를 지정된 좌표로 이동하고 클릭을 수행합니다. (Monitor Screen Coordinates)
        /// </summary>
        public static void MoveAndClick(int x, int y, string clickType)
        {
            // 1. Move Cursor (Absolute Screen Position)
            SetCursorPos(x, y);
            
            Thread.Sleep(50);

            // 2. Perform Click
            switch (clickType?.ToLower())
            {
                case "right":
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                    Thread.Sleep(20);
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    break;

                case "double":
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    Thread.Sleep(100);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;

                case "left":
                default:
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    Thread.Sleep(20);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
            }
        }

        public static void PressKey(byte virtualKey)
        {
            keybd_event(virtualKey, 0, 0, 0);
            Thread.Sleep(50);
            keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, 0);
        }
    }
}