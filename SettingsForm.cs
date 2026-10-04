namespace KeyCanvas;

internal sealed class SettingsForm : Form
{
    private static readonly int[] FrameRates = [30, 60, 90, 120];
    private readonly Font bodyFont = new("Segoe UI", 11);
    private readonly Font headingFont = new("Segoe UI", 30, FontStyle.Bold);
    private readonly Font cardFont = new("Segoe UI", 13, FontStyle.Bold);
    private readonly Font hintFont = new("Segoe UI", 9);
    private readonly Button background = new SettingsButton() { Name = "Background", AutoSize = true, MinimumSize = new Size(180, 28) };
    private readonly ComboBox language = Choice(["ENG · English", "RUS · Русский"]);
    private readonly ComboBox figures = Choice([]);
    private readonly ComboBox palette = Choice([]);
    private readonly ComboBox fps = Choice(["30 FPS", "60 FPS", "90 FPS", "120 FPS"]);
    private readonly ComboBox sound = Choice([]);
    private readonly NumericUpDown volume = Number(0, 100, 5);
    private readonly NumericUpDown opacity = Number(10, 100, 10);
    private readonly CheckBox alphabet = Toggle();
    private readonly CheckBox transparent = Toggle();
    private readonly ShortcutEditor exitShortcut = new();
    private readonly ShortcutEditor clearShortcut = new();
    private readonly ShortcutEditor menuShortcut = new();
    private readonly NumericUpDown size = Number(50, 200, 10);
    private readonly NumericUpDown speed = Number(25, 200, 25);
    private readonly NumericUpDown particles = Number(0, 200, 25);
    private readonly NumericUpDown limit = Number(100, 1000, 100);
    private readonly NumericUpDown lifetime = Number(5, 60, 5);
    private readonly CheckBox trail = Toggle();
    private readonly CheckBox clicks = Toggle();
    private readonly CheckBox grow = Toggle();
    private readonly CheckBox rhythm = Toggle();
    private readonly CheckBox timing = Toggle();
    private readonly CheckBox showFps = Toggle();
    private readonly CheckBox startupHints = new SettingsToggle() { Name = "StartupHints" };
    private readonly CheckBox soundEnabled = Toggle();
    private readonly CanvasSound previewSound;
    private readonly bool ownsPreviewSound;
    private readonly List<(Control Control, string English, string Russian)> translations = new();
    private AppLanguage SelectedLanguage => (AppLanguage)language.SelectedIndex;
    private Color backgroundColor;

    internal CanvasSettings SelectedSettings => new()
    {
        Language = SelectedLanguage,
        BackgroundArgb = backgroundColor.ToArgb(), Figures = (FigureStyle)figures.SelectedIndex,
        Palette = (ColorPalette)palette.SelectedIndex, FramesPerSecond = FrameRates[fps.SelectedIndex],
        ShapeSizePercent = (int)size.Value, AnimationSpeedPercent = (int)speed.Value,
        ParticleAmountPercent = (int)particles.Value, ObjectLimit = (int)limit.Value,
        FigureLifetimeSeconds = (int)lifetime.Value, MouseTrail = trail.Checked, MouseClicks = clicks.Checked,
        GrowWhileHeld = grow.Checked, ReactToRhythm = rhythm.Checked,
        ShowFrameTiming = timing.Checked, ShowFps = showFps.Checked,
        ShowStartupHints = startupHints.Checked, SoundsEnabled = soundEnabled.Checked,
        Sound = (SoundStyle)sound.SelectedIndex, SoundVolumePercent = (int)volume.Value,
        AlphabetMode = alphabet.Checked, TransparentCanvas = transparent.Checked,
        CanvasOpacityPercent = (int)opacity.Value,
        ExitShortcut = exitShortcut.Selected, ClearShortcut = clearShortcut.Selected, MenuShortcut = menuShortcut.Selected
    };

