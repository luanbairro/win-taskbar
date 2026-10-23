using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinTaskbar.Native
{
    public static class GlobalHookHelper
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;

        public const int VK_CAPITAL = 0x14;
        public const int VK_NUMLOCK = 0x90;
        public const int VK_VOLUME_MUTE = 0xAD;
        public const int VK_VOLUME_DOWN = 0xAE;
        public const int VK_VOLUME_UP = 0xAF;

        public delegate void KeyEventHandler(int vkCode, bool isDown);
        public static event KeyEventHandler? KeyIntercepted;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc? _proc;
        private static IntPtr _hookId = IntPtr.Zero;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        public static void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule?.ModuleName), 0);
        }

        public static void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    KeyIntercepted?.Invoke(vkCode, true);
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
