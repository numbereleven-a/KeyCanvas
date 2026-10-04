using System.Drawing.Drawing2D;

namespace KeyCanvas;

internal static class StartupHints
{
    internal const double Duration = 5;
    internal static float Opacity(double elapsed) => (float)Math.Clamp((Duration - elapsed) / 1.2, 0, 1);

    internal static void Draw(Graphics graphics, Rectangle viewport, AppLanguage language, double elapsed, Color canvasBackground, CanvasSettings? settings = null)
    {
        settings ??= new();
        ActionShortcut[] shortcuts = [settings.ExitShortcut, settings.ClearShortcut, settings.MenuShortcut];
        float opacity = Opacity(elapsed);
        if (opacity <= 0)
            return;
        float scale = Math.Min(viewport.Width / 1200f, viewport.Height / 700f);
        bool light = canvasBackground.GetBrightness() > .55;
        Color ink = light ? Color.FromArgb(25, 30, 45) : Color.FromArgb(235, 242, 255);
        using var foreground = new SolidBrush(Color.FromArgb((int)(240 * opacity), ink));
        using var secondary = new SolidBrush(Color.FromArgb((int)(170 * opacity), ink));
        using var titleFont = new Font("Segoe UI", 42 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var labelFont = new Font("Segoe UI", 24 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", 20 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF Row(float top, float height) => new(viewport.Left, viewport.Top + viewport.Height * top,
            viewport.Width, height * scale);
        graphics.DrawString("KeyCanvas", titleFont, foreground, Row(.10f, 70), format);
        graphics.DrawString(UiText.Pick(language, "Every key paints", "Нажимайте любые клавиши"),
            labelFont, secondary, Row(.21f, 45), format);

        Span<PointF> points = stackalloc PointF[10];
        for (int index = 0; index < 3; index++)
        {
            float x = viewport.Left + viewport.Width * (.22f + index * .28f);
            float y = viewport.Top + viewport.Height * .46f;
            Color accent = index switch
            {
                0 => light ? Color.FromArgb(210, 45, 104) : Color.FromArgb(255, 106, 164),
                1 => light ? Color.FromArgb(0, 132, 177) : Color.FromArgb(103, 218, 255),
                _ => light ? Color.FromArgb(170, 112, 0) : Color.FromArgb(255, 209, 105)
            };
            using var tint = new SolidBrush(Color.FromArgb((int)(22 * opacity), accent));
            using var outline = new Pen(Color.FromArgb((int)(160 * opacity), accent), 2 * scale);
            using var color = new SolidBrush(Color.FromArgb((int)(255 * opacity), accent));
            float radius = 85 * scale;
            if (index == 0)
            {
                graphics.FillEllipse(tint, x - radius, y - radius, radius * 2, radius * 2);
                graphics.DrawEllipse(outline, x - radius, y - radius, radius * 2, radius * 2);
            }
            else
            {
                int vertices = index == 1 ? 3 : 10;
                for (int point = 0; point < vertices; point++)
                {
                    float angle = -MathF.PI / 2 + point * MathF.Tau / vertices;
                    float r = radius * (index == 2 && point % 2 == 1 ? .62f : 1.2f);
                    points[point] = new PointF(x + MathF.Cos(angle) * r, y + MathF.Sin(angle) * r);
                }
                graphics.FillPolygon(tint, points[..vertices]);
                graphics.DrawPolygon(outline, points[..vertices]);
            }
            for (int particle = 0; particle < 5; particle++)
            {
                double angle = particle * Math.Tau / 5 + elapsed * .22 + index;
                float distance = (110 + particle * 4) * scale;
                float r = (2 + particle % 3) * scale;
                graphics.FillEllipse(color, x + (float)Math.Cos(angle) * distance - r,
                    y + (float)Math.Sin(angle) * distance - r, r * 2, r * 2);
            }
            string key = shortcuts[index].Caption;
            using var chordFont = new Font("Segoe UI", (key.Length > 10 ? 18 : key.Length > 4 ? 25 : 52) * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString(key, chordFont, color, new RectangleF(x - radius, y - radius, radius * 2, radius * 2), format);
            string hold = UiText.Pick(language, $"Hold {shortcuts[index].HoldSeconds:0.#} seconds", $"Держать {shortcuts[index].HoldSeconds:0.#} сек");
            string action = index switch
            {
                0 => UiText.Pick(language, "Quit", "Выход"),
                1 => UiText.Pick(language, "Clear", "Очистка"),
                _ => UiText.Pick(language, "Settings", "Настройки")
            };
            var column = new RectangleF(x - viewport.Width * .14f, viewport.Top + viewport.Height * .62f,
                viewport.Width * .28f, 40 * scale);
            graphics.DrawString(hold, smallFont, secondary, column, format);
            column.Y += 38 * scale;
            graphics.DrawString(action, labelFont, foreground, column, format);
        }
        string footer = settings.AlphabetMode ? UiText.Pick(language,
            "Alt+Shift: keyboard layout  ·  Ctrl+Alt+Delete: Windows",
            "Alt+Shift: раскладка  ·  Ctrl+Alt+Delete: Windows") : UiText.Pick(language,
            "Space: wave  ·  Arrows: particles  ·  Ctrl+Alt+Delete: Windows",
            "Пробел: волна  ·  Стрелки: частицы  ·  Ctrl+Alt+Delete: Windows");
        graphics.DrawString(footer, smallFont, secondary, Row(.83f, 45), format);
    }
}
