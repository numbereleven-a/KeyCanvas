using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace KeyCanvas;

internal static class SettingsTheme
{
    internal static readonly Color Purple = Color.FromArgb(105, 51, 245);
    internal static readonly Color Ink = Color.FromArgb(20, 24, 48);
    internal static readonly Color Muted = Color.FromArgb(111, 119, 149);
    internal static readonly Color Border = Color.FromArgb(226, 229, 241);
    internal static GraphicsPath Round(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class SettingsCard : Panel
{
    internal SettingsCard()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Padding = new Padding(16);
        Margin = new Padding(7);
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = SettingsTheme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 12 * DeviceDpi / 96f);
        using var fill = new SolidBrush(Color.White);
        using var border = new Pen(SettingsTheme.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        base.OnPaint(e);
    }
}

internal sealed class SettingsButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Primary { get; init; }
    internal SettingsButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.White;
        AutoSize = true;
        Padding = new Padding(16, 9, 16, 9);
        Margin = new Padding(5);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        // ButtonBase can skip background painting; initialize the entire reused buffer.
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = Primary ? SettingsTheme.Purple : BackColor;
        using var path = SettingsTheme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 9 * DeviceDpi / 96f);
        using var brush = new SolidBrush(fill);
        using var border = new Pen(Primary ? SettingsTheme.Purple : SettingsTheme.Border);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Primary ? Color.White : ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
    }
}

internal sealed class SettingsToggle : CheckBox
{
    internal SettingsToggle()
    {
        AutoSize = true;
        Margin = new Padding(0, 4, 0, 4);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        int gap = (int)(32 * DeviceDpi / 96f);
        int width = proposedSize.Width > gap ? proposedSize.Width - gap : int.MaxValue;
        var text = TextRenderer.MeasureText(Text, Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak);
        return new Size(text.Width + gap, Math.Max(text.Height, (int)(24 * DeviceDpi / 96f)));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        float scale = DeviceDpi / 96f;
        float side = 20 * scale;
        var box = new RectangleF(0, (Height - side) / 2, side, side);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = SettingsTheme.Round(box, 4 * scale);
        using var fill = new SolidBrush(Checked ? SettingsTheme.Purple : Color.White);
        using var outline = new Pen(Checked ? SettingsTheme.Purple : SettingsTheme.Muted, 1.3f * scale);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(outline, path);
        if (Checked)
        {
            using var pen = new Pen(Color.White, 2 * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLines(pen, [new PointF(box.X + side * .22f, box.Y + side * .5f),
                new PointF(box.X + side * .43f, box.Y + side * .72f), new PointF(box.X + side * .8f, box.Y + side * .27f)]);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)(29 * scale), 0, Width - (int)(29 * scale), Height),
            Enabled ? ForeColor : SystemColors.GrayText, TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1));
    }
}

