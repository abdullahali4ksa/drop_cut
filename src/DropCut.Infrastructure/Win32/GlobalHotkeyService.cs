using System.Diagnostics;
using System.Runtime.InteropServices;
using DropCut.Core.Contracts;

namespace DropCut.Infrastructure.Win32;

/// <summary>
/// System-wide low-level keyboard hook service for background hotkeys (PrintScreen, Ctrl+Shift+S, Ctrl+B+B).
/// Operates globally across Windows even when DropCut is running in the background or minimized.
/// </summary>
public class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_SNAPSHOT = 0x2C;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_B = 0x42;
    private const int VK_S = 0x53;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hookId = IntPtr.Zero;
    private DateTime _lastBPressed = DateTime.MinValue;
    private bool _isDisposed;

    public event EventHandler? HotkeyPressed;

    public GlobalHotkeyService()
    {
        _hookProc = HookCallback;
    }

    public void StartListening()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        try
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var moduleHandle = GetModuleHandle(curModule?.ModuleName);
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);
        }
        catch
        {
            // Protected Zero-Crash event boundary
        }
    }

    public void StopListening()
    {
        if (_hookId != IntPtr.Zero)
        {
            try
            {
                UnhookWindowsHookEx(_hookId);
            }
            catch
            {
                // Protected Zero-Crash cleanup
            }
            finally
            {
                _hookId = IntPtr.Zero;
            }
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var msg = (int)wParam;
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    var vkCode = Marshal.ReadInt32(lParam);
                    var isCtrlDown = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
                    var isShiftDown = (GetKeyState(VK_SHIFT) & 0x8000) != 0;

                    // 1. Hardware PrintScreen key
                    if (vkCode == VK_SNAPSHOT)
                    {
                        RaiseHotkeyPressed();
                    }
                    // 2. Ctrl + Shift + S (standard Windows Snip hotkey)
                    else if (isCtrlDown && isShiftDown && vkCode == VK_S)
                    {
                        RaiseHotkeyPressed();
                    }
                    // 3. Ctrl + B + B (double-press 'B' with Ctrl held) or Ctrl + B
                    else if (isCtrlDown && vkCode == VK_B)
                    {
                        var now = DateTime.UtcNow;
                        if ((now - _lastBPressed).TotalMilliseconds <= 550)
                        {
                            _lastBPressed = DateTime.MinValue;
                            RaiseHotkeyPressed();
                        }
                        else
                        {
                            _lastBPressed = now;
                            // Also trigger immediately so user doesn't have to wait if single-press is preferred
                            RaiseHotkeyPressed();
                        }
                    }
                }
            }
            catch
            {
                // Protected Zero-Crash hook loop
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void RaiseHotkeyPressed()
    {
        try
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Protected Zero-Crash invocation boundary
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            StopListening();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
