using System.Text.Json;

namespace KeyCanvas;

internal enum FigureStyle { Random, Circle, Triangle, Square, Star, Line, Ring }
internal enum ColorPalette { Bright, Pastel, Warm, Cool }
internal enum SoundStyle { Piano, Bells, Xylophone, Synthesizer }

internal sealed record CanvasSettings
{
    internal static readonly ActionShortcut DefaultExit = new() { Key = Keys.Escape, HoldSeconds = 5 };
    internal static readonly ActionShortcut DefaultClear = new() { Key = Keys.F1, HoldSeconds = 3 };
    internal static readonly ActionShortcut DefaultMenu = new() { Key = Keys.F12, HoldSeconds = 2 };
    public AppLanguage Language { get; init; } = UiText.WindowsLanguage;
    public int BackgroundArgb { get; init; } = Color.FromArgb(12, 17, 35).ToArgb();
    public FigureStyle Figures { get; init; }
    public ColorPalette Palette { get; init; }
    public int ShapeSizePercent { get; init; } = 100;
    public int AnimationSpeedPercent { get; init; } = 100;
    public int ParticleAmountPercent { get; init; } = 100;
    public int ObjectLimit { get; init; } = 500;
    public int FigureLifetimeSeconds { get; init; } = 5;
    public int FramesPerSecond { get; init; } = 60;
    public bool MouseTrail { get; init; } = true;
    public bool MouseClicks { get; init; } = true;
    public bool GrowWhileHeld { get; init; } = true;
    public bool ReactToRhythm { get; init; } = true;
    public bool ShowFrameTiming { get; init; }
    public bool ShowFps { get; init; }
    public bool ShowStartupHints { get; init; } = true;
    public bool SoundsEnabled { get; init; } = true;
    public SoundStyle Sound { get; init; } = SoundStyle.Bells;
    public int SoundVolumePercent { get; init; } = 15;
    public bool AlphabetMode { get; init; }
    public bool TransparentCanvas { get; init; }
    public int CanvasOpacityPercent { get; init; } = 60;
    public ActionShortcut ExitShortcut { get; init; } = DefaultExit;
    public ActionShortcut ClearShortcut { get; init; } = DefaultClear;
    public ActionShortcut MenuShortcut { get; init; } = DefaultMenu;

    internal bool IsActionKey(int key) => key == (int)ExitShortcut.Key || key == (int)ClearShortcut.Key || key == (int)MenuShortcut.Key;

    internal CanvasSettings Normalize()
    {
        var normalized = this with
        {
        Language = Enum.IsDefined(Language) ? Language : AppLanguage.English,
        Sound = Enum.IsDefined(Sound) ? Sound : SoundStyle.Bells,
        SoundVolumePercent = Math.Clamp(SoundVolumePercent, 0, 100),
        CanvasOpacityPercent = Math.Clamp(CanvasOpacityPercent, 10, 100),
        ExitShortcut = (ExitShortcut ?? DefaultExit).Normalize(DefaultExit),
        ClearShortcut = (ClearShortcut ?? DefaultClear).Normalize(DefaultClear),
        MenuShortcut = (MenuShortcut ?? DefaultMenu).Normalize(DefaultMenu),
        BackgroundArgb = BackgroundArgb | unchecked((int)0xFF000000),
        Figures = Enum.IsDefined(Figures) ? Figures : FigureStyle.Random,
        Palette = Enum.IsDefined(Palette) ? Palette : ColorPalette.Bright,
        ShapeSizePercent = Math.Clamp(ShapeSizePercent, 50, 200),
        AnimationSpeedPercent = Math.Clamp(AnimationSpeedPercent, 25, 200),
        ParticleAmountPercent = Math.Clamp(ParticleAmountPercent, 0, 200),
        ObjectLimit = Math.Clamp(ObjectLimit, 100, 1000),
        FigureLifetimeSeconds = Math.Clamp(FigureLifetimeSeconds, 5, 60),
        FramesPerSecond = FramesPerSecond is 30 or 60 or 90 or 120 ? FramesPerSecond : 60
        };
        // Damaged stored shortcuts must not hide access to settings behind another action.
        return normalized.ExitShortcut.SameChord(normalized.ClearShortcut) || normalized.ExitShortcut.SameChord(normalized.MenuShortcut) || normalized.ClearShortcut.SameChord(normalized.MenuShortcut)
            ? normalized with { ExitShortcut = DefaultExit, ClearShortcut = DefaultClear, MenuShortcut = DefaultMenu }
            : normalized;
    }
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyCanvas", "settings.json");

    internal static CanvasSettings Load(string path)
    {
        try
        {
            return (JsonSerializer.Deserialize<CanvasSettings>(File.ReadAllText(path)) ?? new()).Normalize();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing, damaged or unreadable preferences must not prevent opening the canvas.
            return new();
        }
    }

    internal static void Save(string path, CanvasSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
