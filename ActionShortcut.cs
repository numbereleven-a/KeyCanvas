namespace KeyCanvas;

internal sealed record ActionShortcut
{
    public Keys Key { get; init; }
    public Keys Modifiers { get; init; }
    public double HoldSeconds { get; init; }
    internal static readonly Keys[] AllowedKeys = [Keys.Escape, .. Enumerable.Range((int)Keys.F1, 12).Select(key => (Keys)key),
        .. Enumerable.Range((int)Keys.A, 26).Select(key => (Keys)key), .. Enumerable.Range((int)Keys.D0, 10).Select(key => (Keys)key), Keys.Space, Keys.Return];
    internal string Caption => (Modifiers.HasFlag(Keys.Control) ? "Ctrl+" : "") +
        (Modifiers.HasFlag(Keys.Alt) ? "Alt+" : "") + (Modifiers.HasFlag(Keys.Shift) ? "Shift+" : "") +
        (Key == Keys.Escape ? "Esc" : Key.ToString());
    internal ActionShortcut Normalize(ActionShortcut fallback) => AllowedKeys.Contains(Key)
        ? this with { Modifiers = Modifiers & (Keys.Control | Keys.Alt | Keys.Shift), HoldSeconds = Math.Clamp(double.IsFinite(HoldSeconds) ? HoldSeconds : fallback.HoldSeconds, .5, 10) }
        : fallback;
    internal bool SameChord(ActionShortcut other) => Key == other.Key && Modifiers == other.Modifiers;
    internal double? Started(double?[] held)
    {
        double? start = held[(int)Key];
        if (!start.HasValue)
            return null;
        double? Modifier(Keys generic, Keys left, Keys right) => held[(int)generic] ?? held[(int)left] ?? held[(int)right];
        bool Match(Keys flag, double? since)
        {
            if (Modifiers.HasFlag(flag) != since.HasValue)
                return false;
            if (since.HasValue)
                start = Math.Max(start.Value, since.Value);
            return true;
        }
        return Match(Keys.Control, Modifier(Keys.ControlKey, Keys.LControlKey, Keys.RControlKey)) &&
            Match(Keys.Alt, Modifier(Keys.Menu, Keys.LMenu, Keys.RMenu)) &&
            Match(Keys.Shift, Modifier(Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey)) ? start : null;
    }
}

internal sealed class ShortcutEditor : FlowLayoutPanel
{
    private readonly ComboBox key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 85 };
    private readonly CheckBox ctrl = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox alt = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox shift = new() { Text = "Shift", AutoSize = true };
    private readonly NumericUpDown seconds = new() { Minimum = .5m, Maximum = 10, DecimalPlaces = 1, Increment = .5m, Width = 65 };
    internal ShortcutEditor()
    {
        AutoSize = true;
        Dock = DockStyle.Fill;
        WrapContents = true;
        key.Items.AddRange(ActionShortcut.AllowedKeys.Select(value => (object)(value == Keys.Escape ? "Esc" : value.ToString())).ToArray());
        Controls.AddRange([key, ctrl, alt, shift, seconds]);
    }
    internal ActionShortcut Selected => new()
    {
        Key = ActionShortcut.AllowedKeys[key.SelectedIndex],
        Modifiers = (ctrl.Checked ? Keys.Control : Keys.None) | (alt.Checked ? Keys.Alt : Keys.None) | (shift.Checked ? Keys.Shift : Keys.None),
        HoldSeconds = (double)seconds.Value
    };
    internal void SetValues(ActionShortcut value)
    {
        key.SelectedIndex = Array.IndexOf(ActionShortcut.AllowedKeys, value.Key);
        ctrl.Checked = value.Modifiers.HasFlag(Keys.Control);
        alt.Checked = value.Modifiers.HasFlag(Keys.Alt);
        shift.Checked = value.Modifiers.HasFlag(Keys.Shift);
        seconds.Value = (decimal)value.HoldSeconds;
    }
}
