using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace KeyCanvas;

internal sealed class CanvasForm : Form
{
    private readonly InputBuffer input = new();
    private readonly CanvasScene scene = new();
    private readonly CanvasSound sounds = new();
    private HoldGesture exitGesture;
    private HoldGesture clearGesture;
    private HoldGesture settingsGesture;
    private FrameClock? clock;
    private KeyboardHook? keyboard;
    private bool cursorHidden;
    private bool canvasActive;
    private bool exitAllowed;
    private bool settingsOpen;
    private CanvasSettings settings;
    private double startupStarted = InputBuffer.Now;
    private bool hadStartupHints;
    private double previousFrame;
    private double lastMouseTime;
    private Point lastMouse;
    private Rectangle primaryViewport;
    private double nextDesktopCheck;
    private bool inputDesktopActive = true;
    private double nextHookRecovery;
    private bool repaintNeeded = true;
    private bool hadIndicators;
    private readonly double[] frameCosts = new double[120];
    private readonly double[] frameIntervals = new double[120];
    private readonly double[] sortedCosts = new double[120];
    private int sampleCount;
    private int sampleIndex;
    private double nextTimingLabel;
    private string timingLabel = "Measuring…";
    private string fpsLabel = "FPS: …";
    private readonly Font indicatorFont = new("Segoe UI", 10);
    private readonly string canvasDesktop = NativeMethods.DesktopName(
        NativeMethods.GetThreadDesktop(NativeMethods.GetCurrentThreadId()));

    internal CanvasForm(CanvasSettings? initialSettings = null)
    {
        settings = initialSettings ?? SettingsStore.Load(SettingsStore.FilePath);
        exitGesture = new(settings.ExitShortcut.HoldSeconds);
        clearGesture = new(settings.ClearShortcut.HoldSeconds);
        settingsGesture = new(settings.MenuShortcut.HoldSeconds);
        timingLabel = UiText.Pick(settings.Language, "Measuring…", "Замер кадра…");
        Text = "KeyCanvas";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(settings.BackgroundArgb);
        Opacity = settings.TransparentCanvas ? settings.CanvasOpacityPercent / 100.0 : 1;
        scene.ApplySettings(settings);
        DoubleBuffered = true;
        ShowInTaskbar = false;
        TopMost = true;
        UpdateScreens();
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        SystemEvents.SessionSwitch += SessionSwitch;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        startupStarted = InputBuffer.Now;
        keyboard = new KeyboardHook(input, Handle);
        sounds.ApplySettings(settings);
        SetCanvasActive(NativeMethods.GetForegroundWindow() == Handle);
        previousFrame = InputBuffer.Now;
        clock = new FrameClock(Handle, settings.FramesPerSecond);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        SetCanvasActive(true);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        SetCanvasActive(false);
        base.OnDeactivate(e);
    }

    private void SetCanvasActive(bool active)
    {
        active = active && !settingsOpen;
        canvasActive = active;
        if (!active)
            sounds.Stop();
        keyboard?.SetMode(settingsOpen ? KeyboardMode.Menu : active ? KeyboardMode.Canvas : KeyboardMode.Disabled);
        clock?.SetFrameRate(settingsOpen || !active ? 10 : settings.FramesPerSecond);
        scene.ReleaseHeld();
        exitGesture.Update(null, InputBuffer.Now);
        clearGesture.Update(null, InputBuffer.Now);
        settingsGesture.Update(null, InputBuffer.Now);
        // Form.TopMost's setter also activates the window. Preserve the foreground owner.
        nint foreground = NativeMethods.GetForegroundWindow();
        if (IsHandleCreated)
        {
            NativeMethods.SetWindowPos(Handle, active ? -1 : -2, 0, 0, 0, 0, 0x13);
            if (!active && !settingsOpen && foreground != 0 && foreground != Handle &&
                (NativeMethods.GetWindowLong(foreground, -20) & 8) == 0)
                NativeMethods.SetWindowPos(Handle, foreground, 0, 0, 0, 0, 0x13);
        }
        repaintNeeded = true;
        if (active && !cursorHidden)
        {
            Cursor.Hide();
            cursorHidden = true;
        }
        else if (!active && cursorHidden)
        {
            Cursor.Show();
            cursorHidden = false;
        }
    }

