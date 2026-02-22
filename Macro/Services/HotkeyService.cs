using System;
using System.Windows.Interop;
using Macro.Utils;

namespace Macro.Services
{
    public class HotkeyService
    {
        public const int HOTKEY_ID_START = 9001;
        public const int HOTKEY_ID_STOP = 9002;
        public const int HOTKEY_ID_PAUSE = 9003;

        private static readonly Lazy<HotkeyService> _instance = new Lazy<HotkeyService>(() => new HotkeyService());
        public static HotkeyService Instance => _instance.Value;

        private HwndSource? _hwndSource;
        private IntPtr _hWnd;

        public event Action<int>? HotkeyPressed;

        private HotkeyService() { }

        public void Init(IntPtr hWnd)
        {
            if (_hwndSource != null) return;

            _hWnd = hWnd;
            _hwndSource = HwndSource.FromHwnd(hWnd);
            _hwndSource.AddHook(HwndHook);
        }

        public bool RegisterHotkey(int id, uint modifiers, uint vk)
        {
            if (_hWnd == IntPtr.Zero) return false;
            return InputHelper.RegisterHotKey(_hWnd, id, modifiers, vk);
        }

        public void UnregisterHotkey(int id)
        {
            if (_hWnd == IntPtr.Zero) return;
            InputHelper.UnregisterHotKey(_hWnd, id);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                HotkeyPressed?.Invoke(id);
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}
