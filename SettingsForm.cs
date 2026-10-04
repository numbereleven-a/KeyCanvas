namespace KeyCanvas;

internal sealed class SettingsForm : Form
{
    private static readonly int[] FrameRates = [30, 60, 90, 120];
    private readonly Font bodyFont = new("Segoe UI", 10);
    private readonly Font headingFont = new("Segoe UI", 15, FontStyle.Bold);
    private readonly Button background = new() { Name = "Background", AutoSize = true, MinimumSize = new Size(180, 28) };
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
    private readonly CheckBox startupHints = new() { Name = "StartupHints", AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
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
        ForeColor = Color.FromArgb(30, 36, 54);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(620, 860);

        var layout = new TableLayoutPanel
        {
            Name = "OptionsLayout", Dock = DockStyle.Fill, Padding = new Padding(22, 12, 22, 12), ColumnCount = 2,
            AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        var heading = Translate(new Label
        {
            Font = headingFont,
            AutoSize = true, Margin = new Padding(0, 4, 0, 12)
        }, "Make the canvas yours", "Полотно на ваш вкус");
        AddWide(layout, heading);
        AddRow(layout, "Language", "Язык", language);
        AddRow(layout, "Background color", "Цвет фона", background);
        AddRow(layout, "Canvas opacity, %", "Непрозрачность полотна, %", opacity);
        Translate(transparent, "Transparent canvas (show desktop behind it)", "Прозрачное полотно (виден рабочий стол)");
        Translate(alphabet, "Alphabet and key names (current keyboard layout)", "Алфавит и названия клавиш (текущая раскладка)");
        AddWide(layout, transparent);
        AddWide(layout, alphabet);
        AddRow(layout, "Shapes", "Фигуры", figures);
        AddRow(layout, "Palette", "Палитра", palette);
        AddRow(layout, "Shape size, %", "Размер фигур, %", size);
        AddRow(layout, "Animation speed, %", "Скорость анимации, %", speed);
        AddRow(layout, "Particle amount, %", "Количество частиц, %", particles);
        AddRow(layout, "Maximum objects", "Максимум объектов", limit);
        AddRow(layout, "Shape lifetime, seconds", "Длительность фигур, сек", lifetime);
        AddRow(layout, "Target frame rate", "Частота кадров", fps);
        AddRow(layout, "Sound", "Звук", sound);
        AddRow(layout, "Sound volume, %", "Громкость звука, %", volume);
        var preview = Translate(new Button { Name = "PreviewSound", AutoSize = true }, "Listen", "Послушать");
        preview.Click += (_, _) =>
        {
            previewSound.ApplySettings(SelectedSettings with { SoundsEnabled = true });
            previewSound.Play(0, InputBuffer.Now);
            if (!previewSound.Available && SelectedSettings.SoundVolumePercent > 0)
                MessageBox.Show(this, UiText.Pick(SelectedLanguage, "Could not open Windows MIDI output. It may be in use. You can choose a soft sound instead.", "Не удалось открыть MIDI-устройство Windows. Оно может быть занято. Можно выбрать мягкий звук."), Text);
        };
        AddRow(layout, "Sound preview", "Проверить звук", preview);
        Translate(trail, "Draw a trail when the mouse moves", "Рисовать след при движении мыши");
        Translate(clicks, "React to mouse buttons and wheel", "Реагировать на кнопки и колесо мыши");
        Translate(grow, "Grow shapes while keys are held", "Увеличивать фигуры при удержании клавиш");
        Translate(rhythm, "Boost effects with faster key presses", "Усиливать эффекты при быстрых нажатиях");
        Translate(showFps, "Always show FPS", "Постоянно показывать FPS");
        Translate(timing, "Show frame time (median and p95)", "Показывать время кадра (медиана и p95)");
        Translate(startupHints, "Show shortcut hints at startup", "Показывать горячие клавиши при запуске");
        Translate(soundEnabled, "Enable quiet sounds", "Включить тихие звуки");
        AddWide(layout, trail);
        AddWide(layout, clicks);
        AddWide(layout, grow);
        AddWide(layout, rhythm);
        AddWide(layout, soundEnabled);
        AddWide(layout, startupHints);
        AddWide(layout, showFps);
        AddWide(layout, timing);
        AddWide(layout, Translate(new Label
        {
            AutoSize = true, ForeColor = Color.FromArgb(90, 98, 116), Margin = new Padding(0, 10, 0, 8)
        }, "Apply to save changes and return to the canvas.\nHigher frame rates and more objects use more CPU.",
            "Примените изменения для сохранения и возврата к полотну.\nВысокий FPS и большое число объектов увеличивают нагрузку."));
        var buttons = new FlowLayoutPanel
        {
            Name = "ActionButtons", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Bottom,
            WrapContents = true, Padding = new Padding(22, 8, 22, 8)
        };
        var apply = Translate(new Button { AutoSize = true, DialogResult = DialogResult.OK }, "Apply and continue", "Применить и продолжить");
        apply.Click += (_, _) =>
        {
            var chosen = SelectedSettings;
            if (chosen.ExitShortcut.SameChord(chosen.ClearShortcut) || chosen.ExitShortcut.SameChord(chosen.MenuShortcut) || chosen.ClearShortcut.SameChord(chosen.MenuShortcut))
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(this, UiText.Pick(SelectedLanguage, "Choose different shortcuts for each action.", "Выберите разные сочетания для каждого действия."), Text);
            }
        };
        var cancel = Translate(new Button { AutoSize = true, DialogResult = DialogResult.Cancel }, "Cancel", "Отмена");
        var reset = Translate(new Button { Name = "Reset", AutoSize = true }, "Defaults", "По умолчанию");
        reset.Click += (_, _) => SetValues(new() { Language = SelectedLanguage });
        buttons.Controls.AddRange([apply, cancel, reset]);
        AcceptButton = apply;
        CancelButton = cancel;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var canvasTab = Translate(new TabPage(), "Canvas", "Полотно");
        canvasTab.Controls.Add(layout);
        var shortcutTab = Translate(new TabPage(), "Shortcuts", "Горячие клавиши");
        var shortcutLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, AutoScroll = true };
        AddWide(shortcutLayout, Translate(new Label { AutoSize = true },
            "Choose a key, optional modifiers and hold time in seconds.\nRelease any required key to cancel. Ctrl+Alt+Delete remains available.",
            "Выберите клавишу, модификаторы и время удержания в секундах.\nОтпускание нужной клавиши отменяет действие. Ctrl+Alt+Delete доступен."));
        AddWide(shortcutLayout, Translate(new Label { AutoSize = true }, "Quit / unlock", "Выход / разблокировка"));
        AddWide(shortcutLayout, exitShortcut);
        AddWide(shortcutLayout, Translate(new Label { AutoSize = true }, "Clear canvas", "Очистка полотна"));
        AddWide(shortcutLayout, clearShortcut);
        AddWide(shortcutLayout, Translate(new Label { AutoSize = true }, "Open settings", "Открыть настройки"));
        AddWide(shortcutLayout, menuShortcut);
        shortcutTab.Controls.Add(shortcutLayout);
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
        background.Text = $"{UiText.Pick(SelectedLanguage, "Choose", "Выбрать")} · #{color.R:X2}{color.G:X2}{color.B:X2}";
        background.UseVisualStyleBackColor = false;
    }

    private static ComboBox Choice(string[] items)
    {
        var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        choice.Items.AddRange(items);
        return choice;
    }

    private static NumericUpDown Number(int min, int max, int step) => new()
    {
        Minimum = min, Maximum = max, Increment = step, Width = 110
    };

    private static CheckBox Toggle() => new() { AutoSize = true, Margin = new Padding(0, 4, 0, 4) };

    private void AddRow(TableLayoutPanel layout, string english, string russian, Control control)
    {
        int row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(Translate(new Label { AutoSize = true, Anchor = AnchorStyles.Left }, english, russian), 0, row);
        control.Margin = new Padding(0, 3, 0, 3);
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
        }
    }
}
