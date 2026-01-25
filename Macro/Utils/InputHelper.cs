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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // Mouse Event Constants
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        // Keyboard Event Constants
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        // Window Show Constants
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private const uint MAPVK_VK_TO_VSC = 0x00;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static System.Collections.Generic.List<string> GetOpenWindows()
        {
            IntPtr shellWindow = GetShellWindow();
            var windows = new System.Collections.Generic.List<string>();

            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                System.Text.StringBuilder builder = new System.Text.StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                string title = builder.ToString();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    windows.Add(title);
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public static void TypeText(string text, int intervalMs, CancellationToken token)
        {
            foreach (char c in text)
            {
                if (token.IsCancellationRequested) break;

                short vk = VkKeyScan(c);
                if (vk == -1) continue; // 매핑되지 않는 문자

                byte virtualKey = (byte)(vk & 0xff);
                byte shiftState = (byte)((vk >> 8) & 0xff);

                // Shift가 필요한 경우 (대문자, 특수기호 등)
                // bit 0: Shift, bit 1: Ctrl, bit 2: Alt
                bool shiftPressed = (shiftState & 1) != 0;
                bool ctrlPressed = (shiftState & 2) != 0;
                bool altPressed = (shiftState & 4) != 0;

                if (shiftPressed) keybd_event(0x10, 0, 0, 0); // Shift Down
                if (ctrlPressed) keybd_event(0x11, 0, 0, 0);  // Ctrl Down
                if (altPressed) keybd_event(0x12, 0, 0, 0);   // Alt Down
                
                if (shiftPressed || ctrlPressed || altPressed) Thread.Sleep(10);

                PressKey(virtualKey);

                if (shiftPressed || ctrlPressed || altPressed) Thread.Sleep(10);

                if (altPressed) keybd_event(0x12, 0, KEYEVENTF_KEYUP, 0);   // Alt Up
                if (ctrlPressed) keybd_event(0x11, 0, KEYEVENTF_KEYUP, 0);  // Ctrl Up
                if (shiftPressed) keybd_event(0x10, 0, KEYEVENTF_KEYUP, 0); // Shift Up

                Thread.Sleep(intervalMs);
            }
        }

        public static IntPtr FindWindowByTitle(string titlePart)
        {
            IntPtr exactMatch = IntPtr.Zero;
            IntPtr partialMatch = IntPtr.Zero;
            IntPtr shellWindow = GetShellWindow();

            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                System.Text.StringBuilder builder = new System.Text.StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                string windowTitle = builder.ToString();

                // 1. 완전 일치 우선
                if (string.Equals(windowTitle, titlePart, StringComparison.OrdinalIgnoreCase))
                {
                    exactMatch = hWnd;
                    return false; // Stop enumeration immediately
                }

                // 2. 부분 일치 (후보군으로 저장)
                if (partialMatch == IntPtr.Zero && windowTitle.Contains(titlePart, StringComparison.OrdinalIgnoreCase))
                {
                    partialMatch = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            return exactMatch != IntPtr.Zero ? exactMatch : partialMatch;
        }

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

        public static void PressKey(byte virtualKey, int durationMs = 0)
        {
            // Scan Code 구하기 (일부 프로그램/게임 호환성 위해 필수)
            byte scanCode = (byte)MapVirtualKey(virtualKey, MAPVK_VK_TO_VSC);
            uint flags = 0;
            
            // 확장 키 처리 (화살표, 홈/엔드 등)
            if (IsExtendedKey(virtualKey))
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }

            keybd_event(virtualKey, scanCode, flags, 0);
            
            // 지정된 시간이 있으면 그만큼 대기, 없으면 랜덤 클릭
            if (durationMs > 0)
            {
                Thread.Sleep(durationMs);
            }
            else
            {
                Thread.Sleep(_random.Next(30, 70));
            }

            keybd_event(virtualKey, scanCode, flags | KEYEVENTF_KEYUP, 0);
        }

        private static bool IsExtendedKey(byte vk)
        {
            // Extended Keys: 
            // Insert(45), Delete(46), Home(36), End(35), PageUp(33), PageDown(34)
            // Left(37), Up(38), Right(39), Down(40)
            // NumLock(144), Snapshot(44), Divide(111)
            return (vk == 0x2D) || (vk == 0x2E) || (vk == 0x24) || (vk == 0x23) || (vk == 0x21) || (vk == 0x22) ||
                   (vk == 0x25) || (vk == 0x26) || (vk == 0x27) || (vk == 0x28) ||
                   (vk == 0x90) || (vk == 0x2C) || (vk == 0x6F);
        }
    }
}