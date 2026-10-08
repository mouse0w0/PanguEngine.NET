using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.Screens.Showcase;

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

    internal static Text Caption(string text)
    {
        var caption = Label(text);
        caption.Classes.Add("showcase-caption");
        return caption;
    }

    internal static StackPanel Sample(string title, UiNode node) => Column(Caption(title), node);

    internal static UiShowcaseGallery Samples(params UiNode[] children)
    {
        var gallery = new UiShowcaseGallery(170, 3);
        foreach (var child in children)
            gallery.Children.Add(child);
        return gallery;
    }

    internal static StackPanel Preview(params UiNode[] children)
    {
        var preview = Column(children);
        preview.Classes.Add("showcase-preview");
        preview.Padding = new Thickness(12);
        return preview;
    }

    internal static StackPanel ExampleCard(UiShowcaseExample example)
    {
        var title = Label(example.Title);
        title.Classes.Add("showcase-example-title");
        var instructions = Label(example.Instructions);
        instructions.Classes.Add("showcase-example-instructions");
        var card = Column(title, instructions, example.Content);
        card.Classes.Add("showcase-example-page");
        card.Padding = new Thickness(18);
        card.Spacing = 12;
        return card;
    }

    internal static UiShowcaseGallery Examples(UiShowcaseExample[] examples)
    {
        var gallery = new UiShowcaseGallery();
        foreach (var example in examples)
            gallery.Children.Add(ExampleCard(example));
        return gallery;
    }

    private static StackPanel Stack(Orientation orientation, UiNode[] children)
    {
        var panel = new StackPanel { Orientation = orientation, Spacing = 8 };
        foreach (var child in children)
            panel.Children.Add(child);
        return panel;
    }
}
