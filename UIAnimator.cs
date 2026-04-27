namespace MultiplayerLauncher;

internal static class UIAnimator
{
    private static readonly Dictionary<Control, CancellationTokenSource> _tokens = new();

    // ── Public API ─────────────────────────────────────────────────────────────

    /// Attach smooth enter/leave color transitions to any control.
    public static void HoverColor(Control ctrl, Color normal, Color hover,
                                  int enterMs = 100, int leaveMs = 160)
    {
        ctrl.MouseEnter += async (_, _) => await AnimateToAsync(ctrl, hover,  enterMs);
        ctrl.MouseLeave += async (_, _) => await AnimateToAsync(ctrl, normal, leaveMs);
    }

    /// Animate a control's BackColor toward <paramref name="target"/> over <paramref name="durationMs"/> ms.
    /// Cancels any in-progress animation on the same control first.
    public static async Task AnimateToAsync(Control ctrl, Color target, int durationMs)
    {
        if (_tokens.TryGetValue(ctrl, out var old)) { old.Cancel(); old.Dispose(); }

        var cts = new CancellationTokenSource();
        _tokens[ctrl] = cts;

        try
        {
            Color start = ctrl.IsDisposed ? target : ctrl.BackColor;
            const int steps = 10;
            int delay = Math.Max(1, durationMs / steps);

            for (int i = 1; i <= steps; i++)
            {
                if (cts.Token.IsCancellationRequested || ctrl.IsDisposed) return;
                ctrl.BackColor = Lerp(start, target, EaseOut((float)i / steps));
                await Task.Delay(delay, cts.Token);
            }
            if (!ctrl.IsDisposed) ctrl.BackColor = target;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_tokens.TryGetValue(ctrl, out var cur) && cur == cts) _tokens.Remove(ctrl);
            cts.Dispose();
        }
    }

    // ── Color helpers ──────────────────────────────────────────────────────────

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
