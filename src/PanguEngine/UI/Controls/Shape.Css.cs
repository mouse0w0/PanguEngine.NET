using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI.Controls;

public abstract partial class Shape
{
    static Shape()
    {
        CssRegistry.RegisterElement<Shape>("Shape");
        CssRegistry.RegisterProperty<Shape, Brush?>("fill", FillProperty, CssValueConverters.ParseBrush);
        CssRegistry.RegisterProperty<Shape, Brush?>("stroke", StrokeProperty, CssValueConverters.ParseBrush);
        CssRegistry.RegisterProperty<Shape, double>(
            "stroke-width",
            StrokeThicknessProperty,
            CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<Shape, StrokeLineCap>(
            "stroke-linecap",
            StrokeLineCapProperty,
            ParseStrokeLineCap);
        CssRegistry.RegisterProperty<Shape, StrokeLineJoin>(
            "stroke-linejoin",
            StrokeLineJoinProperty,
            ParseStrokeLineJoin);
        CssRegistry.RegisterProperty<Shape, double>(
            "stroke-miterlimit",
            StrokeMiterLimitProperty,
            CssValueConverters.ParseNumber);
    }

    private static StrokeLineCap ParseStrokeLineCap(string value) =>
        value.ToLowerInvariant() switch
        {
            "butt" => StrokeLineCap.Butt,
            "square" => StrokeLineCap.Square,
            "round" => StrokeLineCap.Round,
            _ => throw new FormatException($"Value '{value}' is not a recognized stroke line cap.")
        };

    private static StrokeLineJoin ParseStrokeLineJoin(string value) =>
        value.ToLowerInvariant() switch
        {
            "miter" => StrokeLineJoin.Miter,
            "bevel" => StrokeLineJoin.Bevel,
            "round" => StrokeLineJoin.Round,
            _ => throw new FormatException($"Value '{value}' is not a recognized stroke line join.")
        };
}
