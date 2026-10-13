using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

public abstract partial class Shape
{
    static Shape()
    {
        UiCssRegistry.RegisterElement<Shape>("Shape");
        UiCssRegistry.RegisterProperty<Shape, Brush?>("fill", FillProperty, UiCssValueConverters.ParseBrush);
        UiCssRegistry.RegisterProperty<Shape, Brush?>("stroke", StrokeProperty, UiCssValueConverters.ParseBrush);
        UiCssRegistry.RegisterProperty<Shape, double>(
            "stroke-width",
            StrokeThicknessProperty,
            UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<Shape, StrokeLineCap>(
            "stroke-linecap",
            StrokeLineCapProperty,
            ParseStrokeLineCap);
        UiCssRegistry.RegisterProperty<Shape, StrokeLineJoin>(
            "stroke-linejoin",
            StrokeLineJoinProperty,
            ParseStrokeLineJoin);
        UiCssRegistry.RegisterProperty<Shape, double>(
            "stroke-miterlimit",
            StrokeMiterLimitProperty,
            UiCssValueConverters.ParseNumber);
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
