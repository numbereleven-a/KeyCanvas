using System.Media;
using System.Text;

namespace KeyCanvas;

internal sealed class SoftCanvasSound : IDisposable
{
    private static readonly double[] Notes = [261.63, 293.66, 329.63, 392, 440, 523.25, 587.33, 659.25];
    private readonly List<SoundPlayer> players = new();
    private readonly List<MemoryStream> streams = new();
    private SoundStyle preparedStyle;
    private int preparedVolume;
    private bool enabled;
    private double lastPlayed = double.NegativeInfinity;

    internal void ApplySettings(CanvasSettings settings)
    {
        enabled = settings.SoundsEnabled && settings.SoundVolumePercent > 0;
        if (!enabled)
        {
            Stop();
            return;
        }
        if (players.Count > 0 && preparedStyle == settings.Sound && preparedVolume == settings.SoundVolumePercent)
            return;
        ClearPlayers();
        for (int note = 0; note < Notes.Length; note++)
        {
            var stream = new MemoryStream(CreateWave(settings.Sound, settings.SoundVolumePercent, note));
            var player = new SoundPlayer(stream);
            streams.Add(stream);
            players.Add(player);
            player.Load(); // Cache the small PCM samples before receiving any key presses.
        }
        preparedStyle = settings.Sound;
        preparedVolume = settings.SoundVolumePercent;
    }

    internal void Play(int key, double now)
    {
        // Limit rapid bursts; the most recent note replaces playback rather than building a queue.
        if (!enabled || now - lastPlayed < .06 || players.Count == 0)
            return;
        lastPlayed = now;
        players[(int)((uint)key % (uint)players.Count)].Play();
    }

    internal void Stop()
    {
        if (players.Count > 0)
            players[0].Stop();
    }

    internal static byte[] CreateWave(SoundStyle style, int volumePercent, int note)
    {
        const int sampleRate = 22050;
        const double duration = .4;
        int samples = (int)(sampleRate * duration);
        using var stream = new MemoryStream(44 + samples * 2);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1); // PCM
        writer.Write((ushort)1); // Mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        double frequency = Notes[note];
        for (int i = 0; i < samples; i++)
        {
            double time = (double)i / sampleRate;
            double phase = Math.Tau * frequency * time;
            double value = style switch
            {
                SoundStyle.SoftPiano => Math.Sin(phase) * Math.Exp(-7 * time) +
                    .35 * Math.Sin(phase * 2) * Math.Exp(-16 * time) + .12 * Math.Sin(phase * 3) * Math.Exp(-25 * time),
                SoundStyle.SoftBells => Math.Sin(phase) * Math.Exp(-6 * time) +
                    .35 * Math.Sin(phase * 2.76) * Math.Exp(-14 * time) + .15 * Math.Sin(phase * 5.4) * Math.Exp(-28 * time),
                _ => Math.Sin(phase) * Math.Exp(-10 * time) +
                    .5 * Math.Sin(phase * 3) * Math.Exp(-24 * time) + .2 * Math.Sin(phase * 6) * Math.Exp(-36 * time)
            };
            double envelope = Math.Min(1, time / .01) * Math.Min(1, (duration - time) / .03);
            writer.Write((short)(value / 1.7 * envelope * volumePercent / 100 * .2 * short.MaxValue));
        }
        return stream.ToArray();
    }

    private void ClearPlayers()
    {
        Stop();
        foreach (var player in players)
            player.Dispose();
        foreach (var stream in streams)
            stream.Dispose();
        players.Clear();
        streams.Clear();
    }

    public void Dispose()
    {
        enabled = false;
        ClearPlayers();
    }
}