    private void Tick()
    {
        double frameStart = InputBuffer.Now;
        keyboard?.Heartbeat();
        if (settingsOpen)
        {
            if (hadStartupHints)
            {
                Invalidate();
                Update();
                hadStartupHints = settings.ShowStartupHints && InputBuffer.Now - startupStarted < StartupHints.Duration;
            }
            return;
        }
        double now = InputBuffer.Now;
        double interval = now - previousFrame;
        double elapsed = Math.Clamp(now - previousFrame, 0, .1);
        previousFrame = now;
        // The secure desktop can hide key releases without sending OnDeactivate.
        if (now >= nextDesktopCheck)
        {
            inputDesktopActive = NativeMethods.IsInputDesktop(canvasDesktop);
            nextDesktopCheck = now + .25;
        }
        bool active = NativeMethods.GetForegroundWindow() == Handle && inputDesktopActive;
        if (active != canvasActive)
            SetCanvasActive(active);
        if (active && keyboard is { IsInstalled: false })
            RecoverKeyboard();
        bool previouslyHadObjects = scene.Objects.Count > 0;
        var frame = input.Read();
        if (exitGesture.Update(settings.ExitShortcut.Started(frame.DownSince), now))
        {
            exitAllowed = true;
            Close();
            return;
        }
        if (settingsGesture.Update(settings.MenuShortcut.Started(frame.DownSince), now))
        {
            OpenSettings();
            return;
        }
        foreach (var press in frame.Presses)
        {
            if ((press.Key is (int)Keys.LShiftKey or (int)Keys.RShiftKey or (int)Keys.ShiftKey) && press.Modifiers.HasFlag(Keys.Alt) ||
                (press.Key is (int)Keys.LMenu or (int)Keys.RMenu or (int)Keys.Menu) && press.Modifiers.HasFlag(Keys.Shift))
            {
                var layouts = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().ToArray();
                int current = Array.FindIndex(layouts, layout => layout.Handle == InputLanguage.CurrentInputLanguage.Handle);
                if (layouts.Length > 1)
                    InputLanguage.CurrentInputLanguage = layouts[(current + 1) % layouts.Length];
            }
            scene.Press(press);
            if (!settings.IsActionKey(press.Key))
                sounds.Play(press.Key, now, held: true);
        }
        sounds.Update(frame.DownSince, now);
        if (clearGesture.Update(settings.ClearShortcut.Started(frame.DownSince), now))
            scene.Clear();
        scene.Update(elapsed, frame.DownSince);
        bool indicators = exitGesture.Progress > 0 || clearGesture.Progress > 0 || settingsGesture.Progress > 0;
        bool startupVisible = settings.ShowStartupHints && now - startupStarted < StartupHints.Duration;
        bool measure = settings.ShowFrameTiming || settings.ShowFps;
        if (repaintNeeded || previouslyHadObjects || scene.Objects.Count > 0 || indicators || hadIndicators || measure || startupVisible || hadStartupHints)
        {
            Invalidate();
            Update(); // Paint directly; coalesced WM_PAINT messages must not halve the frame rate.
            repaintNeeded = false;
            if (measure && canvasActive)
                RecordFrame((InputBuffer.Now - frameStart) * 1000, interval * 1000, now);
        }
        hadIndicators = indicators;
        hadStartupHints = startupVisible;
    }

    private void RecordFrame(double cost, double interval, double now)
    {
        frameCosts[sampleIndex] = cost;
        frameIntervals[sampleIndex] = interval;
        sampleIndex = (sampleIndex + 1) % frameCosts.Length;
        sampleCount = Math.Min(sampleCount + 1, frameCosts.Length);
        if (now < nextTimingLabel)
            return;
        nextTimingLabel = now + 1;
        Array.Copy(frameCosts, sortedCosts, sampleCount);
        Array.Sort(sortedCosts, 0, sampleCount);
        double averageInterval = 0;
        for (int i = 0; i < sampleCount; i++)
            averageInterval += frameIntervals[i];
        averageInterval /= sampleCount;
        fpsLabel = $"FPS: {1000 / averageInterval:F1}";
        timingLabel = UiText.Pick(settings.Language,
            $"CPU frame: {sortedCosts[sampleCount / 2]:F1} ms · p95 {sortedCosts[(int)((sampleCount - 1) * .95)]:F1} ms",
            $"CPU кадра: {sortedCosts[sampleCount / 2]:F1} мс · p95 {sortedCosts[(int)((sampleCount - 1) * .95)]:F1} мс");
    }

