namespace ObsidianDisplayGuard;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x4F44;
    private bool _registered;

    internal HotkeyWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "ObsidianDisplayGuardHotkey",
            Parent = new nint(-3)
        });

        _registered = NativeMethods.RegisterHotKey(
            Handle,
            HotkeyId,
            NativeMethods.ModControl |
            NativeMethods.ModAlt |
            NativeMethods.ModShift |
            NativeMethods.ModNoRepeat,
            NativeMethods.VkF12);
    }

    internal bool Registered => _registered;
    internal event Action? Pressed;

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmHotkey && message.WParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
        }

        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }

        DestroyHandle();
    }
}