internal sealed class SettingsSlider : Control
{
    private readonly NumericUpDown number;
    private bool dragging;
    internal SettingsSlider(NumericUpDown number)
    {
        this.number = number;
        DoubleBuffered = true;
        Height = 28;
        Dock = DockStyle.Fill;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.Slider;
        number.ValueChanged += (_, _) => Invalidate();
        number.EnabledChanged += (_, _) => { Enabled = number.Enabled; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float r = 8 * DeviceDpi / 96f;
        float start = r + 2, width = Math.Max(1, Width - start * 2);
        float x = start + (float)((number.Value - number.Minimum) / (number.Maximum - number.Minimum)) * width;
        float y = Height / 2f;
        using var track = new Pen(SettingsTheme.Border, 5 * DeviceDpi / 96f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var active = new Pen(Enabled ? SettingsTheme.Purple : SettingsTheme.Muted, track.Width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(track, start, y, start + width, y);
        if (x > start) e.Graphics.DrawLine(active, start, y, x, y);
        using var fill = new SolidBrush(Color.White);
        using var outline = new Pen(Color.FromArgb(200, 187, 246), 1.3f);
        e.Graphics.FillEllipse(fill, x - r, y - r, r * 2, r * 2);
        e.Graphics.DrawEllipse(outline, x - r, y - r, r * 2, r * 2);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1));
    }
    private void SetFromMouse(int x)
    {
        decimal fraction = (decimal)Math.Clamp((x - 10.0 * DeviceDpi / 96) / Math.Max(1, Width - 20.0 * DeviceDpi / 96), 0, 1);
        decimal value = number.Minimum + fraction * (number.Maximum - number.Minimum);
        number.Value = Math.Clamp(Math.Round(value / number.Increment) * number.Increment, number.Minimum, number.Maximum);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button != MouseButtons.Left) return; Focus(); Capture = dragging = true; SetFromMouse(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(dragging) SetFromMouse(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = Capture = false; }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        decimal value = e.KeyCode switch { Keys.Left or Keys.Down => number.Value - number.Increment,
            Keys.Right or Keys.Up => number.Value + number.Increment, Keys.Home => number.Minimum, Keys.End => number.Maximum, _ => number.Value };
        number.Value = Math.Clamp(value, number.Minimum, number.Maximum);
        e.Handled = true;
    }
}

internal sealed class SettingsTabs : TabControl
{
    internal SettingsTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Dock = DockStyle.Fill;
        DrawMode = TabDrawMode.OwnerDrawFixed;
        ItemSize = new Size(180, 42);
        SizeMode = TabSizeMode.Fixed;
        Padding = new Point(18, 8);
    }
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateTabSize();
    }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateTabSize();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        UpdateTabSize();
    }
    private void UpdateTabSize()
    {
        int width = Math.Max(180 * DeviceDpi / 96, TabPages.Cast<TabPage>()
            .Select(page => TextRenderer.MeasureText(page.Text, Font).Width + 32 * DeviceDpi / 96).DefaultIfEmpty(0).Max());
        var size = new Size(width, Font.Height + 18 * DeviceDpi / 96);
        if (ItemSize != size) ItemSize = size;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(248, 249, 252));
        for (int index = 0; index < TabCount; index++)
            OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(index), index,
                index == SelectedIndex ? DrawItemState.Selected : DrawItemState.None));
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var fill = new SolidBrush(Color.FromArgb(250, 250, 255));
        e.Graphics.FillRectangle(fill, e.Bounds);
        bool selected = e.Index == SelectedIndex;
        using var labelFont = new Font(Font, selected ? FontStyle.Bold : FontStyle.Regular);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, labelFont, e.Bounds,
            selected ? SettingsTheme.Purple : SettingsTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (selected)
        {
            using var pen = new Pen(SettingsTheme.Purple, 4);
            e.Graphics.DrawLine(pen, e.Bounds.Left + 16, e.Bounds.Bottom - 3, e.Bounds.Right - 16, e.Bounds.Bottom - 3);
        }
    }
}

internal sealed class SettingsHero : Panel
{
    internal SettingsHero()
    {
        DoubleBuffered = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(0, 128);
        Dock = DockStyle.Top;
        Padding = new Padding(22, 18, 210, 12);
    }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        int right = (int)((Width < 900 * DeviceDpi / 96 ? 20 : 210) * DeviceDpi / 96f);
        if (Padding.Right != right)
            Padding = new Padding(Padding.Left, Padding.Top, right, Padding.Bottom);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 900 * DeviceDpi / 96)
            return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        float x = Width - 130 * scale, y = 45 * scale;
        using var purple = new LinearGradientBrush(new RectangleF(x, y - 26 * scale, 64 * scale, 64 * scale), Color.FromArgb(242, 161, 234), SettingsTheme.Purple, 45);
        e.Graphics.FillEllipse(purple, x, y - 26 * scale, 64 * scale, 64 * scale);
        using var blue = new SolidBrush(Color.FromArgb(106, 194, 255));
        e.Graphics.FillPolygon(blue, [new PointF(x - 80 * scale, y - 15 * scale), new PointF(x - 100 * scale, y + 38 * scale), new PointF(x - 43 * scale, y + 23 * scale)]);
        using var gold = new SolidBrush(Color.FromArgb(255, 199, 91));
        e.Graphics.FillPolygon(gold, [new PointF(x - 36 * scale, y + 31 * scale), new PointF(x - 5 * scale, y + 50 * scale), new PointF(x - 23 * scale, y + 80 * scale), new PointF(x - 55 * scale, y + 62 * scale)]);
    }
}
