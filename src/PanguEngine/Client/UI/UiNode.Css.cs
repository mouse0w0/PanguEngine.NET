using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    static UiNode()
    {
        UiCssRegistry.RegisterElement<UiNode>("UiNode");
        UiCssRegistry.RegisterProperty<UiNode, double>("width", WidthProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("height", HeightProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("min-width", MinWidthProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("min-height", MinHeightProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("max-width", MaxWidthProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("max-height", MaxHeightProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, Thickness>("margin", MarginProperty, UiCssValueConverters.ParseThickness);
        UiCssRegistry.RegisterProperty<UiNode>("margin-top", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Top, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode>("margin-right", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Right, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode>("margin-bottom", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Bottom, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode>("margin-left", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Left, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode, double>("opacity", OpacityProperty, UiCssValueConverters.ParseNumber);
        UiCssRegistry.RegisterProperty<UiNode, HorizontalAlignment>(
            "horizontal-alignment",
            HorizontalAlignmentProperty,
            UiCssValueConverters.ParseHorizontalAlignment);
        UiCssRegistry.RegisterProperty<UiNode, VerticalAlignment>(
            "vertical-alignment",
            VerticalAlignmentProperty,
            UiCssValueConverters.ParseVerticalAlignment);
        UiCssRegistry.RegisterProperty<UiNode, Visibility>(
            "visibility",
            VisibilityProperty,
            UiCssValueConverters.ParseVisibility);
    }
}
