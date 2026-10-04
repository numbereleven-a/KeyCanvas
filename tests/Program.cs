using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Media;
using System.Text;
using KeyCanvas;
using ColorPalette = KeyCanvas.ColorPalette;

internal static class Program
{
    private static readonly HashSet<Keys> syntheticHeldKeys = new();
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            if (args.Length == 2 && args[0] == "--screenshots")
            {
                CaptureScreenshots(args[1]);
                Console.WriteLine("Passed: actual English canvas and settings screenshots.");
                return 0;
            }
            if (args.Length == 2 && args[0] == "--release-check")
            {
                TestRelease(args[1]);
                Console.WriteLine("Passed: portable executable, F12 settings, menu cancellation and five-second Esc exit.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "--window-benchmark")
            {
                BenchmarkWindow();
                return 0;
            }
            if (args.Length == 1 && args[0] == "--menu-check")
            {
                TestMenuInput();
                Console.WriteLine("Passed: F12 timing, native color dialog, blocked menu shortcuts, Alt+F4 releases, window focus, freeze fail-open and capture recovery.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "--benchmark")
            {
                Benchmark();
                return 0;
            }
            if (args.Length == 1 && args[0] == "--new-modes-check")
            {
                TestNewModesWindow();
                Console.WriteLine("Passed: desktop transparency, shortcut tab, custom Ctrl+F2 menu and Ctrl+F1 exit.");
                return 0;
            }
            TestInputAndGestures();
            TestKeyboardPolicy();
            TestScene();
            TestHookLifetime();
            TestFrameClock();
            TestSettings();
            TestLocalization();
            TestSound();
            TestNewModes();
            Render(args.Length == 2 && args[0] == "--render" ? args[1] : null);
            Console.WriteLine("Passed: input transitions, timed gestures, bounded scene, rendering, hook lifetime, frame clock, settings.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static void Check(bool success, string message)
    {
        if (!success)
            throw new InvalidOperationException(message);
    }

    private static void TestRelease(string executable)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false })!;
        try
        {
            Check(process.WaitForInputIdle(5000), "The portable application must reach its message loop.");
            nint canvas = 0;
            uint owner = 0;
            Check(SpinWait.SpinUntil(() =>
            {
                canvas = NativeMethods.FindWindow(null, "KeyCanvas");
                NativeMethods.GetWindowThreadProcessId(canvas, out owner);
                return owner == process.Id && IsWindowVisible(canvas) || process.HasExited;
            }, 5000) && owner == process.Id && IsWindowVisible(canvas), "The portable test must own the canvas it operates.");
            Thread.Sleep(200);
            if (NativeMethods.GetForegroundWindow() != canvas)
            {
                SendKey(Keys.F24, true);
                SendKey(Keys.F24, false);
                NativeMethods.SetForegroundWindow(canvas);
            }
            Check(NativeMethods.GetForegroundWindow() == canvas, "The portable canvas must have focus.");
            SendKey(Keys.A, true);
            SendKey(Keys.A, false);
            Thread.Sleep(100);
            Check(!process.HasExited, "The portable build must handle a visual and sound-producing key press.");
            SendKey(Keys.F12, true);
            Thread.Sleep(2400);
            SendKey(Keys.F12, false);
            nint menu = 0;
            string title = UiText.SettingsTitle(SettingsStore.Load(SettingsStore.FilePath).Language);
            Check(SpinWait.SpinUntil(() =>
            {
                menu = NativeMethods.FindWindow(null, title);
                NativeMethods.GetWindowThreadProcessId(menu, out owner);
                return owner == process.Id && IsWindowVisible(menu) && NativeMethods.GetForegroundWindow() == menu;
            }, 5000), "Portable F12 must show and activate its own settings.");
            SendKey(Keys.Escape, true);
            SendKey(Keys.Escape, false);
            Thread.Sleep(300);
            Check(NativeMethods.GetForegroundWindow() == canvas, "Cancel must return to the portable canvas.");
            SendKey(Keys.Escape, true);
            Check(process.WaitForExit(6500) && process.ExitCode == 0, "Five-second Esc must close the portable application cleanly.");
            SendKey(Keys.Escape, false);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(2000);
            }
            foreach (Keys key in syntheticHeldKeys.ToArray())
                SendKey(key, false);
        }
    }

    private static void BenchmarkWindow()
    {
        foreach (bool largeFigures in new[] { false, true })
        foreach (bool persistentBuffer in new[] { false, true })
        {
            using var window = new WindowBenchmark(largeFigures);
            BufferedGraphicsManager.Current.MaximumBuffer = persistentBuffer ? new Size(1921, 1081) : new Size(225, 96);
            window.Show();
            Application.DoEvents();
            var times = new List<double>();
            int samples = largeFigures ? 20 : 120;
            for (int i = 0; i < samples + 10; i++)
            {
                long start = Stopwatch.GetTimestamp();
                window.Invalidate();
                window.Update();
                if (i >= 10)
                    times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            Check(window.PaintCount >= samples + 10, "The window benchmark must execute actual paint events.");
            Console.WriteLine($"Window paint, {(largeFigures ? "1000 figures, radius 170, size 200%" : "800 objects")}, {window.ClientSize.Width}x{window.ClientSize.Height}, persistent buffer={persistentBuffer}: median {times.Order().ElementAt(samples / 2):F2} ms, p95 {times.Order().ElementAt((int)(samples * .95)):F2} ms.");
        }
    }

    private sealed class WindowBenchmark : Form
    {
        private readonly CanvasScene scene = new(new Random(7));
        internal int PaintCount { get; private set; }
        protected override bool ShowWithoutActivation => true;
        internal WindowBenchmark(bool largeFigures)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds.Location;
            ClientSize = new Size(1920, 1080);
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(12, 17, 35);
            DoubleBuffered = true;
            scene.SetViewports([ClientRectangle], ClientRectangle);
            scene.ApplySettings(new() { ObjectLimit = 800, FigureLifetimeSeconds = 25 });
            if (largeFigures)
                scene.ApplySettings(new() { ObjectLimit = 1000, ParticleAmountPercent = 0, ShapeSizePercent = 200 });
            for (int i = 0; i < (largeFigures ? 1000 : 100); i++)
                scene.Press(new KeyPress((int)Keys.A, i));
            if (largeFigures)
                foreach (var shape in scene.Objects)
                    shape.Radius = 170;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintCount++;
            scene.Draw(e.Graphics);
        }
    }

    private static void TestInputAndGestures()
    {
        var input = new InputBuffer();
        input.Record((int)Keys.A, true, 10);
        input.Record((int)Keys.A, true, 11);
        var frame = input.Read();
        Check(frame.Presses.Length == 1 && frame.DownSince[(int)Keys.A] == 10, "Autorepeat must preserve the original hold.");
        input.Record((int)Keys.A, false, 12);
        input.Record((int)Keys.A, true, 13);
        Check(input.Read().Presses.Single().Time == 13, "A second press must create a new object.");

        var exit = new HoldGesture(5);
        Check(!exit.Update(10, 14.999) && exit.Progress < 1, "Exit must wait five seconds.");
        Check(!exit.Update(null, 15), "Early release must cancel exit.");
        Check(!exit.Update(16, 20.999), "A new hold must start from zero.");
        Check(exit.Update(16, 21), "Five seconds must trigger exit.");
        Check(!exit.Update(16, 25), "A hold must trigger only once.");
        var clear = new HoldGesture(3);
        Check(!clear.Update(1, 3.999) && clear.Update(1, 4), "Clear must take three seconds.");
        Check(!clear.Update(1, 5) && !clear.Update(null, 5) && clear.Update(6, 9), "Clear must rearm only for a new hold.");
        var menu = new HoldGesture(2);
        Check(!menu.Update(1, 2.999) && !menu.Update(null, 3), "Early F12 release must cancel settings.");
        Check(menu.Update(4, 6) && !menu.Update(4, 7), "F12 must open settings once after two seconds.");

        for (int i = 0; i < 1000; i++)
        {
            input.Record((int)Keys.B, true, i);
            input.Record((int)Keys.B, false, i);
        }
        Check(input.Read().Presses.Length == InputBuffer.MaxPendingPresses, "Input backlog must be bounded.");
        input.Reset();
        frame = input.Read();
        Check(frame.Presses.Length == 0 && frame.DownSince.All(time => !time.HasValue), "Focus loss must reset input.");
    }

    private static void TestKeyboardPolicy()
    {
        var policy = new KeyboardPolicy();
        Check(policy.Suppress(KeyboardMode.Menu, (int)Keys.LWin, true, false), "Menu must suppress Win.");
        Check(policy.Suppress(KeyboardMode.Menu, (int)Keys.LWin, false, false), "Menu must suppress the Win release.");
        Check(policy.Suppress(KeyboardMode.Menu, (int)Keys.Tab, true, true), "Menu must suppress Alt+Tab.");
        Check(policy.Suppress(KeyboardMode.Menu, (int)Keys.Escape, true, true), "Menu must suppress Alt+Esc.");
        Check(!policy.Suppress(KeyboardMode.Menu, (int)Keys.LControlKey, true, false), "Menu controls need Ctrl.");
        Check(policy.Suppress(KeyboardMode.Menu, (int)Keys.Escape, true, false), "Menu must suppress Ctrl+Esc.");
        Check(!policy.Suppress(KeyboardMode.Canvas, (int)Keys.LControlKey, false, false), "A permitted modifier must be released across a mode transition.");
        Check(!policy.Suppress(KeyboardMode.Menu, (int)Keys.F4, true, true), "Alt+F4 must close the menu.");
        Check(!policy.Suppress(KeyboardMode.Canvas, (int)Keys.F4, false, true), "Alt+F4 release must survive the menu closing.");
        Check(!policy.Suppress(KeyboardMode.Menu, (int)Keys.Tab, true, false), "Tab must navigate the menu.");
        Check(!policy.Suppress(KeyboardMode.Menu, (int)Keys.Enter, true, false), "Enter must apply settings.");
        Check(!policy.Suppress(KeyboardMode.Menu, (int)Keys.Escape, true, false), "Escape alone must cancel the menu.");
        policy.Seed((int)Keys.LShiftKey, true);
        Check(!policy.Suppress(KeyboardMode.Canvas, (int)Keys.LShiftKey, false, false), "Keys held before installing the hook must be released.");
        policy.Seed((int)Keys.ControlKey, true);
        policy.Seed((int)Keys.LControlKey, true);
        policy.Suppress(KeyboardMode.Menu, (int)Keys.LControlKey, false, false);
        Check(!policy.Suppress(KeyboardMode.Menu, (int)Keys.Escape, true, false),
            "Seeding a held Ctrl must not leave its generic state stuck after the side-specific release.");
        Check(policy.Suppress(KeyboardMode.Canvas, (int)Keys.A, true, false), "Canvas must capture ordinary keys.");
        Check(!policy.Suppress(KeyboardMode.Disabled, (int)Keys.A, true, false), "Suspended capture must pass ordinary keys.");
        Check(!policy.Suppress(KeyboardMode.Canvas, (int)Keys.A, false, false), "A passed autorepeat requires a passed release.");
    }

    private static void TestScene()
    {
        var scene = new CanvasScene(new Random(7));
        scene.Press(new KeyPress((int)Keys.Escape, 0));
        scene.Press(new KeyPress((int)Keys.F1, 0));
        scene.Press(new KeyPress((int)Keys.F12, 0));
        Check(scene.Objects.Count == 0, "Parent controls must not draw objects.");
        scene.Press(new KeyPress((int)Keys.A, 1));
        var shape = scene.Objects[0];
        float radius = shape.Radius;
        var held = new double?[256];
        held[(int)Keys.A] = 1;
        scene.Update(.5, held);
        Check(shape.Radius > radius, "Holding a key must grow its shape.");
        held[(int)Keys.A] = null;
        radius = shape.Radius;
        scene.Update(.5, held);
        Check(shape.Radius == radius, "Releasing a key must stop growth.");
        scene.Press(new KeyPress((int)Keys.Space, 2));
        Check(scene.Objects.Any(o => o.Kind == ShapeKind.Ring && o.Position == new PointF(640, 360)), "Space must create a central wave.");
        var wave = scene.Objects.First(o => o.Kind == ShapeKind.Ring);
        wave.Radius = 200;
        held[(int)Keys.Space] = 2;
        scene.Update(.1, held);
        Check(wave.Radius > 200, "Holding Space must not shrink an expanding wave.");

        var arrows = new CanvasScene(new Random(1));
        arrows.Press(new KeyPress((int)Keys.Left, 0));
        Check(arrows.Objects.All(o => o.Velocity.X < 0), "Left arrow must emit leftward particles.");
        int count = arrows.Objects.Count;
        held[(int)Keys.Left] = 0;
        arrows.Update(.1, held);
        Check(arrows.Objects.Count > count, "Held arrows must continue their stream.");

        for (int i = 0; i < 3000; i++)
            scene.Mouse(new PointF(i % 1280, i % 720), true);
        Check(scene.Objects.Count <= scene.Settings.ObjectLimit, "Long play must not grow the scene without limit.");
        Check(scene.Objects.Any(o => o.FadeRemaining.HasValue), "Old objects must begin fading before eviction.");
        scene.Clear();
        scene.Update(.7, new double?[256]);
        Check(scene.Objects.Count == 0, "Animated clear must finish.");

        scene.SetViewports([new Rectangle(100, 200, 500, 400)], new Rectangle(100, 200, 500, 400));
        scene.Press(new KeyPress((int)Keys.Space, 10));
        Check(scene.Objects[0].Position == new PointF(350, 400), "Center must follow the primary monitor.");
        scene.Update(40, new double?[256]);
        Check(scene.Objects.Count == 0, "Expired objects must be removed.");
    }

    private static void TestHookLifetime()
    {
        // Install a real Windows hook with interception disabled: no desktop input is affected.
        var input = new InputBuffer();
        var hook = new KeyboardHook(input, 0);
        Check(hook.IsInstalled, "Windows hook installation failed.");
        hook.Dispose();
        Check(!hook.IsInstalled, "Windows hook must be removed on disposal.");
        hook.Dispose();
        using var second = new KeyboardHook(input, 0);
        Check(second.IsInstalled, "Hook must be installable again after cleanup.");
        string desktop = NativeMethods.DesktopName(NativeMethods.GetThreadDesktop(NativeMethods.GetCurrentThreadId()));
        Check(desktop.Length > 0, "Canvas desktop name must be readable.");
        Check(!NativeMethods.IsInputDesktop(""), "Unreadable desktop must suspend child input.");
    }

    private static void Render(string? path)
    {
        var scene = new CanvasScene(new Random(19));
        for (int i = 0; i < 22; i++)
            scene.Press(new KeyPress((int)Keys.A + i, i * .15));
        scene.Press(new KeyPress((int)Keys.Space, 4));
        scene.Update(.3, new double?[256]);
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(12, 17, 35));
        scene.Draw(graphics);
        bool painted = false;
        for (int y = 0; y < bitmap.Height && !painted; y += 16)
            for (int x = 0; x < bitmap.Width && !painted; x += 16)
                painted = bitmap.GetPixel(x, y).ToArgb() != Color.FromArgb(12, 17, 35).ToArgb();
        Check(painted, "Shapes and particles must render into the canvas.");
        if (path is not null)
        {
            bitmap.Save(path, ImageFormat.Png);
            using var menu = new SettingsForm(new());
            using var host = ShowPreview(menu);
            using var menuBitmap = new Bitmap(menu.Width, menu.Height);
            menu.DrawToBitmap(menuBitmap, new Rectangle(0, 0, menu.Width, menu.Height));
            menuBitmap.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "settings-preview.png"), ImageFormat.Png);
            host.Hide();
        }
    }

    private static void TestSettings()
    {
        var selected = new CanvasSettings
        {
            Language = AppLanguage.English,
            BackgroundArgb = Color.FromArgb(44, 36, 80).ToArgb(), Figures = FigureStyle.Star,
            Palette = ColorPalette.Cool, ShapeSizePercent = 150, AnimationSpeedPercent = 50,
            ParticleAmountPercent = 0, ObjectLimit = 100, FigureLifetimeSeconds = 10, FramesPerSecond = 120,
            MouseTrail = false, MouseClicks = false, GrowWhileHeld = false, ReactToRhythm = false, ShowFrameTiming = true, ShowFps = true,
            ShowStartupHints = true, SoundsEnabled = false, Sound = SoundStyle.Xylophone, SoundVolumePercent = 35
        };
        using (var menu = new SettingsForm(selected))
        {
            Check(menu.SelectedSettings == selected, "Menu must preserve every preference until applied.");
            using var host = ShowPreview(menu);
            var buttons = menu.Controls.OfType<FlowLayoutPanel>().Single();
            buttons.Controls.OfType<Button>().Single(button => button.Name == "Reset").PerformClick();
            Check(menu.SelectedSettings == new CanvasSettings { Language = selected.Language }, "Reset must restore visual defaults and preserve the chosen language.");
            menu.Controls.Find("OptionsLayout", true).Single().Controls.OfType<Button>().Single(button => button.Name == "PreviewSound").PerformClick();
            Check(selected.Figures == FigureStyle.Star, "Editing the menu must not change the active settings before applying.");
        }

        string directory = Path.Combine(Path.GetTempPath(), "KeyCanvas.Tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        try
        {
            Check(SettingsStore.Load(path) == new CanvasSettings(), "First launch must use defaults.");
            SettingsStore.Save(path, selected);
            Check(SettingsStore.Load(path) == selected, "Settings must survive a save/load cycle.");
            CultureInfo original = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                Check(SettingsStore.Load(path).Language == AppLanguage.English,
                    "A saved language choice must take priority over the current Windows UI language.");
            }
            finally { CultureInfo.CurrentUICulture = original; }
            SettingsStore.Save(path, selected with { FramesPerSecond = 60 });
            Check(SettingsStore.Load(path).FramesPerSecond == 60 && !File.Exists(path + ".tmp"),
                "Replacing preferences must preserve valid JSON and remove the temporary file.");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool failed = false;
                try { SettingsStore.Save(path, selected); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failed = true; }
                Check(failed && SettingsStore.Load(path).FramesPerSecond == 60,
                    "A failed replacement must preserve the previous settings.");
            }
            Check(!File.Exists(path + ".tmp"), "Failed saves must remove their temporary file.");
            File.WriteAllText(path, "{");
            Check(SettingsStore.Load(path) == new CanvasSettings(), "Damaged settings must not block startup.");
            var bounded = (selected with { ObjectLimit = -1, AnimationSpeedPercent = 0, FramesPerSecond = 999 }).Normalize();
            Check(bounded.ObjectLimit == 100 && bounded.AnimationSpeedPercent == 25 && bounded.FramesPerSecond == 60,
                "Invalid stored values must be constrained before reaching the UI or frame clock.");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }

        var scene = new CanvasScene(new Random(7));
        for (int i = 0; i < 80; i++)
            scene.Press(new KeyPress((int)Keys.A, i));
        scene.ApplySettings(selected);
        Check(scene.Objects.Count == 100, "Reducing the object limit must apply to the existing canvas.");
        scene.Clear();
        scene.Update(1, new double?[256]);
        scene.Press(new KeyPress((int)Keys.A, 100));
        Check(scene.Objects.Count == 1 && scene.Objects[0].Kind == ShapeKind.Star,
            "Selected figures and disabled particles must affect keyboard drawing.");
        var shape = scene.Objects[0];
        Color originalColor = shape.Color;
        scene.ApplySettings(selected with { FramesPerSecond = 30 });
        Check(shape.Color == originalColor, "Changing only FPS must preserve existing colors.");
        float radius = shape.Radius;
        PointF position = shape.Position;
        var held = new double?[256];
        held[(int)Keys.A] = 100;
        scene.Update(1, held);
        Check(shape.Radius == radius, "Disabled hold growth must stop size changes.");
        Check(Math.Abs(shape.Position.X - position.X - shape.Velocity.X * .5f) < .001f,
            "Animation speed must change motion while preserving real-time gestures.");
        scene.Mouse(new PointF(100, 100), false);
        scene.Mouse(new PointF(100, 100), true);
        Check(scene.Objects.Count == 1, "Disabled mouse options must stop mouse drawing.");
    }

    private static void Benchmark()
    {
        var scene = new CanvasScene(new Random(19));
        scene.ApplySettings(new() { ObjectLimit = 800, FigureLifetimeSeconds = 25 });
        scene.SetViewports([new Rectangle(0, 0, 1920, 1080)], new Rectangle(0, 0, 1920, 1080));
        for (int i = 0; i < 100; i++)
            scene.Press(new KeyPress((int)Keys.A + i % 26, i * .15));
        using var bitmap = new Bitmap(1920, 1080);
        using var graphics = Graphics.FromImage(bitmap);
        var times = new List<double>();
        for (int i = 0; i < 130; i++)
        {
            var start = Stopwatch.GetTimestamp();
            graphics.Clear(Color.FromArgb(12, 17, 35));
            scene.Draw(graphics);
            if (i >= 10)
                times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        times.Sort();
        Console.WriteLine($"1920x1080, {scene.Objects.Count} objects: draw median {times[times.Count / 2]:F2} ms, p95 {times[(int)(times.Count * .95)]:F2} ms.");

        using var context = new ApplicationContext();
        using var timer = new System.Windows.Forms.Timer { Interval = 16 };
        var intervals = new List<double>();
        long previous = Stopwatch.GetTimestamp();
        timer.Tick += (_, _) =>
        {
            long now = Stopwatch.GetTimestamp();
            intervals.Add(Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
            previous = now;
            if (intervals.Count == 120)
                context.ExitThread();
        };
        timer.Start();
        Application.Run(context);
        timer.Stop();
        intervals.RemoveAt(0);
        Console.WriteLine($"UI timer, no drawing: {1000 / intervals.Average():F1} ticks/s, median {intervals.Order().ElementAt(intervals.Count / 2):F2} ms.");

        var frames = new List<double>();
        previous = Stopwatch.GetTimestamp();
        using var renderContext = new ApplicationContext();
        using var probe = new FrameProbe(() =>
        {
            long now = Stopwatch.GetTimestamp();
            frames.Add(Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
            previous = now;
            graphics.Clear(Color.FromArgb(12, 17, 35));
            scene.Draw(graphics);
            if (frames.Count == 130)
                renderContext.ExitThread();
        });
        Application.Run(renderContext);
        frames.RemoveRange(0, 10);
        Console.WriteLine($"Frame clock with 800-object drawing: {1000 / frames.Average():F1} frames/s, median {frames.Order().ElementAt(frames.Count / 2):F2} ms, p95 {frames.Order().ElementAt((int)(frames.Count * .95)):F2} ms.");
    }

    private static void TestLocalization()
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
            Check(new CanvasSettings().Language == AppLanguage.Russian, "Russian Windows UI must select Russian on first launch.");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var defaults = new CanvasSettings();
            Check(defaults.Language == AppLanguage.English && defaults.ObjectLimit == 500 &&
                defaults.FigureLifetimeSeconds == 5 && !defaults.ShowFps && !defaults.ShowFrameTiming && defaults.ShowStartupHints &&
                defaults.SoundsEnabled && defaults.Sound == SoundStyle.Bells && defaults.SoundVolumePercent == 15,
                "Other Windows languages must use English with the requested visual and FPS defaults.");
        }
        finally { CultureInfo.CurrentUICulture = original; }

        using var menu = new SettingsForm(new() { Language = AppLanguage.English, Figures = FigureStyle.Star, Palette = ColorPalette.Cool });
        var selector = menu.Controls.Find("OptionsLayout", true).Single().Controls.OfType<ComboBox>().Single(control => control.Name == "Language");
        selector.SelectedIndex = 1;
        Check(menu.Text == UiText.SettingsTitle(AppLanguage.Russian) && menu.SelectedSettings.Language == AppLanguage.Russian &&
            menu.SelectedSettings.Figures == FigureStyle.Star && menu.SelectedSettings.Palette == ColorPalette.Cool,
            "The language selector must immediately translate the menu without resetting other selections.");
        selector.SelectedIndex = 0;
        Check(menu.Text == UiText.SettingsTitle(AppLanguage.English), "The menu must switch back to English.");
        Check(StartupHints.Opacity(0) == 1 && StartupHints.Opacity(4.5) is > 0 and < 1 && StartupHints.Opacity(5) == 0,
            "Startup hints must fade and be gone at five seconds.");
        var scene = new CanvasScene(new Random(7));
        scene.ApplySettings(new() { ParticleAmountPercent = 0 });
        scene.Press(new KeyPress((int)Keys.A, 0));
        scene.Press(new KeyPress((int)Keys.Space, 0));
        Check(scene.Objects.All(shape => shape.Lifetime == 5), "Regular shapes and waves must live exactly five seconds by default.");
        scene.Update(4.99, new double?[256]);
        Check(scene.Objects.Count == 2, "Shapes must remain until the requested lifetime ends.");
        scene.Update(.02, new double?[256]);
        Check(scene.Objects.Count == 0, "Shapes must expire at the requested lifetime.");
    }

    private static void CaptureScreenshots(string directory)
    {
        Directory.CreateDirectory(directory);
        using var canvas = new CanvasForm(new() { Language = AppLanguage.English, ShowStartupHints = true });
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        var elapsed = Stopwatch.StartNew();
        int stage = 0;
        timer.Tick += (_, _) =>
        {
            switch (stage)
            {
                case 0:
                    if (NativeMethods.GetForegroundWindow() != canvas.Handle)
                    {
                        SendKey(Keys.F24, true);
                        SendKey(Keys.F24, false);
                        canvas.Activate();
                        break;
                    }
                    elapsed.Restart();
                    stage = 1;
                    break;
                case 1:
                    if (elapsed.Elapsed.TotalSeconds < .4)
                        break;
                    SaveScreenshot(canvas, Path.Combine(directory, "startup.png"));
                    stage = 2;
                    break;
                case 2:
                    if (elapsed.Elapsed.TotalSeconds < 5.2)
                        break;
                    for (int i = 0; i < 26; i++)
                    {
                        SendKey(Keys.A + i, true);
                        SendKey(Keys.A + i, false);
                    }
                    SendKey(Keys.Space, true);
                    SendKey(Keys.Space, false);
                    elapsed.Restart();
                    stage = 3;
                    break;
                case 3:
                    if (elapsed.Elapsed.TotalSeconds < .3)
                        break;
                    SaveScreenshot(canvas, Path.Combine(directory, "canvas.png"));
                    SendKey(Keys.F12, true);
                    elapsed.Restart();
                    stage = 4;
                    break;
                case 4:
                    if (elapsed.Elapsed.TotalSeconds < 2.3)
                        break;
                    var menu = canvas.OwnedForms.OfType<SettingsForm>().Single();
                    SendKey(Keys.F12, false);
                    Check(menu.Text == UiText.SettingsTitle(AppLanguage.English), "Documentation screenshots must use English.");
                    SaveScreenshot(menu, Path.Combine(directory, "settings.png"));
                    menu.Controls.Find("OptionsLayout", true).Single().Controls.OfType<ComboBox>().Single(control => control.Name == "Language").SelectedIndex = 1;
                    SaveScreenshot(menu, Path.Combine(directory, "settings-ru.png"));
                    menu.Controls.OfType<TabControl>().Single().SelectedIndex = 1;
                    SaveScreenshot(menu, Path.Combine(directory, "shortcuts-ru.png"));
                    menu.Controls.Find("OptionsLayout", true).Single().Controls.OfType<ComboBox>().Single(control => control.Name == "Language").SelectedIndex = 0;
                    SaveScreenshot(menu, Path.Combine(directory, "shortcuts.png"));
                    menu.DialogResult = DialogResult.Cancel;
                    stage = 5;
                    break;
                case 5:
                    timer.Stop();
                    canvas.Dispose();
                    Application.ExitThread();
                    stage = 6;
                    break;
            }
        };
        try
        {
            timer.Start();
            Application.Run(canvas);
            Check(stage == 6, "Screenshot capture must finish cleanly.");
        }
        finally
        {
            timer.Stop();
            foreach (Keys key in syntheticHeldKeys.ToArray())
                SendKey(key, false);
        }
    }

    private static void TestNewModesWindow()
    {
        using var desktop = new Form { FormBorderStyle = FormBorderStyle.None, Bounds = SystemInformation.VirtualScreen,
            StartPosition = FormStartPosition.Manual, BackColor = Color.White, ShowInTaskbar = false };
        using var canvas = new CanvasForm(new()
        {
            AlphabetMode = true, TransparentCanvas = true, CanvasOpacityPercent = 40, BackgroundArgb = Color.Black.ToArgb(),
            ShowStartupHints = false, SoundsEnabled = false,
            ExitShortcut = new() { Key = Keys.F1, Modifiers = Keys.Control, HoldSeconds = 1 },
            MenuShortcut = new() { Key = Keys.F2, Modifiers = Keys.Control, HoldSeconds = .5 }
        });
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        var elapsed = Stopwatch.StartNew();
        Exception? failure = null;
        int stage = 0;
        timer.Tick += (_, _) =>
        {
            try
            {
                double now = elapsed.Elapsed.TotalSeconds;
                if (now > 8)
                    throw new InvalidOperationException("Custom shortcut window check timed out.");
                switch (stage)
                {
                    case 0:
                        if (NativeMethods.GetForegroundWindow() != canvas.Handle)
                        {
                            SendKey(Keys.F24, true); SendKey(Keys.F24, false); canvas.Activate();
                            break;
                        }
                        elapsed.Restart(); stage++; break;
                    case 1 when now > .4:
                        var primary = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds;
                        using (var pixel = new Bitmap(1, 1))
                        {
                            using var graphics = Graphics.FromImage(pixel);
                            graphics.CopyFromScreen(primary.Left + 20, primary.Bottom - 20, 0, 0, new Size(1, 1));
                            Color color = pixel.GetPixel(0, 0);
                            Check(color.R is > 140 and < 170 && color.G is > 140 and < 170,
                                "The live desktop must be visible through the black 40% canvas.");
                        }
                        SendKey(Keys.A, true); SendKey(Keys.A, false);
                        SendKey(Keys.LControlKey, true); SendKey(Keys.F2, true);
                        elapsed.Restart(); stage++; break;
                    case 2 when canvas.OwnedForms.OfType<SettingsForm>().Any():
                        Check(now >= .45, "Custom menu shortcut must respect its hold duration.");
                        SendKey(Keys.F2, false); SendKey(Keys.LControlKey, false);
                        var menu = canvas.OwnedForms.OfType<SettingsForm>().Single();
                        var tabs = menu.Controls.OfType<TabControl>().Single();
                        Check(tabs.TabPages.Count == 2, "Settings must have a separate shortcut tab.");
                        tabs.SelectedIndex = 1;
                        Check(menu.Controls.OfType<FlowLayoutPanel>().Single().Visible, "Apply buttons must remain visible on the shortcut tab.");
                        stage++; elapsed.Restart();
                        menu.DialogResult = DialogResult.Cancel;
                        break;
                    case 3 when now > .3:
                        Check(NativeMethods.GetForegroundWindow() == canvas.Handle, "Cancelling settings must restore canvas focus.");
                        SendKey(Keys.LControlKey, true); SendKey(Keys.F1, true);
                        elapsed.Restart(); stage++; break;
                }
            }
            catch (Exception error)
            {
                failure = error; timer.Stop(); canvas.Dispose(); Application.ExitThread();
            }
        };
        canvas.FormClosed += (_, _) =>
        {
            if (stage != 4 || elapsed.Elapsed.TotalSeconds < .9)
                failure ??= new InvalidOperationException("The custom exit shortcut ended before its full hold.");
            timer.Stop(); Application.ExitThread();
        };
        try { desktop.Show(); timer.Start(); Application.Run(canvas); }
        finally { foreach (var key in syntheticHeldKeys.ToArray()) SendKey(key, false); }
        if (failure is not null) throw failure;
    }
    private static void TestNewModes()
    {
        var selected = new CanvasSettings
        {
            AlphabetMode = true, TransparentCanvas = true, CanvasOpacityPercent = 40,
            Sound = SoundStyle.Synthesizer,
            ExitShortcut = new() { Key = Keys.F1, Modifiers = Keys.Control, HoldSeconds = 1.5 }
        };
        using (var menu = new SettingsForm(selected))
            Check(menu.SelectedSettings == selected, "New modes and action shortcuts must survive menu binding.");
        var held = new double?[256];
        held[(int)Keys.F1] = 1;
        Check(selected.ExitShortcut.Started(held) is null, "A shortcut must require every selected modifier.");
        held[(int)Keys.RControlKey] = 2;
        Check(selected.ExitShortcut.Started(held) == 2, "Timing must begin when the full chord is held.");
        var gesture = new HoldGesture(1.5);
        Check(!gesture.Update(selected.ExitShortcut.Started(held), 3) && gesture.Update(selected.ExitShortcut.Started(held), 3.5),
            "Custom duration must apply to the full chord.");
        held[(int)Keys.RControlKey] = null;
        Check(!gesture.Update(selected.ExitShortcut.Started(held), 4) && gesture.Progress == 0,
            "Releasing a required modifier must cancel the action.");
        held[(int)Keys.F1] = 5;
        held[(int)Keys.LControlKey] = 5;
        Check(selected.ClearShortcut.Started(held) is null, "Ctrl+F1 must not also trigger plain F1.");
        var scene = new CanvasScene(new Random(8));
        scene.ApplySettings(selected with { ParticleAmountPercent = 0 });
        scene.Press(new((int)Keys.A, 0, "ф"));
        Check(scene.Objects.Single().Kind == ShapeKind.Letter && scene.Objects.Single().Label == "ф",
            "Alphabet mode must retain the translated key label.");
        using var bitmap = new Bitmap(1280, 720);
        using var graphics = Graphics.FromImage(bitmap);
        scene.Draw(graphics);
        using (var canvas = new CanvasForm(selected))
            Check(Math.Abs(canvas.Opacity - .4) < .001, "Transparent mode must apply the chosen opacity.");
        Check((selected with { CanvasOpacityPercent = 0 }).Normalize().CanvasOpacityPercent == 10,
            "The canvas must remain visible and receive pointer input.");
        foreach (InputLanguage layout in InputLanguage.InstalledInputLanguages)
        {
            string label = KeyLabels.Translate((int)Keys.A, new byte[256], layout.Handle);
            if (layout.Culture.TwoLetterISOLanguageName == "ru")
                Check(label == "ф", "Russian layout must translate A to the printed Russian letter.");
            if (layout.Culture.TwoLetterISOLanguageName == "en")
                Check(label == "a", "English layout must translate A to a.");
        }
        string path = Path.Combine(Path.GetTempPath(), "KeyCanvas-settings-" + Guid.NewGuid() + ".json");
        try
        {
            SettingsStore.Save(path, selected);
            Check(SettingsStore.Load(path) == selected, "New modes and shortcut timings must persist.");
        }
        finally { File.Delete(path); }
    }
    private static void TestSound()
    {
        Check(CanvasSound.ProgramFor(SoundStyle.Piano) == 0 && CanvasSound.ProgramFor(SoundStyle.Synthesizer) == 80,
            "Piano and synthesizer must select their General MIDI instruments.");
        using var audio = new CanvasSound();
        audio.ApplySettings(new() { Sound = SoundStyle.Piano });
        var held = new double?[256];
        held[(int)Keys.A] = held[(int)Keys.S] = InputBuffer.Now;
        audio.Play((int)Keys.A, InputBuffer.Now, true);
        audio.Play((int)Keys.S, InputBuffer.Now, true);
        audio.Update(held, InputBuffer.Now);
        Array.Clear(held);
        audio.Update(held, InputBuffer.Now);
        audio.ApplySettings(new() { SoundsEnabled = false });
        audio.Play((int)Keys.A, InputBuffer.Now);
        var normalized = (new CanvasSettings { Sound = (SoundStyle)99, SoundVolumePercent = 999 }).Normalize();
        Check(normalized.Sound == SoundStyle.Bells && normalized.SoundVolumePercent == 100,
            "Stored sound preferences must be constrained before playback.");
    }
    private static void SaveScreenshot(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void TestFrameClock()
    {
        using var context = new ApplicationContext();
        FrameProbe? probe = null;
        int frames = 0;
        using (probe = new FrameProbe(() =>
        {
            frames++;
            if (frames == 1)
            {
                Thread.Sleep(90); // A slow frame must not fill the window message queue.
                Check(NativeMethods.PeekMessage(out _, probe!.Handle, FrameClock.FrameMessage,
                    FrameClock.FrameMessage, 1), "A pending frame must be available after slow drawing.");
                Check(!NativeMethods.PeekMessage(out _, probe.Handle, FrameClock.FrameMessage,
                    FrameClock.FrameMessage, 0), "Slow frames must not accumulate queued updates.");
                probe.ResumeClock();
            }
            if (frames == 4)
                context.ExitThread();
        }))
            Application.Run(context);
        Check(frames == 4, "The clock must resume after a slow frame.");
        using var invalidWindowClock = new FrameClock(-1234);
        Thread.Sleep(100);
        bool failed = false;
        try { invalidWindowClock.CheckError(); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "A failed frame delivery must be reported instead of silently stopping the clock.");
    }

    private sealed class FrameProbe : NativeWindow, IDisposable
    {
        private readonly Action draw;
        private readonly FrameClock clock;

        internal FrameProbe(Action draw)
        {
            this.draw = draw;
            CreateHandle(new CreateParams { Caption = "Frame test", Parent = -3 }); // Message-only; never visible.
            clock = new FrameClock(Handle);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == FrameClock.FrameMessage)
            {
                clock.CheckError();
                clock.CompleteFrame();
                draw();
                return;
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            clock.Dispose();
            DestroyHandle();
        }

        internal void ResumeClock() => clock.CompleteFrame();
    }

    private sealed class PreviewHost : Form
    {
        protected override bool ShowWithoutActivation => true;
    }

    private static PreviewHost ShowPreview(SettingsForm menu)
    {
        var host = new PreviewHost
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000),
            ShowInTaskbar = false, ClientSize = menu.Size
        };
        menu.TopLevel = false;
        menu.Location = Point.Empty;
        host.Controls.Add(menu);
        menu.Show();
        host.Show(); // Offscreen and without activation: native controls can paint and receive test clicks.
        Application.DoEvents();
        return host;
    }

    private static void TestMenuInput()
    {
        using var canvas = new CanvasForm(new() { ShowFps = true });
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        var elapsed = Stopwatch.StartNew();
        int stage = 0;
        int leakedKeys = 0;
        bool checkedEarlyHold = false;
        bool f12Down = false;
        bool requestedFocus = false;
        SettingsForm? settingsMenu = null;
        nint colorDialog = 0;
        Form? otherWindow = null;
        bool dismissedSystemMenu = false;
        bool sawFailOpen = false;
        int keysAfterFreeze = 0;
        canvas.KeyDown += (_, _) => leakedKeys++;
        timer.Tick += (_, _) =>
        {
            switch (stage)
            {
                case 0:
                    if (NativeMethods.GetForegroundWindow() != canvas.Handle && elapsed.Elapsed.TotalSeconds < 2)
                    {
                        if (!requestedFocus)
                        {
                            SendKey(Keys.F24, true);
                            SendKey(Keys.F24, false);
                            requestedFocus = true;
                        }
                        canvas.Activate();
                        NativeMethods.SetForegroundWindow(canvas.Handle);
                        break;
                    }
                    Check(NativeMethods.GetForegroundWindow() == canvas.Handle, "The canvas must be foreground for the input check.");
                    SendKey(Keys.F12, true);
                    f12Down = true;
                    elapsed.Restart();
                    stage = 1;
                    break;
                case 1:
                    if (!checkedEarlyHold && elapsed.Elapsed.TotalSeconds >= 1.5)
                    {
                        Check(canvas.OwnedForms.Length == 0, "F12 must not open the menu before two seconds.");
                        checkedEarlyHold = true;
                    }
                    if (elapsed.Elapsed.TotalSeconds < 2.3)
                        break;
                    settingsMenu = canvas.OwnedForms.OfType<SettingsForm>().SingleOrDefault();
                    Check(settingsMenu is not null && settingsMenu.Visible && NativeMethods.GetForegroundWindow() == settingsMenu.Handle,
                        "Two-second F12 hold must open and focus the settings menu.");
                    SendKey(Keys.F12, false);
                    f12Down = false;
                    var color = settingsMenu!.Controls.Find("OptionsLayout", true).Single().Controls.OfType<Button>().Single(button => button.Name == "Background");
                    canvas.BeginInvoke(() => color.PerformClick());
                    elapsed.Restart();
                    stage = 2;
                    break;
                case 2:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    colorDialog = NativeMethods.GetForegroundWindow();
                    Check(colorDialog != canvas.Handle && colorDialog != settingsMenu!.Handle,
                        "The native color dialog must receive focus.");
                    SendKey(Keys.LWin, true);
                    SendKey(Keys.LWin, false);
                    SendKey(Keys.LControlKey, true);
                    SendKey(Keys.Escape, true);
                    SendKey(Keys.Escape, false);
                    SendKey(Keys.LControlKey, false);
                    SendKey(Keys.LMenu, true);
                    SendKey(Keys.Tab, true);
                    SendKey(Keys.Tab, false);
                    SendKey(Keys.LMenu, false);
                    elapsed.Restart();
                    stage = 3;
                    break;
                case 3:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    Check(NativeMethods.GetForegroundWindow() == colorDialog,
                        "Win, Ctrl+Esc and Alt+Tab must not leave the native color dialog.");
                    SendKey(Keys.Escape, true);
                    SendKey(Keys.Escape, false);
                    elapsed.Restart();
                    stage = 4;
                    break;
                case 4:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    if (NativeMethods.GetForegroundWindow() == colorDialog && !dismissedSystemMenu)
                    {
                        // Releasing Alt activates the native window menu; first Escape dismisses it.
                        dismissedSystemMenu = true;
                        SendKey(Keys.Escape, true);
                        SendKey(Keys.Escape, false);
                        elapsed.Restart();
                        break;
                    }
                    Check(NativeMethods.GetForegroundWindow() == settingsMenu!.Handle,
                        $"Escape must return from the color dialog to the settings menu (menu visible={settingsMenu.Visible}, canvas foreground={NativeMethods.GetForegroundWindow() == canvas.Handle}, color dialog foreground={NativeMethods.GetForegroundWindow() == colorDialog}).");
                    SendKey(Keys.LMenu, true);
                    SendKey(Keys.F4, true);
                    elapsed.Restart();
                    stage = 5;
                    break;
                case 5:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    Check(canvas.OwnedForms.Length == 0 && NativeMethods.GetForegroundWindow() == canvas.Handle,
                        "Alt+F4 must cancel only the menu and return to the canvas.");
                    SendKey(Keys.F4, false);
                    SendKey(Keys.LMenu, false);
                    SendKey(Keys.A, true);
                    SendKey(Keys.A, false);
                    elapsed.Restart();
                    stage = 6;
                    break;
                case 6:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    Check(leakedKeys == 0 && NativeMethods.GetAsyncKeyState((int)Keys.LMenu) >= 0,
                        "Capture must resume and the permitted Alt press must be released.");
                    otherWindow = new Form { Text = "Focus test", ClientSize = new Size(320, 160) };
                    otherWindow.Show();
                    otherWindow.Activate();
                    elapsed.Restart();
                    stage = 7;
                    break;
                case 7:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    Check(NativeMethods.GetForegroundWindow() == otherWindow!.Handle,
                        "Suspending the canvas must preserve the new foreground window.");
                    for (nint above = GetWindow(otherWindow.Handle, 3); above != 0; above = GetWindow(above, 3))
                        Check(above != canvas.Handle, "The inactive canvas must be behind the foreground window.");
                    canvas.Activate();
                    elapsed.Restart();
                    stage = 8;
                    break;
                case 8:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    Check(NativeMethods.GetForegroundWindow() == canvas.Handle, "The canvas must activate again.");
                    var probe = new Thread(() =>
                    {
                        Thread.Sleep(3100);
                        SendKey(Keys.A, true);
                        Thread.Sleep(50);
                        sawFailOpen = NativeMethods.GetAsyncKeyState((int)Keys.A) < 0;
                        SendKey(Keys.A, false);
                    });
                    probe.Start();
                    Thread.Sleep(3350); // Simulate a stalled UI while the hook thread remains alive.
                    Check(probe.Join(1000) && sawFailOpen, "A stalled UI must release keyboard capture after three seconds.");
                    stage = 9;
                    elapsed.Restart();
                    break;
                case 9:
                    if (elapsed.Elapsed.TotalSeconds < .3)
                        break;
                    using (var bitmap = new Bitmap(canvas.Width, canvas.Height))
                    {
                        canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        var primary = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds;
                        int left = primary.Left - canvas.Left + 20;
                        int top = primary.Top - canvas.Top + 20;
                        bool textVisible = false;
                        for (int y = top; y < top + 30; y++)
                            for (int x = left; x < left + 120; x++)
                            {
                                Color pixel = bitmap.GetPixel(x, y);
                                textVisible |= pixel.R > 220 && pixel.G > 220 && pixel.B > 220;
                            }
                        Check(textVisible, "Enabled FPS must remain visible after startup hints expire and after UI recovery.");
                    }
                    keysAfterFreeze = leakedKeys;
                    SendKey(Keys.B, true);
                    SendKey(Keys.B, false);
                    stage = 10;
                    elapsed.Restart();
                    break;
                case 10:
                    if (elapsed.Elapsed.TotalSeconds < .2)
                        break;
                    Check(leakedKeys == keysAfterFreeze, "Responsive rendering must restore input interception after fail-open.");
                    stage = 11;
                    timer.Stop();
                    otherWindow!.Dispose();
                    canvas.Dispose();
                    Application.ExitThread();
                    break;
            }
        };
        try
        {
            timer.Start();
            Application.Run(canvas);
            Check(stage == 11, "The menu input check did not complete.");
        }
        finally
        {
            timer.Stop();
            if (f12Down)
                SendKey(Keys.F12, false);
            otherWindow?.Dispose();
            foreach (Keys key in syntheticHeldKeys.ToArray())
                SendKey(key, false);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    private static void SendKey(Keys key, bool down)
    {
        var input = new SyntheticInput
        {
            Type = 1, Keyboard = new KeyboardData { Key = (ushort)key, Flags = down ? 0u : 2u }
        };
        Check(SendInput(1, [input], Marshal.SizeOf<SyntheticInput>()) == 1, "Synthetic keyboard input failed.");
        if (down)
            syntheticHeldKeys.Add(key);
        else
            syntheticHeldKeys.Remove(key);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SyntheticInput
    {
        internal uint Type;
        internal InputUnion Data;
        internal KeyboardData Keyboard { set => Data.Keyboard = value; }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] internal KeyboardData Keyboard;
        [FieldOffset(0)] internal MouseData Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData
    {
        internal int X, Y;
        internal uint Data, Flags, Time;
        internal nuint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardData
    {
        internal ushort Key;
        internal ushort Scan;
        internal uint Flags;
        internal uint Time;
        internal nuint Extra;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, SyntheticInput[] inputs, int size);
}
