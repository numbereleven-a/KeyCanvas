using System.Text.Json;

namespace KeyCanvas;

internal enum FigureStyle { Random, Circle, Triangle, Square, Star, Line, Ring }
internal enum ColorPalette { Bright, Pastel, Warm, Cool }
internal enum SoundStyle { Piano, Bells, Xylophone }

internal sealed record CanvasSettings
{
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

    internal CanvasSettings Normalize() => this with
    {
        Language = Enum.IsDefined(Language) ? Language : AppLanguage.English,
        Sound = Enum.IsDefined(Sound) ? Sound : SoundStyle.Bells,
        SoundVolumePercent = Math.Clamp(SoundVolumePercent, 0, 100),
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
