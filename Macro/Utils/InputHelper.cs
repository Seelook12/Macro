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

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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

        private static readonly Random _random = new Random();

        /// <summary>
        /// 마우스를 부드럽게 이동시키고 랜덤한 지연을 섞어 클릭을 수행합니다.
        /// </summary>
        public static void MoveAndClick(int x, int y, string clickType)
        {
            // 1. 목표 좌표에 아주 미세한 랜덤 오차 추가 (±1 픽셀)
            int targetX = x + _random.Next(-1, 2);
            int targetY = y + _random.Next(-1, 2);

            // 2. 현재 위치 가져오기
            GetCursorPos(out POINT startPoint);

            // 3. 부드러운 이동 (Smooth Move)
            SmoothMove(startPoint.X, startPoint.Y, targetX, targetY);
            
            // 이동 후 잠시 멈춤 (사람이 목표를 확인하는 짧은 시간)
            Thread.Sleep(_random.Next(50, 150));

            // 4. 클릭 수행 (랜덤한 클릭 유지 시간 적용)
            switch (clickType?.ToLower())
            {
                case "right":
                    PerformClick(MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP);
                    break;

                case "double":
                    PerformClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP);
                    Thread.Sleep(_random.Next(100, 200)); // 더블 클릭 사이의 랜덤 간격
                    PerformClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP);
                    break;

                case "left":
                default:
                    PerformClick(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP);
                    break;
            }
        }

        private static void SmoothMove(int startX, int startY, int endX, int endY)
        {
            int steps = _random.Next(10, 20); // 이동 단계 수
            for (int i = 1; i <= steps; i++)
            {
                // 선형 보간에 약간의 랜덤성을 섞어 곡선 느낌 유도
                double t = (double)i / steps;
                // 베지어 곡선과 유사한 느낌을 위해 가속/감속 적용 (Sine easing)
                t = Math.Sin(t * Math.PI / 2); 

                int curX = (int)(startX + (endX - startX) * t);
                int curY = (int)(startY + (endY - startY) * t);

                SetCursorPos(curX, curY);
                Thread.Sleep(_random.Next(5, 15)); // 각 단계별 미세 대기
            }
            // 최종 위치 보정
            SetCursorPos(endX, endY);
        }

        private static void PerformClick(uint downFlag, uint upFlag)
        {
            mouse_event(downFlag, 0, 0, 0, 0);
            Thread.Sleep(_random.Next(30, 80)); // 버튼을 누르고 있는 시간 (랜덤)
            mouse_event(upFlag, 0, 0, 0, 0);
        }

        public static void PressKey(byte virtualKey)
        {
            keybd_event(virtualKey, 0, 0, 0);
            Thread.Sleep(_random.Next(30, 70)); // 키를 누르고 있는 시간 (랜덤)
            keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, 0);
        }
    }
}