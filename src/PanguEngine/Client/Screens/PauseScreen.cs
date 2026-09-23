using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.Screens;

internal sealed class PauseScreen : UiScreen
{
    internal PauseScreen()
        : base(CreateRoot())
    {
        PausesGame = true;
        CloseOnEscape = true;
    }

    private static UiNode CreateRoot()
    {
        var title = new Text
        {
            Content = "游戏已暂停",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var resumeButton = new Button { Text = "回到游戏", MinWidth = 220, MinHeight = 42 };
        var showcaseButton = new Button { Text = "UI 展示", MinWidth = 220, MinHeight = 42 };
        var exitButton = new Button
        {
            Text = "退出游戏",
            MinWidth = 220,
            MinHeight = 42
        };
        exitButton.Classes.Add("danger");
        resumeButton.Click += (_, _) => ClientEngine.Current.Ui.Close();
        showcaseButton.Click += (_, _) => ClientEngine.Current.Ui.Open(UiShowcaseScreen.CreateForClient());
        exitButton.Click += (_, _) => ClientEngine.Current.RequestShutdown();

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            MinWidth = 268,
            MaxWidth = 340,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(24),
            Background = new SolidColorBrush(20, 24, 30, 235),
            BorderBrush = new SolidColorBrush(90, 102, 116),
            BorderThickness = new Thickness(1)
        };
        panel.Children.Add(title);
        panel.Children.Add(resumeButton);
        panel.Children.Add(showcaseButton);
        panel.Children.Add(exitButton);

        var mask = new Panel
        {
            Background = new SolidColorBrush(0, 0, 0, 128)
        };
        mask.Children.Add(panel);
        return mask;
    }
}
