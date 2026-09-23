using System.Globalization;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.Screens;

/// <summary>
/// Hosts the interactive UI showcase with four categories and one visible example at a time.
/// </summary>
internal sealed class UiShowcaseScreen : UiScreen
{
    private const int CategoryCount = 4;

    private readonly UiStyleSheet _sheet;
    private readonly UiStyleSheet _overrides;
    private readonly Action _returnToPause;
    private readonly UiShowcaseShell _shell;
    private readonly UiShowcaseExample[] _controls;
    private readonly UiShowcaseExample[] _styles;
    private readonly UiShowcaseExample[] _bindings;
    private readonly UiShowcaseExample[] _layout;
    private readonly List<Button> _switchButtons = [];
    private readonly int[] _exampleIndices = new int[CategoryCount];

    private int _categoryIndex;
    private bool _overridesEnabled;
    private double _openingScale;

    internal UiShowcaseScreen(UiStyleSheet sheet, UiStyleSheet overrides, Action returnToPause)
    {
        _sheet = sheet;
        _overrides = overrides;
        _returnToPause = returnToPause;

        _shell = new UiShowcaseShell();
        SetStyleSheets(new[] { sheet });

        _controls = UiShowcaseControls.CreateExamples(Report);
        _styles = UiShowcaseStyles.CreateExamples(Report, SetOverrides);
        _bindings = UiShowcaseBindings.CreateExamples(Report);
        _layout = UiShowcaseLayout.CreateExamples(Report);

        PausesGame = true;
        CloseOnEscape = true;

        for (var index = 0; index < _shell.CategoryButtons.Count; index++)
        {
            var category = index;
            _shell.CategoryButtons[index].Click += (_, _) => SelectCategory(category);
        }

        _shell.ReturnButton.Click += (_, _) => ReturnToPause();
        _shell.ScaleDownButton.Click += (_, _) => SetScaleFactor(0.75);
        _shell.ScaleResetButton.Click += (_, _) => SetScaleFactor(1);
        _shell.ScaleUpButton.Click += (_, _) => SetScaleFactor(1.25);
        _shell.RestoreScaleButton.Click += (_, _) => SetScaleFactor(1);

        Root = _shell;
        SelectCategory(0);
        UpdateScaleDisplay();
    }

    internal UiShowcaseShell Shell => _shell;

    internal int CurrentCategory => _categoryIndex;

    internal int CurrentExampleIndex => _exampleIndices[_categoryIndex];

    internal UiShowcaseExample CurrentExample =>
        ExamplesFor(_categoryIndex)[_exampleIndices[_categoryIndex]];

    internal void SelectCategory(int index)
    {
        _categoryIndex = index;
        _shell.SetCategorySelected(index);
        RebuildExampleSwitches();
        ShowCurrentExample();
    }

    internal void SelectExample(int index)
    {
        _exampleIndices[_categoryIndex] = index;
        ShowCurrentExample();
    }

    internal void SetOverrides(bool enabled)
    {
        if (_overridesEnabled == enabled)
            return;

        _overridesEnabled = enabled;
        SetStyleSheets(enabled ? new[] { _sheet, _overrides } : new[] { _sheet });
    }

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

    private void ShowCurrentExample()
    {
        var example = ExamplesFor(_categoryIndex)[_exampleIndices[_categoryIndex]];
        _shell.SetExampleHeader(example.Title, example.Instructions);
        var host = _shell.ContentHost;
        host.Children.Clear();
        host.Children.Add(example.Content);
        UpdateExampleSwitchSelection();
    }

    private void RebuildExampleSwitches()
    {
        var switches = _shell.ExampleSwitches;
        switches.Children.Clear();
        _switchButtons.Clear();

        var examples = ExamplesFor(_categoryIndex);
        for (var index = 0; index < examples.Length; index++)
        {
            var exampleIndex = index;
            var button = UiShowcaseWidgets.ActionButton(
                $"showcase-example-{index}",
                (index + 1).ToString(CultureInfo.InvariantCulture),
                () => SelectExample(exampleIndex));
            button.Classes.Add("showcase-example-switch");
            _switchButtons.Add(button);
            switches.Children.Add(button);
        }
    }

    private void UpdateExampleSwitchSelection()
    {
        var selected = _exampleIndices[_categoryIndex];
        for (var index = 0; index < _switchButtons.Count; index++)
        {
            if (index == selected)
                _switchButtons[index].Classes.Add("selected");
            else
                _switchButtons[index].Classes.Remove("selected");
        }
    }

    private UiShowcaseExample[] ExamplesFor(int category) =>
        category switch
        {
            0 => _controls,
            1 => _styles,
            2 => _bindings,
            3 => _layout,
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

    private void UpdateScaleDisplay() =>
        _shell.SetScaleValue(string.Create(CultureInfo.InvariantCulture, $"缩放 {Scale:0.##}x"));
}
