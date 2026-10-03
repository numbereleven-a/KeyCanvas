using System.Diagnostics;

namespace KeyCanvas;

internal readonly record struct KeyPress(int Key, double Time);
internal sealed class InputFrame
{
    internal double?[] DownSince { get; } = new double?[256];
    internal KeyPress[] Presses { get; set; } = [];
}

internal sealed class InputBuffer
{
    private readonly object gate = new();
    private readonly double?[] downSince = new double?[256];
    private readonly Queue<KeyPress> presses = new();
    private readonly InputFrame frame = new();
    internal const int MaxPendingPresses = 256;
    internal static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    internal void Record(int key, bool down, double time)
    {
        if ((uint)key >= downSince.Length)
            return;
        lock (gate)
        {
            if (!down)
            {
                downSince[key] = null;
                return;
            }
            if (downSince[key].HasValue)
                return; // Windows autorepeat grows the existing object, without creating more.
            downSince[key] = time;
            if (presses.Count == MaxPendingPresses)
                presses.Dequeue();
            presses.Enqueue(new KeyPress(key, time));
        }
    }

    internal InputFrame Read()
    {
        lock (gate)
        {
            // The sole reader consumes this reusable snapshot before reading the next frame.
            Array.Copy(downSince, frame.DownSince, downSince.Length);
            frame.Presses = presses.ToArray();
            presses.Clear();
            return frame;
        }
    }

    internal void Reset()
    {
        lock (gate)
        {
            Array.Clear(downSince);
            presses.Clear();
        }
    }
}

internal sealed class HoldGesture(double duration)
{
    private double? completedStart;
    internal double Progress { get; private set; }

    internal bool Update(double? downSince, double now)
    {
        Progress = downSince.HasValue ? Math.Clamp((now - downSince.Value) / duration, 0, 1) : 0;
        if (!downSince.HasValue)
            completedStart = null;
        if (Progress < 1 || completedStart == downSince)
            return false;
        completedStart = downSince;
        return true;
    }
}
