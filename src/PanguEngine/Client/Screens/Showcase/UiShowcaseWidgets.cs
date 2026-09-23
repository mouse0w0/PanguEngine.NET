using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.Screens;

internal static class UiShowcaseWidgets
{
    internal static Text Label(string text, string? id = null) =>
        new() { Content = text, StyleId = id, Wrapping = TextWrapping.Wrap };

    internal static Button ActionButton(string id, string text, Action action)
    {
        var button = new Button { StyleId = id, Text = text };
        button.Click += (_, _) => action();
        return button;
    }

    internal static StackPanel Column(params UiNode[] children) => Stack(Orientation.Vertical, children);

    internal static StackPanel Row(params UiNode[] children) => Stack(Orientation.Horizontal, children);

    private static StackPanel Stack(Orientation orientation, UiNode[] children)
    {
        var panel = new StackPanel { Orientation = orientation, Spacing = 8 };
        foreach (var child in children)
            panel.Children.Add(child);
        return panel;
    }
}
