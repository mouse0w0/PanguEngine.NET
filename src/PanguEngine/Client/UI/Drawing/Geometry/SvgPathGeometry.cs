namespace PanguEngine.Client.UI.Drawing.Geometry;

internal sealed class SvgPathGeometry
{
    private readonly PathGeometry _geometry;

    private SvgPathGeometry(PathGeometry geometry) => _geometry = geometry;

    internal Rect Bounds => _geometry.Bounds;

    internal static SvgPathGeometry Parse(string? data) => new(PathGeometry.Parse(data));

    internal FlattenedContour[] Flatten(double tolerance) => _geometry.Flatten(tolerance);
}
