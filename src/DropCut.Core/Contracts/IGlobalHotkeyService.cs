namespace DropCut.Core.Contracts;

/// <summary>
/// Service providing system-wide background keyboard shortcuts and snip triggers
/// that function even when the application is minimized or unfocused.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>
    /// Event fired when a registered global shortcut (PrintScreen, Ctrl+Shift+S, or Ctrl+B+B) is detected.
    /// </summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Starts the background global keyboard hook listener.
    /// </summary>
    void StartListening();

    /// <summary>
    /// Stops the global keyboard hook listener and frees Win32 hook handles.
    /// </summary>
    void StopListening();
}
