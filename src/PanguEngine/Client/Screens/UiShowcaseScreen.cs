using System.Globalization;
using PanguEngine.Client.Screens.Showcase;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.Screens;

/// <summary>
/// Hosts component tabs with comparable UI controls, styles, bindings and layouts.
/// </summary>
internal sealed class UiShowcaseScreen : GameScreen
{
    private readonly Action _returnToPause;
    private readonly UiShowcaseShell _shell;

    private double _openingScale;

    internal UiShowcaseScreen(UiStyleSheet sheet, UiStyleSheet overrides, Action returnToPause)
    {
        _returnToPause = returnToPause;

        _shell = new UiShowcaseShell();
        SetStyleSheets([sheet, overrides]);
        _shell.SetPages([
            .. UiShowcaseControls.CreateExamples(Report),
            new UiShowcaseExample("圆角", "比较圆角背景、边框、形状和内容裁剪；顶部可切换缩放。", UiShowcaseWidgets.Examples(UiShowcaseRoundedRectangles.CreateExamples(Report))),
            new UiShowcaseExample("样式", "比较规则、配色和图像画刷。", UiShowcaseWidgets.Examples(UiShowcaseStyles.CreateExamples())),
            new UiShowcaseExample("绑定", "直接编辑输入，体验数据同步。", UiShowcaseWidgets.Examples(UiShowcaseBindings.CreateExamples(Report))),
            new UiShowcaseExample("布局", "并列比较对齐、排列、停靠、网格、位置与可见性。", UiShowcaseWidgets.Examples(UiShowcaseLayout.CreateExamples()))]);

        PausesGame = true;
        CloseOnEscape = true;

        _shell.ReturnButton.Click += (_, _) => ReturnToPause();
        _shell.ScaleDownButton.Click += (_, _) => SetScaleFactor(0.75);
        _shell.ScaleResetButton.Click += (_, _) => SetScaleFactor(1);
        _shell.ScaleUpButton.Click += (_, _) => SetScaleFactor(1.25);
        _shell.RestoreScaleButton.Click += (_, _) => SetScaleFactor(1);

        Root = _shell;
        UpdateScaleDisplay();
    }

    internal UiShowcaseShell Shell => _shell;

    internal void SetScaleFactor(double factor)
    {
        Scale = _openingScale * factor;
        UpdateScaleDisplay();
    }

    internal void ReturnToPause() => _returnToPause();

    internal static UiShowcaseScreen CreateForClient()
    {
        using var showcaseStream = Engine.ResourceManager.Open("pangu/ui/showcase.css");
        var sheet = UiStyleSheet.Parse(showcaseStream, "pangu/ui/showcase.css");
        using var overridesStream = Engine.ResourceManager.Open("pangu/ui/showcase-overrides.css");
        var overrides = UiStyleSheet.Parse(overridesStream, "pangu/ui/showcase-overrides.css");
        return new UiShowcaseScreen(
            sheet,
            overrides,
            static () => ClientEngine.Current.Ui.Open(new PauseScreen()));
    }

    /// <inheritdoc />
    protected override void OnOpening()
    {
        _openingScale = Scale;
        UpdateScaleDisplay();
    }

    /// <inheritdoc />
    protected override void OnFrameUpdate(double alpha)
    {
        if (_shell.IsArrangeValid)
            _shell.ApplyViewportMode(new Size(_shell.LayoutBounds.Width, _shell.LayoutBounds.Height));
    }

    private void Report(string message) => _shell.SetFeedback(message);

    private void UpdateScaleDisplay() =>
        _shell.SetScaleValue(string.Create(CultureInfo.InvariantCulture, $"缩放 {Scale:0.##}x"));
}
