namespace PanguEngine.Client.UI.Drawing.Geometry;

internal enum PathSegmentKind
{
    Move,
    Line,
    Quadratic,
    Cubic,
    Arc,
    Close
}

internal readonly record struct PathSegment(
    PathSegmentKind Kind,
    Point Start,
    Point End,
    Point Control1,
    Point Control2,
    double RadiusX,
    double RadiusY,
    double Rotation,
    double Sweep)
{
    internal Point Evaluate(double t)
    {
        if (t == 0) return Start;
        if (t == 1) return End;
        if (Kind is PathSegmentKind.Move or PathSegmentKind.Line or PathSegmentKind.Close)
            return new Point(Start.X + (End.X - Start.X) * t, Start.Y + (End.Y - Start.Y) * t);
        if (Kind == PathSegmentKind.Quadratic)
        {
            var u = 1 - t;
            return new Point(
                u * u * Start.X + 2 * u * t * Control1.X + t * t * End.X,
                u * u * Start.Y + 2 * u * t * Control1.Y + t * t * End.Y);
        }
        if (Kind == PathSegmentKind.Cubic)
        {
            var u = 1 - t;
            return new Point(
                u * u * u * Start.X + 3 * u * u * t * Control1.X + 3 * u * t * t * Control2.X + t * t * t * End.X,
                u * u * u * Start.Y + 3 * u * u * t * Control1.Y + 3 * u * t * t * Control2.Y + t * t * t * End.Y);
        }

        var angle = Rotation + Sweep * t;
        var cos = Math.Cos(Control2.X);
        var sin = Math.Sin(Control2.X);
        return new Point(
            Control1.X + RadiusX * Math.Cos(angle) * cos - RadiusY * Math.Sin(angle) * sin,
            Control1.Y + RadiusX * Math.Cos(angle) * sin + RadiusY * Math.Sin(angle) * cos);
    }
}
