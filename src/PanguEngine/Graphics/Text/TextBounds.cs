namespace PanguEngine.Graphics.Text;

/// <summary>
/// Describes text bounds in logical pixels.
/// </summary>
public readonly record struct TextBounds
{
    /// <summary>
    /// Creates text bounds.
    /// </summary>
    public TextBounds(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the left coordinate.</summary>
    public double X { get; }
    /// <summary>Gets the top coordinate.</summary>
    public double Y { get; }
    /// <summary>Gets the width.</summary>
    public double Width { get; }
    /// <summary>Gets the height.</summary>
    public double Height { get; }
    /// <summary>Gets empty bounds.</summary>
    public static TextBounds Empty => default;

    internal static TextBounds Union(TextBounds left, TextBounds right)
    {
        if (left.Width == 0 && left.Height == 0)
            return right;
        if (right.Width == 0 && right.Height == 0)
            return left;
        var x = Math.Min(left.X, right.X);
        var y = Math.Min(left.Y, right.Y);
        var rightEdge = Math.Max(left.X + left.Width, right.X + right.Width);
        var bottom = Math.Max(left.Y + left.Height, right.Y + right.Height);
        return new TextBounds(x, y, rightEdge - x, bottom - y);
    }
}