    private void RecoverKeyboard()
    {
        double now = InputBuffer.Now;
        if (!canvasActive || settingsOpen || keyboard is null || !keyboard.IsResponsive || now < nextHookRecovery)
            return;
        nextHookRecovery = now + 1;
        keyboard.Dispose();
        keyboard = new KeyboardHook(input, Handle);
        keyboard.SetMode(KeyboardMode.Canvas);
        scene.ReleaseHeld();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        scene.Draw(e.Graphics);
        DrawIndicator(e.Graphics, exitGesture.Progress, 28, UiText.Pick(settings.Language, "Quit", "Выход"));
        DrawIndicator(e.Graphics, clearGesture.Progress, 110, UiText.Pick(settings.Language, "Clear", "Очистка"));
        DrawIndicator(e.Graphics, settingsGesture.Progress, 192, UiText.Pick(settings.Language, "Settings", "Настройки"));
        if (settings.ShowStartupHints)
            StartupHints.Draw(e.Graphics, primaryViewport, settings.Language, InputBuffer.Now - startupStarted, BackColor, settings);
        if (settings.ShowFrameTiming || settings.ShowFps)
        {
            float scale = DeviceDpi / 96f;
            using var background = new SolidBrush(Color.FromArgb(200, 12, 17, 35));
            using var foreground = new SolidBrush(Color.White);
            var box = new RectangleF(primaryViewport.Left + 16 * scale, primaryViewport.Top + 16 * scale,
                340 * scale, (settings.ShowFrameTiming && settings.ShowFps ? 58 : 34) * scale);
            e.Graphics.FillRectangle(background, box);
            string text = settings.ShowFps ? fpsLabel : "";
            if (settings.ShowFrameTiming)
                text += (settings.ShowFps ? "\n" : "") + timingLabel;
            e.Graphics.DrawString(text, indicatorFont, foreground, box.Left + 8 * scale, box.Top + 5 * scale);
        }
    }

    private void DrawIndicator(Graphics graphics, double progress, int offset, string text)
    {
        if (progress <= 0)
            return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        int x = primaryViewport.Right - (int)(82 * scale);
        int y = primaryViewport.Top + (int)(offset * scale);
        Color ink = BackColor.GetBrightness() > .55 ? Color.FromArgb(25, 30, 45) : Color.FromArgb(240, 240, 255);
        using var track = new Pen(Color.FromArgb(55, ink), 4 * scale);
        using var filled = new Pen(ink, 4 * scale);
        using var brush = new SolidBrush(ink);
        graphics.DrawEllipse(track, x, y, 48 * scale, 48 * scale);
        graphics.DrawArc(filled, x, y, 48 * scale, 48 * scale, -90, (float)progress * 360);
        using var format = new StringFormat { Alignment = StringAlignment.Center };
        graphics.DrawString(text, indicatorFont, brush, new RectangleF(x - 20 * scale, y + 54 * scale, 88 * scale, 24 * scale), format);
    }

