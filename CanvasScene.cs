using System.Drawing.Drawing2D;

namespace KeyCanvas;

internal enum ShapeKind { Circle, Triangle, Square, Star, Line, Ring, Particle }

internal sealed class CanvasObject
{
    internal ShapeKind Kind;
    internal PointF Position;
    internal PointF Velocity;
    internal float Radius;
    internal float Angle;
    internal float Spin;
    internal Color Color;
    internal double Age;
    internal double Lifetime;
    internal double? FadeRemaining;
}

internal sealed class CanvasScene
{
    private readonly List<CanvasObject> objects = new();
    private readonly Dictionary<int, CanvasObject> heldObjects = new();
    private readonly Random random;
    private Rectangle[] viewports = [new Rectangle(0, 0, 1280, 720)];
    private Rectangle centerViewport = new(0, 0, 1280, 720);
    private double previousPress = double.NegativeInfinity;
    private double streamTime;
    private static readonly int[] ArrowKeys = [(int)Keys.Left, (int)Keys.Up, (int)Keys.Right, (int)Keys.Down];
    private readonly List<int> releasedKeys = new();
    private static readonly Color[][] Palettes =
    [
        [
        Color.FromArgb(255, 106, 164), Color.FromArgb(103, 218, 255),
        Color.FromArgb(255, 209, 105), Color.FromArgb(167, 142, 255),
        Color.FromArgb(105, 239, 185), Color.FromArgb(255, 148, 103)
        ],
        [Color.FromArgb(255, 184, 207), Color.FromArgb(177, 224, 246), Color.FromArgb(255, 231, 174),
         Color.FromArgb(212, 193, 246), Color.FromArgb(178, 235, 209), Color.FromArgb(255, 210, 186)],
        [Color.FromArgb(255, 101, 101), Color.FromArgb(255, 160, 90), Color.FromArgb(255, 218, 100),
         Color.FromArgb(255, 124, 163), Color.FromArgb(234, 115, 84), Color.FromArgb(255, 192, 132)],
        [Color.FromArgb(100, 220, 255), Color.FromArgb(120, 163, 255), Color.FromArgb(168, 145, 255),
         Color.FromArgb(99, 232, 199), Color.FromArgb(138, 211, 255), Color.FromArgb(192, 182, 255)]
    ];

    internal CanvasScene(Random? random = null) => this.random = random ?? new Random();
    internal IReadOnlyList<CanvasObject> Objects => objects;
    internal CanvasSettings Settings { get; private set; } = new();

    internal void ApplySettings(CanvasSettings settings)
    {
        double lifetimeScale = (double)settings.FigureLifetimeSeconds / Settings.FigureLifetimeSeconds;
        bool recolor = settings.Palette != Settings.Palette;
        Settings = settings;
        if (objects.Count > Settings.ObjectLimit)
            objects.RemoveRange(0, objects.Count - Settings.ObjectLimit);
        foreach (var shape in objects)
        {
            if (recolor)
                shape.Color = RandomColor();
            if (shape.Kind != ShapeKind.Particle)
                shape.Lifetime *= lifetimeScale;
        }
    }

    internal void SetViewports(Rectangle[] rectangles, Rectangle primary)
    {
        viewports = rectangles;
        centerViewport = primary;
    }

    internal void Press(KeyPress press)
    {
        if (press.Key is (int)Keys.Escape or (int)Keys.F1 or (int)Keys.F12)
            return;
        float energy = Settings.ReactToRhythm && press.Time - previousPress < .22 ? 1.5f : 1;
        previousPress = press.Time;
        PointF point = RandomPoint();
        if (press.Key == (int)Keys.Space)
        {
            point = new PointF(centerViewport.Left + centerViewport.Width / 2f,
                centerViewport.Top + centerViewport.Height / 2f);
            var wave = Add(ShapeKind.Ring, point, 65 * energy, Settings.FigureLifetimeSeconds);
            heldObjects[press.Key] = wave;
            Burst(point, 36, energy);
        }
        else if (IsArrow(press.Key))
        {
            DirectionalBurst(press.Key);
        }
        else
        {
            var shape = Add(RandomFigure(), point, Next(18, 50) * energy, Settings.FigureLifetimeSeconds);
            heldObjects[press.Key] = shape;
            Burst(point, 7, energy);
        }
    }

    internal void Mouse(PointF point, bool burst)
    {
        if (burst ? !Settings.MouseClicks : !Settings.MouseTrail)
            return;
        Add(burst ? RandomFigure() : ShapeKind.Circle, point,
            burst ? Next(20, 48) : Next(5, 13), Settings.FigureLifetimeSeconds);
        Burst(point, burst ? 18 : 2, 1);
    }

    internal void Update(double elapsed, double?[] held)
    {
        double movement = elapsed * Settings.AnimationSpeedPercent / 100;
        streamTime += elapsed;
        if (streamTime >= .08)
        {
            streamTime %= .08;
            foreach (int key in ArrowKeys)
                if (held[key].HasValue)
                    DirectionalBurst(key);
        }

        releasedKeys.Clear();
        foreach (var (key, shape) in heldObjects)
        {
            if (!held[key].HasValue || !objects.Contains(shape))
                releasedKeys.Add(key);
            else if (Settings.GrowWhileHeld && shape.Radius < 170)
                shape.Radius = Math.Min(170, shape.Radius + (float)movement * 42);
        }
        foreach (int key in releasedKeys)
            heldObjects.Remove(key);

        foreach (var shape in objects)
        {
            shape.Age += elapsed;
            shape.Position = new PointF(shape.Position.X + shape.Velocity.X * (float)movement,
                shape.Position.Y + shape.Velocity.Y * (float)movement);
            shape.Angle += shape.Spin * (float)movement;
            if (shape.Kind == ShapeKind.Ring)
                shape.Radius += (float)movement * 22;
            if (shape.FadeRemaining.HasValue)
                shape.FadeRemaining -= elapsed;
        }
        objects.RemoveAll(shape => shape.Age >= shape.Lifetime || shape.FadeRemaining <= 0);
    }