    internal SettingsForm(CanvasSettings settings, CanvasSound? sharedSound = null)
    {
        ownsPreviewSound = sharedSound is null;
        previewSound = sharedSound ?? new();
        language.Name = "Language";
        sound.Name = "Sound";
        Font = bodyFont;
        BackColor = Color.FromArgb(248, 249, 252);
        ForeColor = SettingsTheme.Ink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(1120, 980);

        var layout = new TableLayoutPanel
        {
            Name = "OptionsLayout", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
            Padding = new Padding(12, 0, 12, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var appearance = Card(layout, 0, 0, "Appearance", "Оформление", "Language, background and visual style.", "Язык, фон и визуальный стиль.", "✦", Color.FromArgb(238, 230, 255));
        var shapes = Card(layout, 0, 1, "Shapes", "Фигуры", "Shape style, quantity and motion.", "Вид фигур, их количество и движение.", "□", Color.FromArgb(252, 227, 251));
        var audio = Card(layout, 1, 0, "Performance & Audio", "Производительность и звук", "Frame rate and sound settings.", "Частота кадров и настройки звука.", "⚙", Color.FromArgb(221, 248, 236));
        var interaction = Card(layout, 1, 1, "Interaction", "Взаимодействие", "Choose how the canvas responds to input.", "Выберите реакцию полотна на нажатия.", "↗", Color.FromArgb(225, 239, 255));
        var hero = new SettingsHero();
        var heroText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent };
        AddWide(heroText, Translate(new Label { Font = headingFont, AutoSize = true, Margin = Padding.Empty }, "Make the canvas yours", "Полотно на ваш вкус"));
        AddWide(heroText, Translate(new Label { Font = bodyFont, AutoSize = true, ForeColor = SettingsTheme.Muted, Margin = new Padding(0, 6, 0, 0) },
            "Customize how shapes look, move and react on your screen.", "Настройте вид фигур, их движение и реакцию на нажатия."));
        hero.Controls.Add(heroText);
        AddRow(appearance, "Language", "Язык", language);
        AddRow(appearance, "Background color", "Цвет фона", background);
        AddRow(appearance, "Canvas opacity, %", "Непрозрачность полотна, %", opacity);
        Translate(transparent, "Transparent canvas (show desktop behind it)", "Прозрачное полотно (виден рабочий стол)");
        Translate(alphabet, "Alphabet and key names (current keyboard layout)", "Алфавит и названия клавиш (текущая раскладка)");
        AddWide(appearance, transparent);
        AddWide(appearance, alphabet);
        AddRow(shapes, "Shapes", "Фигуры", figures);
        AddRow(shapes, "Palette", "Палитра", palette);
        AddRow(shapes, "Shape size, %", "Размер фигур, %", size);
        AddRow(shapes, "Animation speed, %", "Скорость анимации, %", speed);
        AddRow(shapes, "Particle amount, %", "Количество частиц, %", particles);
        AddRow(shapes, "Maximum objects", "Максимум объектов", limit);
        AddRow(shapes, "Shape lifetime, seconds", "Длительность фигур, сек", lifetime);
        AddRow(audio, "Target frame rate", "Частота кадров", fps);
        AddRow(audio, "Sound", "Звук", sound);
        AddRow(audio, "Sound volume, %", "Громкость звука, %", volume);
        var preview = Translate(new SettingsButton { Name = "PreviewSound", AutoSize = true, ForeColor = SettingsTheme.Purple, BackColor = Color.FromArgb(245, 240, 255) }, "Listen", "Послушать");
        preview.Click += (_, _) =>
        {
            previewSound.ApplySettings(SelectedSettings with { SoundsEnabled = true });
            previewSound.Play(0, InputBuffer.Now);
            if (!previewSound.Available && SelectedSettings.SoundVolumePercent > 0)
                MessageBox.Show(this, UiText.Pick(SelectedLanguage, "Could not open Windows MIDI output. It may be in use. You can choose a soft sound instead.", "Не удалось открыть MIDI-устройство Windows. Оно может быть занято. Можно выбрать мягкий звук."), Text);
        };
        AddRow(audio, "Sound preview", "Проверить звук", preview);
        Translate(trail, "Draw a trail when the mouse moves", "Рисовать след при движении мыши");
        Translate(clicks, "React to mouse buttons and wheel", "Реагировать на кнопки и колесо мыши");
        Translate(grow, "Grow shapes while keys are held", "Увеличивать фигуры при удержании клавиш");
        Translate(rhythm, "Boost effects with faster key presses", "Усиливать эффекты при быстрых нажатиях");
        Translate(showFps, "Always show FPS", "Постоянно показывать FPS");
        Translate(timing, "Show frame time (median and p95)", "Показывать время кадра (медиана и p95)");
        Translate(startupHints, "Show shortcut hints at startup", "Показывать горячие клавиши при запуске");
        Translate(soundEnabled, "Enable quiet sounds", "Включить тихие звуки");
        AddWide(interaction, trail);
        AddWide(interaction, clicks);
        AddWide(interaction, grow);
        AddWide(interaction, rhythm);
        AddWide(interaction, soundEnabled);
        AddWide(interaction, startupHints);
        AddWide(interaction, showFps);
        AddWide(interaction, timing);
        var buttons = new FlowLayoutPanel
        {
            Name = "ActionButtons", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Bottom,
            WrapContents = true, Padding = new Padding(22, 8, 22, 8)
        };
        var apply = Translate(new SettingsButton { Name = "Apply", Primary = true, AutoSize = true, DialogResult = DialogResult.OK }, "Apply and continue", "Применить и продолжить");
        apply.Click += (_, _) =>
        {
            var chosen = SelectedSettings;
            if (chosen.ExitShortcut.SameChord(chosen.ClearShortcut) || chosen.ExitShortcut.SameChord(chosen.MenuShortcut) || chosen.ClearShortcut.SameChord(chosen.MenuShortcut))
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(this, UiText.Pick(SelectedLanguage, "Choose different shortcuts for each action.", "Выберите разные сочетания для каждого действия."), Text);
            }
        };
        var cancel = Translate(new SettingsButton { AutoSize = true, DialogResult = DialogResult.Cancel }, "Cancel", "Отмена");
        var reset = Translate(new SettingsButton { Name = "Reset", AutoSize = true }, "Defaults", "По умолчанию");
        reset.Click += (_, _) => SetValues(new() { Language = SelectedLanguage });
        var spacer = new Panel { Height = 1, Margin = Padding.Empty };
        buttons.Controls.AddRange([reset, spacer, cancel, apply]);
        buttons.Layout += (_, _) =>
        {
            int occupied = new[] { reset, cancel, apply }.Sum(button => button.GetPreferredSize(Size.Empty).Width + button.Margin.Horizontal);
            int width = Math.Max(0, buttons.ClientSize.Width - buttons.Padding.Horizontal - occupied - 2);
            if (spacer.Width != width) spacer.Width = width;
        };
        AcceptButton = apply;
        CancelButton = cancel;
        var tabs = new SettingsTabs();
        var canvasTab = Translate(new TabPage(), "Canvas", "Полотно");
        canvasTab.BackColor = BackColor;
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BackColor };
        viewport.Controls.Add(layout);
        canvasTab.Controls.Add(viewport);
        canvasTab.Controls.Add(hero);
        viewport.Resize += (_, _) =>
        {
            bool narrow = viewport.ClientSize.Width < 900 * DeviceDpi / 96;
            layout.SuspendLayout();
            layout.ColumnCount = narrow ? 1 : 2;
            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, narrow ? 100 : 50));
            if (!narrow) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var cards = new[] { appearance.Parent!, audio.Parent!, shapes.Parent!, interaction.Parent! };
            for (int index = 0; index < cards.Length; index++)
            {
                layout.SetColumn(cards[index], narrow ? 0 : index % 2);
                layout.SetRow(cards[index], narrow ? index : index / 2);
            }
            layout.ResumeLayout(true);
        };
        var shortcutTab = Translate(new TabPage(), "Shortcuts", "Горячие клавиши");
        shortcutTab.BackColor = BackColor;
        var shortcutLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, BackColor = Color.White };
        AddWide(shortcutLayout, Translate(new Label { Font = bodyFont, AutoSize = true, ForeColor = SettingsTheme.Muted, Margin = new Padding(0, 0, 0, 20) },
            "Choose a key, optional modifiers and hold time in seconds.\nRelease any required key to cancel. Ctrl+Alt+Delete remains available.",
            "Выберите клавишу, модификаторы и время удержания в секундах.\nОтпускание нужной клавиши отменяет действие. Ctrl+Alt+Delete доступен."));
        AddWide(shortcutLayout, Translate(new Label { Font = cardFont, AutoSize = true, Margin = new Padding(0, 12, 0, 8) }, "Quit / unlock", "Выход / разблокировка"));
        AddWide(shortcutLayout, exitShortcut);
        AddWide(shortcutLayout, Translate(new Label { Font = cardFont, AutoSize = true, Margin = new Padding(0, 20, 0, 8) }, "Clear canvas", "Очистка полотна"));
        AddWide(shortcutLayout, clearShortcut);
        AddWide(shortcutLayout, Translate(new Label { Font = cardFont, AutoSize = true, Margin = new Padding(0, 20, 0, 8) }, "Open settings", "Открыть настройки"));
        AddWide(shortcutLayout, menuShortcut);
        foreach (var editor in new[] { exitShortcut, clearShortcut, menuShortcut })
            editor.Font = bodyFont;
        var shortcutCard = new SettingsCard { Dock = DockStyle.Top };
        shortcutCard.Controls.Add(shortcutLayout);
        var shortcutViewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22, 0, 22, 22), BackColor = BackColor };
        shortcutViewport.Controls.Add(shortcutCard);
        var shortcutHero = new SettingsHero();
        var shortcutHeroText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent };
        AddWide(shortcutHeroText, Translate(new Label { Font = headingFont, AutoSize = true, Margin = Padding.Empty }, "Your shortcuts", "Ваши горячие клавиши"));
        AddWide(shortcutHeroText, Translate(new Label { Font = bodyFont, AutoSize = true, ForeColor = SettingsTheme.Muted, Margin = new Padding(0, 6, 0, 0) },
            "Choose how to unlock, clear and open settings.", "Настройте выход, очистку и открытие настроек."));
        shortcutHero.Controls.Add(shortcutHeroText);
        shortcutTab.Controls.Add(shortcutViewport);
        shortcutTab.Controls.Add(shortcutHero);
        tabs.TabPages.AddRange([canvasTab, shortcutTab]);
        Controls.Add(tabs);
        Controls.Add(buttons);
        language.SelectedIndexChanged += (_, _) => ApplyLanguage();
        SetValues(settings);
        transparent.CheckedChanged += (_, _) => opacity.Enabled = transparent.Checked;
        opacity.Enabled = transparent.Checked;

        background.Click += (_, _) =>
        {
            using var picker = new ColorDialog { Color = backgroundColor, FullOpen = true };
            if (picker.ShowDialog(this) == DialogResult.OK)
                SetBackground(picker.Color);
        };
    }

    private void SetValues(CanvasSettings settings)
    {
        language.SelectedIndex = (int)settings.Language;
        SetBackground(Color.FromArgb(settings.BackgroundArgb));
        figures.SelectedIndex = (int)settings.Figures;
        palette.SelectedIndex = (int)settings.Palette;
        fps.SelectedIndex = Array.IndexOf(FrameRates, settings.FramesPerSecond);
        size.Value = settings.ShapeSizePercent;
        speed.Value = settings.AnimationSpeedPercent;
        particles.Value = settings.ParticleAmountPercent;
        limit.Value = settings.ObjectLimit;
        lifetime.Value = settings.FigureLifetimeSeconds;
        trail.Checked = settings.MouseTrail;
        clicks.Checked = settings.MouseClicks;
        grow.Checked = settings.GrowWhileHeld;
        rhythm.Checked = settings.ReactToRhythm;
        timing.Checked = settings.ShowFrameTiming;
        showFps.Checked = settings.ShowFps;
        startupHints.Checked = settings.ShowStartupHints;
        soundEnabled.Checked = settings.SoundsEnabled;
        sound.SelectedIndex = (int)settings.Sound;
        volume.Value = settings.SoundVolumePercent;
        alphabet.Checked = settings.AlphabetMode;
        transparent.Checked = settings.TransparentCanvas;
        opacity.Value = settings.CanvasOpacityPercent;
        exitShortcut.SetValues(settings.ExitShortcut);
        clearShortcut.SetValues(settings.ClearShortcut);
        menuShortcut.SetValues(settings.MenuShortcut);
    }

    private T Translate<T>(T control, string english, string russian) where T : Control
    {
        if (control is CheckBox or Button)
            control.Font = bodyFont;
        if (control is Label label)
            label.UseMnemonic = false;
        translations.Add((control, english, russian));
        return control;
    }

    private void ApplyLanguage()
    {
        SuspendLayout();
        Text = UiText.SettingsTitle(SelectedLanguage);
        foreach (var item in translations)
            item.Control.Text = UiText.Pick(SelectedLanguage, item.English, item.Russian);
        int figure = Math.Max(0, figures.SelectedIndex);
        int colors = Math.Max(0, palette.SelectedIndex);
        int timbre = Math.Max(0, sound.SelectedIndex);
        figures.Items.Clear();
        figures.Items.AddRange(SelectedLanguage == AppLanguage.Russian
            ? ["Случайные фигуры", "Круги", "Треугольники", "Квадраты", "Звёзды", "Линии", "Кольца"]
            : ["Random shapes", "Circles", "Triangles", "Squares", "Stars", "Lines", "Rings"]);
        palette.Items.Clear();
        palette.Items.AddRange(SelectedLanguage == AppLanguage.Russian
            ? ["Яркая", "Пастельная", "Тёплая", "Холодная"] : ["Bright", "Pastel", "Warm", "Cool"]);
        sound.Items.Clear();
        sound.Items.AddRange(SelectedLanguage == AppLanguage.Russian
            ? ["Пианино (MIDI)", "Колокольчики (MIDI)", "Ксилофон (MIDI)", "Синтезатор (MIDI)", "Мягкое пианино", "Мягкие колокольчики", "Мягкий ксилофон"]
            : ["Piano (MIDI)", "Bells (MIDI)", "Xylophone (MIDI)", "Synthesizer (MIDI)", "Soft piano", "Soft bells", "Soft xylophone"]);
        figures.SelectedIndex = figure;
        palette.SelectedIndex = colors;
        sound.SelectedIndex = timbre;
        SetBackground(backgroundColor);
        ResumeLayout(true);
    }

    private void SetBackground(Color color)
    {
        backgroundColor = color;
        background.BackColor = color;
        background.ForeColor = color.GetBrightness() > .55 ? Color.Black : Color.White;
        background.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        background.UseVisualStyleBackColor = false;
    }

    private TableLayoutPanel Card(TableLayoutPanel parent, int column, int row, string title, string russianTitle,
        string subtitle, string russianSubtitle, string glyph, Color tint)
    {
        var card = new SettingsCard();
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = Color.White };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, MinimumSize = new Size(0, 58), ColumnCount = 2, Margin = new Padding(0, 0, 0, 10) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var badge = new Label { Text = glyph, Font = cardFont, ForeColor = SettingsTheme.Purple, BackColor = tint,
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 10, 8) };
        header.Controls.Add(badge, 0, 0);
        header.SetRowSpan(badge, 2);
        header.Controls.Add(Translate(new Label { Font = cardFont, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty }, title, russianTitle), 1, 0);
        header.Controls.Add(Translate(new Label { Font = hintFont, ForeColor = SettingsTheme.Muted, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 0) }, subtitle, russianSubtitle), 1, 1);
        AddWide(content, header);
        card.Controls.Add(content);
        parent.Controls.Add(card, column, row);
        return content;
    }
    private static ComboBox Choice(string[] items)
    {
        var choice = new ComboBox { FlatStyle = FlatStyle.Flat, BackColor = Color.White, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        choice.DrawMode = DrawMode.OwnerDrawFixed;
        choice.ItemHeight = 24;
        choice.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            using var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0
                ? Color.FromArgb(239, 232, 255) : Color.White);
            e.Graphics.FillRectangle(fill, e.Bounds);
            TextRenderer.DrawText(e.Graphics, choice.GetItemText(choice.Items[e.Index]), choice.Font, Rectangle.Inflate(e.Bounds, -5, 0),
                SettingsTheme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        choice.Items.AddRange(items);
        return choice;
    }

    private static NumericUpDown Number(int min, int max, int step) => new()
    {
        Minimum = min, Maximum = max, Increment = step, Width = 74, BorderStyle = BorderStyle.FixedSingle
    };

    private static CheckBox Toggle() => new SettingsToggle();

    private void AddRow(TableLayoutPanel layout, string english, string russian, Control control)
    {
        int row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(Translate(new Label { Font = bodyFont, AutoSize = true, Anchor = AnchorStyles.Left }, english, russian), 0, row);
        control.Font = bodyFont;
        if (control == background)
        {
            control.AutoSize = false;
            control.Height = 42;
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        if (control is NumericUpDown number)
        {
            number.Name = english;
            var pair = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            pair.Controls.Add(new SettingsSlider(number) { AccessibleName = english }, 0, 0);
            number.Dock = DockStyle.Fill;
            pair.Controls.Add(number, 1, 0);
            control = pair;
        }
        control.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(control, 1, row);
    }

    private static void AddWide(TableLayoutPanel layout, Control control)
    {
        int row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(control, 0, row);
        layout.SetColumnSpan(control, layout.ColumnCount);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            if (ownsPreviewSound)
                previewSound.Dispose();
            else
                previewSound.Stop();
            bodyFont.Dispose();
            headingFont.Dispose();
            cardFont.Dispose();
            hintFont.Dispose();
        }
    }
}
