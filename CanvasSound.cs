using System.Runtime.InteropServices;

namespace KeyCanvas;

internal sealed class CanvasSound : IDisposable
{
    private readonly Dictionary<int, (int Note, double Until)> playing = new();
    private nint device;
    private bool enabled;
    private int velocity;
    private SoundStyle style;
    private readonly System.Windows.Forms.Timer releases = new() { Interval = 25 };
    internal bool Available => device != 0;

    internal CanvasSound()
    {
        releases.Tick += (_, _) =>
        {
            foreach (int key in playing.Keys.Where(key => InputBuffer.Now >= playing[key].Until).ToArray())
                Release(key);
            if (playing.Count == 0)
                releases.Stop();
        };
    }

    internal void ApplySettings(CanvasSettings settings)
    {
        Stop();
        enabled = settings.SoundsEnabled && settings.SoundVolumePercent > 0;
        velocity = Math.Clamp((int)Math.Round(settings.SoundVolumePercent * 1.27), 1, 127);
        style = settings.Sound;
        if (enabled && device == 0)
            midiOutOpen(out device, uint.MaxValue, 0, 0, 0);
        if (device != 0)
            Send(0xC0, ProgramFor(style), 0);
    }

    internal static int ProgramFor(SoundStyle style) => style switch
    {
        SoundStyle.Piano => 0, SoundStyle.Bells => 14, SoundStyle.Xylophone => 13, _ => 80
    };
    internal static int NoteFor(int key) => 60 + (int)((uint)(key - (int)Keys.A) % 24);

    internal void Play(int key, double now, bool held = false)
    {
        if (!enabled || device == 0)
            return;
        Release(key);
        int note = NoteFor(key);
        // One MIDI channel is enough for the selected instrument, with a bounded number of voices.
        if (playing.Count >= 32)
        {
            int oldest = playing.Keys.First();
            Release(oldest);
        }
        playing[key] = (note, held ? double.PositiveInfinity : now + .45);
        Send(0x90, note, velocity);
        releases.Start();
    }

    internal void Update(double?[] held, double now)
    {
        foreach (int key in playing.Keys.ToArray())
        {
            var voice = playing[key];
            if (now >= voice.Until || double.IsPositiveInfinity(voice.Until) && !held[key].HasValue)
            {
                Release(key);
            }
        }
    }

    internal void Stop()
    {
        releases.Stop();
        if (device != 0)
        {
            Send(0xB0, 120, 0); // All sound off, including release tails when leaving the canvas.
            Send(0xB0, 123, 0);
        }
        playing.Clear();
    }

    private void Release(int key)
    {
        if (playing.Remove(key, out var voice) && !playing.Values.Any(other => other.Note == voice.Note))
            Send(0x80, voice.Note, 0);
    }

    private void Send(int status, int first, int second) =>
        midiOutShortMsg(device, (uint)(status | first << 8 | second << 16));

    public void Dispose()
    {
        Stop();
        if (device != 0)
        {
            midiOutClose(device);
            device = 0;
        }
        enabled = false;
        releases.Dispose();
    }

    [DllImport("winmm.dll")] private static extern uint midiOutOpen(out nint handle, uint device, nint callback, nint instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint midiOutShortMsg(nint handle, uint message);
    [DllImport("winmm.dll")] private static extern uint midiOutClose(nint handle);
}