    internal void Clear()
    {
        heldObjects.Clear();
        foreach (var shape in objects)
        {
            shape.FadeRemaining = .65;
            shape.Velocity = new PointF(Next(-180, 180), Next(-180, 180));
        }
    }

    internal void ReleaseHeld() => heldObjects.Clear();

    internal void Draw(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.White);
        using var pen = new Pen(Color.White, 4);
        Span<PointF> points = stackalloc PointF[10];
        foreach (var shape in objects)
        {
            float opacity = (float)Math.Clamp((shape.Lifetime - shape.Age) / 2, 0, 1);
            if (shape.FadeRemaining.HasValue)
                opacity = Math.Min(opacity, (float)Math.Clamp(shape.FadeRemaining.Value / .65, 0, 1));
            Color color = Color.FromArgb((int)(opacity * (shape.Kind == ShapeKind.Particle ? 220 : 185)), shape.Color);
            float r = shape.Radius * (shape.Kind == ShapeKind.Particle ? 1 : Settings.ShapeSizePercent / 100f);
            var bounds = new RectangleF(shape.Position.X - r, shape.Position.Y - r, r * 2, r * 2);
            switch (shape.Kind)
            {
                case ShapeKind.Circle:
                case ShapeKind.Particle:
                    brush.Color = color;
                    graphics.FillEllipse(brush, bounds);
                    break;
                case ShapeKind.Ring:
                    pen.Color = color;
                    graphics.DrawEllipse(pen, bounds);
                    break;
                case ShapeKind.Line:
                    pen.Color = color;
                    graphics.DrawLine(pen, Vertex(shape, 0, r), Vertex(shape, MathF.PI, r));
                    break;
                default:
                    int sides = shape.Kind == ShapeKind.Star ? 10 : shape.Kind == ShapeKind.Triangle ? 3 : 4;
                    for (int i = 0; i < sides; i++)
                        points[i] = Vertex(shape, i * MathF.Tau / sides,
                            shape.Kind == ShapeKind.Star && i % 2 == 1 ? r * .45f : r);
                    brush.Color = color;
                    graphics.FillPolygon(brush, points[..sides]);
                    break;
            }
        }
    }

    private static PointF Vertex(CanvasObject shape, float angle, float radius) => new(
        shape.Position.X + MathF.Cos(angle + shape.Angle) * radius,
        shape.Position.Y + MathF.Sin(angle + shape.Angle) * radius);

    private CanvasObject Add(ShapeKind kind, PointF position, float radius, double lifetime)
    {
        // Old objects fade before the hard cap is reached, even during long key/mouse sessions.
        if (objects.Count >= Settings.ObjectLimit - Math.Min(100, Settings.ObjectLimit / 8))
        {
            var oldest = objects.FirstOrDefault(shape => !shape.FadeRemaining.HasValue);
            if (oldest is not null)
                oldest.FadeRemaining = .65;
        }
        if (objects.Count == Settings.ObjectLimit)
            objects.RemoveAt(0);
        var shape = new CanvasObject
        {
            Kind = kind, Position = position, Radius = radius,
            Color = RandomColor(), Lifetime = lifetime,
            Angle = Next(0, MathF.Tau), Spin = Next(-.35f, .35f),
            Velocity = new PointF(Next(-7, 7), Next(-7, 7))
        };
        objects.Add(shape);
        return shape;
    }

    private void Burst(PointF point, int count, float energy)
    {
        count = (int)Math.Round(count * Settings.ParticleAmountPercent / 100.0);
        for (int i = 0; i < count; i++)
        {
            float angle = Next(0, MathF.Tau);
            float speed = Next(22, 110) * energy;
            var particle = Add(ShapeKind.Particle, point, Next(2, 6) * energy, Next(1.2f, 3.2f));
            particle.Velocity = new PointF(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
        }
    }

    private void DirectionalBurst(int key)
    {
        PointF direction = key switch
        {
            (int)Keys.Left => new(-1, 0), (int)Keys.Right => new(1, 0),
            (int)Keys.Up => new(0, -1), _ => new(0, 1)
        };
        PointF origin = RandomPoint();
        int count = (int)Math.Round(6 * Settings.ParticleAmountPercent / 100.0);
        for (int i = 0; i < count; i++)
        {
            var particle = Add(ShapeKind.Particle, origin, Next(3, 8), Next(2, 4));
            float speed = Next(130, 250);
            particle.Velocity = new PointF(direction.X * speed + Next(-25, 25),
                direction.Y * speed + Next(-25, 25));
        }
    }

    private PointF RandomPoint()
    {
        Rectangle viewport = viewports[random.Next(viewports.Length)];
        return new PointF(viewport.Left + Next(.08f, .92f) * viewport.Width,
            viewport.Top + Next(.08f, .92f) * viewport.Height);
    }

    private float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);
    private ShapeKind RandomFigure() => Settings.Figures == FigureStyle.Random
        ? (ShapeKind)random.Next(6) : (ShapeKind)((int)Settings.Figures - 1);
    private Color RandomColor()
    {
        var colors = Palettes[(int)Settings.Palette];
        return colors[random.Next(colors.Length)];
    }
    private static bool IsArrow(int key) => key is >= (int)Keys.Left and <= (int)Keys.Down;
}
