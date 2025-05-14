using Gma.System.MouseKeyHook;
using System.Text;
using System.Windows.Forms;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
using Microsoft.Extensions.Logging;
using LlmEmbeddingsCpu.Core.Enums;

namespace LlmEmbeddingsCpu.Services.KeyboardMonitor;

public sealed class KeyboardMonitorService(
    KeyboardInputStorageService keyboardInputStorageService,
    ILogger<KeyboardMonitorService> logger)
    : IDisposable
{
    private const int MaxCharsInBuffer = 1_000;  // < 512 tokens

    private IKeyboardEvents? _hook;
    private readonly StringBuilder _buffer = new();
    private readonly KeyboardInputStorageService _storage = keyboardInputStorageService;
    private readonly ILogger<KeyboardMonitorService> _log = logger;

    /* ------------------------------------------------------------------ */
    public void StartTracking()
    {
        _hook = Hook.GlobalEvents();
        _hook.KeyPress += OnKeyPress;   // printable characters
        _hook.KeyDown  += OnKeyDown;    // everything

        _log.LogInformation("Keyboard tracking started …");
    }

    public void StopTracking()
    {
        FlushBuffer();
        if (_hook is not null)
        {
            _hook.KeyPress -= OnKeyPress;
            _hook.KeyDown  -= OnKeyDown;
        }
        _log.LogInformation("Keyboard tracking stopped.");
    }

    public void Dispose() => StopTracking();

    /* ---------------------------- events ------------------------------ */
    private void OnKeyPress(object? _, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar)) return;

        if ((Control.ModifierKeys & (Keys.Control | Keys.Alt)) != 0) return;
        
        _buffer.Append(e.KeyChar);

        // Size‑based flush
        if (_buffer.Length >= MaxCharsInBuffer)
            FlushBuffer();
    }

    private void OnKeyDown(object? _, KeyEventArgs e)
    {
        if (!ShouldLogSpecial(e)) return;   // “normal” key → ignore

        FlushBuffer();                      // dump text first
        _ = SaveAsync(KeyboardInputType.Special, BuildSpecialString(e));
    }

    /* ------------------------- classification ------------------------- */

    /// <summary>True ⇢ this key press counts as “special” and should be logged.</summary>
    private static bool ShouldLogSpecial(KeyEventArgs e)
    {
        // ignore pure modifier keys (Shift/Ctrl/Alt by themselves)
        if (e.KeyCode is Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey or
                        Keys.LControlKey or Keys.RControlKey or Keys.ControlKey or
                        Keys.LMenu      or Keys.RMenu      or Keys.Menu)
            return false;

        // any key in combination with Ctrl or Alt is “special”
        if (e.Control || e.Alt) return true;

        // navigation / editing keys we explicitly care about
        return e.KeyCode is
            Keys.Return or         // Enter
            Keys.Tab    or
            Keys.Back   or Keys.Delete or
            Keys.Left   or Keys.Right or Keys.Up or Keys.Down or
            Keys.Home   or Keys.End   or Keys.PageUp or Keys.PageDown or
            Keys.Insert or
            Keys.Escape ||
            e.KeyCode is >= Keys.F1 and <= Keys.F24;   // function keys
    }

    private static string BuildSpecialString(KeyEventArgs e)
    {
        var parts = new List<string>();
        if (e.Control) parts.Add("ctrl");
        if (e.Alt)     parts.Add("alt");
        if (e.Shift && (e.Control || e.Alt)) parts.Add("shift");

        parts.Add(e.KeyCode switch
        {
            Keys.Return => "enter",
            Keys.Back   => "backspace",
            Keys.Delete => "delete",
            Keys.Tab    => "tab",
            Keys.Left   => "arrow_left",
            Keys.Right  => "arrow_right",
            Keys.Up     => "arrow_up",
            Keys.Down   => "arrow_down",
            _           => e.KeyCode.ToString().ToLower()
        });

        return string.Join('+', parts);
    }

    /* --------------------------- storage ------------------------------ */

    private void FlushBuffer()
    {
        if (_buffer.Length == 0) return;
        _ = SaveAsync(KeyboardInputType.Text, _buffer.ToString());
        _buffer.Clear();
    }

    private async Task SaveAsync(KeyboardInputType type, string value)
    {

        var logEntry = new KeyboardInputLog
        {
            Content   = value,
            Type      = type,
            Timestamp = DateTime.Now
        };

        await _storage.SaveLogAsyncAndEncrypt(logEntry);
        _log.LogDebug("Saved log: {Content}", logEntry.Content);
    }
}