    private void OpenSettings()
    {
        settingsOpen = true;
        SetCanvasActive(false);
        try
        {
            using var menu = new SettingsForm(settings);
            // Center on the primary display instead of the midpoint between monitors.
            menu.StartPosition = FormStartPosition.Manual;
            var screen = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
            menu.Load += (_, _) =>
            {
                menu.Size = new Size(Math.Min(menu.Width, screen.Width), Math.Min(menu.Height, screen.Height));
                menu.Location = new Point(screen.Left + (screen.Width - menu.Width) / 2,
                    screen.Top + (screen.Height - menu.Height) / 2);
            };
            if (menu.ShowDialog(this) == DialogResult.OK)
            {
                settings = menu.SelectedSettings;
                exitGesture = new(settings.ExitShortcut.HoldSeconds);
                clearGesture = new(settings.ClearShortcut.HoldSeconds);
                settingsGesture = new(settings.MenuShortcut.HoldSeconds);
                sampleCount = sampleIndex = 0;
                nextTimingLabel = 0;
                timingLabel = UiText.Pick(settings.Language, "Measuring…", "Замер кадра…");
                fpsLabel = "FPS: …";
                BackColor = Color.FromArgb(settings.BackgroundArgb);
                Opacity = settings.TransparentCanvas ? settings.CanvasOpacityPercent / 100.0 : 1;
                scene.ApplySettings(settings);
                sounds.ApplySettings(settings);
                repaintNeeded = true;
                clock?.SetFrameRate(settings.FramesPerSecond);
                try
                {
                    SettingsStore.Save(SettingsStore.FilePath, settings);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show(this, UiText.Pick(settings.Language,
                        "Settings could not be saved. They will remain active until you close the application.",
                        "Не удалось сохранить настройки. Они будут действовать до закрытия программы."),
                        "KeyCanvas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        finally
        {
            settingsOpen = false;
            previousFrame = InputBuffer.Now;
            SetCanvasActive(NativeMethods.GetForegroundWindow() == Handle && NativeMethods.IsInputDesktop(canvasDesktop));
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        double now = InputBuffer.Now;
        int distance = Math.Abs(e.X - lastMouse.X) + Math.Abs(e.Y - lastMouse.Y);
        if (NativeMethods.GetForegroundWindow() == Handle && now - lastMouseTime >= .025 && distance >= 4)
        {
            scene.Mouse(e.Location, false);
            repaintNeeded = true;
            lastMouse = e.Location;
            lastMouseTime = now;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!settingsOpen && settings.MouseClicks)
        {
            scene.Mouse(e.Location, true);
            sounds.Play(e.X ^ e.Y, InputBuffer.Now);
            repaintNeeded = true;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!settingsOpen && settings.MouseClicks)
        {
            scene.Mouse(e.Location, true);
            sounds.Play(e.X ^ e.Y, InputBuffer.Now);
            repaintNeeded = true;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !exitAllowed)
            e.Cancel = true;
        else
            SetCanvasActive(false);
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x2E0) // WM_DPICHANGED: keep coverage of the virtual desktop.
        {
            base.WndProc(ref m);
            UpdateScreens();
            return;
        }
        if (m.Msg is 0x100 or 0x104)
            RecoverKeyboard(); // A key reaching the canvas indicates that Windows removed the hook.
        if (m.Msg == FrameClock.FrameMessage)
        {
            if (clock is not null)
            {
                clock.CheckError();
                clock.CompleteFrame();
                Tick();
            }
            return;
        }
        // Suppress ordinary window close while the canvas is active.
        if (m.Msg == 0x112 && NativeMethods.GetForegroundWindow() == Handle &&
            ((long)m.WParam & 0xFFF0) == 0xF060)
            return;
        base.WndProc(ref m);
    }

    private void UpdateScreens()
    {
        Bounds = SystemInformation.VirtualScreen;
        BufferedGraphicsManager.Current.MaximumBuffer = new Size(Width + 1, Height + 1);
        repaintNeeded = true;
        Rectangle Local(Rectangle bounds) => new(bounds.Left - Left, bounds.Top - Top, bounds.Width, bounds.Height);
        primaryViewport = Local((Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds);
        scene.SetViewports(Screen.AllScreens.Select(screen => Local(screen.Bounds)).ToArray(), primaryViewport);
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated && !IsDisposed)
            BeginInvoke(UpdateScreens);
    }

    private void SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or
            SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() => SetCanvasActive(false));
        }
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or
                 SessionSwitchReason.RemoteConnect)
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() => SetCanvasActive(NativeMethods.GetForegroundWindow() == Handle));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            keyboard?.Dispose();
            keyboard = null;
            sounds.Dispose();
            clock?.Dispose();
            clock = null;
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            SystemEvents.SessionSwitch -= SessionSwitch;
            if (cursorHidden)
            {
                Cursor.Show();
                cursorHidden = false;
            }
            indicatorFont.Dispose();
            Icon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
