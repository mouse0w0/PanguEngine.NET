using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiScreenStyleSheetsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementCopiesInputAndPreservesOtherSource(bool baseStyles)
    {
        var screen = new UiScreen(new Button());
        var other = GetSheets(screen, !baseStyles).ToArray();
        var first = UiStyleSheet.Parse("Button { background: #010203; }");
        var second = new UiStyleSheet([]);
        var input = new List<UiStyleSheet> { first, second };

        SetSheets(screen, baseStyles, input);
        var snapshot = GetSheets(screen, baseStyles);
        input.Clear();

        Assert.Equal(new[] { first, second }, snapshot);
        Assert.Equal(other, GetSheets(screen, !baseStyles));
        var collection = Assert.IsAssignableFrom<IList<UiStyleSheet>>(snapshot);
        Assert.Throws<NotSupportedException>(() => collection[0] = second);
        Assert.Throws<NotSupportedException>(() => collection.Add(first));
    }

    [Fact]
    public void ClearingSourcesFallsBackWithoutReinsertingDefaultSheet()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("Button { background: #010203; }")]);
        screen.SetStyleSheets([UiStyleSheet.Parse("Button { background: #040506; }")]);
        Assert.Equal(new SolidColorBrush(4, 5, 6), button.Background);

        screen.SetStyleSheets([]);

        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
        Assert.Equal(UiStyleOrigin.Base, Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty)).Origin);

        screen.SetBaseStyleSheets([]);

        Assert.Empty(screen.BaseStyleSheets);
        Assert.Empty(screen.StyleSheets);
        Assert.Null(button.Background);
        Assert.Empty(button.GetStyleValueSources(Region.BackgroundProperty));
        Assert.Equal(Thickness.Zero, button.Padding);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualSheetSequenceRetainsResolverAndDoesNotNotify(bool baseStyles)
    {
        var button = new Button();
        var screen = new UiScreen(button);
        var sheet = UiStyleSheet.Parse("Button { background: #010203; }");
        SetSheets(screen, baseStyles, [sheet]);
        var resolver = screen.StyleResolver;
        var changes = 0;
        button.PropertyChanged += (_, _) => changes++;

        SetSheets(screen, baseStyles, [sheet]);

        Assert.Same(resolver, screen.StyleResolver);
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerationFailurePreservesBothSourcesAndAllowsRetry(bool baseStyles)
    {
        var button = new Button();
        var screen = new UiScreen(button);
        var previous = screen.StyleResolver;
        var background = button.Background;
        var expected = new InvalidOperationException("enumeration failed");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            SetSheets(screen, baseStyles, ThrowingSheets(expected)));

        Assert.Same(expected, actual);
        Assert.Same(previous, screen.StyleResolver);
        Assert.Same(previous.BaseStyleSheets, screen.BaseStyleSheets);
        Assert.Same(previous.StyleSheets, screen.StyleSheets);
        Assert.Equal(background, button.Background);

        SetSheets(screen, baseStyles, [UiStyleSheet.Parse("Button { background: #010203; }")]);
        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingFailurePreservesBothSourcesAndNodeStyles(bool baseStyles)
    {
        var button = new Button();
        var screen = new UiScreen(button);
        var previous = screen.StyleResolver;
        var background = button.Background;

        var error = Assert.Throws<UiStyleParseException>(() => SetSheets(
            screen, baseStyles, [UiStyleSheet.Parse("Button { background: nope; }")]));

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.Same(previous, screen.StyleResolver);
        Assert.Same(previous.BaseStyleSheets, screen.BaseStyleSheets);
        Assert.Same(previous.StyleSheets, screen.StyleSheets);
        Assert.Equal(background, button.Background);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongThreadRejectsNewSequenceButAllowsCurrentSnapshot(bool baseStyles)
    {
        var screen = new UiScreen(new Button());
        var current = GetSheets(screen, baseStyles);
        screen.Open();
        Exception? mutationError = null;
        Exception? noOpError = null;
        var thread = new Thread(() =>
        {
            mutationError = Record.Exception(() => SetSheets(screen, baseStyles, [new UiStyleSheet([])]));
            noOpError = Record.Exception(() => SetSheets(screen, baseStyles, current));
        });
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(mutationError);
        Assert.Null(noOpError);
        Assert.Same(current, GetSheets(screen, baseStyles));
        screen.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparationRejectsTreeAndLifecycleChangesFromEnumeration(bool open)
    {
        var root = new Panel();
        var screen = new UiScreen(root);
        if (open)
            screen.Open();
        Exception? rootError = null;
        Exception? childError = null;
        Exception? lifecycleError = null;
        Exception? authorError = null;
        Exception? baseStylesError = null;
        Exception? classesError = null;
        Exception? idError = null;
        var sheets = CallbackSheets(() =>
        {
            rootError = Record.Exception(() => screen.Root = new Panel());
            childError = Record.Exception(() => root.Children.Add(new Panel()));
            authorError = Record.Exception(() => screen.SetStyleSheets([]));
            baseStylesError = Record.Exception(() => screen.SetBaseStyleSheets([]));
            classesError = Record.Exception(() => root.Classes.Add("blocked"));
            idError = Record.Exception(() => root.StyleId = "blocked");
            lifecycleError = Record.Exception(() =>
            {
                if (open)
                    screen.Close();
                else
                    screen.Open();
            });
        });

        screen.SetStyleSheets(sheets);

        Assert.IsType<InvalidOperationException>(rootError);
        Assert.IsType<InvalidOperationException>(childError);
        Assert.IsType<InvalidOperationException>(lifecycleError);
        Assert.IsType<InvalidOperationException>(authorError);
        Assert.IsType<InvalidOperationException>(baseStylesError);
        Assert.IsType<InvalidOperationException>(classesError);
        Assert.IsType<InvalidOperationException>(idError);
        Assert.Same(root, screen.Root);
        Assert.Empty(root.Children);
        Assert.Empty(root.Classes);
        Assert.Null(root.StyleId);
        Assert.Equal(open, screen.IsOpen());
        if (open)
            screen.Close();
    }

    [Fact]
    public void PreparationRejectsStructureChangesFromConverter()
    {
        var root = new Panel();
        var node = new CallbackNode();
        root.Children.Add(node);
        var screen = new UiScreen(root);
        var previous = screen.StyleResolver;
        CallbackNode.OnConvert = () => root.Children.Clear();
        try
        {
            var error = Assert.Throws<UiStyleParseException>(() =>
                screen.SetStyleSheets([UiStyleSheet.Parse("CallbackNode { callback-value: 1; }")]));

            Assert.IsType<InvalidOperationException>(error.InnerException);
            Assert.Same(node, Assert.Single(root.Children));
            Assert.Same(previous, screen.StyleResolver);
        }
        finally
        {
            CallbackNode.OnConvert = null;
        }
    }

    [Fact]
    public void PreparationRejectsStyleInputsAndSourceReplacementFromConverter()
    {
        var node = new CallbackNode();
        var screen = new UiScreen(node);
        var errors = new List<Exception?>();
        CallbackNode.OnConvert = () =>
        {
            errors.Add(Record.Exception(() => screen.SetStyleSheets([])));
            errors.Add(Record.Exception(() => screen.SetBaseStyleSheets([])));
            errors.Add(Record.Exception(() => node.Classes.Add("blocked")));
            errors.Add(Record.Exception(() => node.StyleId = "blocked"));
        };
        try
        {
            screen.SetStyleSheets([UiStyleSheet.Parse("CallbackNode { callback-value: 1; }")]);

            Assert.Equal(4, errors.Count);
            Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
            Assert.Empty(node.Classes);
            Assert.Null(node.StyleId);
            Assert.Equal(1d, node.GetValue(CallbackNode.ValueProperty));
        }
        finally
        {
            CallbackNode.OnConvert = null;
        }
    }

    [Fact]
    public void NotificationRejectsReplacingEitherSourceAndChangingClasses()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        Exception? authorError = null;
        Exception? baseStylesError = null;
        Exception? classesError = null;
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty))
                return;
            authorError = Record.Exception(() => screen.SetStyleSheets([]));
            baseStylesError = Record.Exception(() => screen.SetBaseStyleSheets([]));
            classesError = Record.Exception(() => button.Classes.Add("other"));
        };

        screen.SetStyleSheets([UiStyleSheet.Parse("Button { background: #010203; }")]);

        Assert.IsType<InvalidOperationException>(authorError);
        Assert.IsType<InvalidOperationException>(baseStylesError);
        Assert.IsType<InvalidOperationException>(classesError);
        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
    }

    [Fact]
    public void DetachingRestoresEngineDefaultInsteadOfScreenSources()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("Button { background: #010203; }")]);
        screen.SetStyleSheets([UiStyleSheet.Parse("Button { background: #040506; }")]);

        screen.Root = null;

        Assert.Null(button.Screen);
        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);
        Assert.Equal(UiStyleOrigin.Base, Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty)).Origin);
    }

    private static void SetSheets(UiScreen screen, bool baseStyles, IEnumerable<UiStyleSheet> sheets)
    {
        if (baseStyles)
            screen.SetBaseStyleSheets(sheets);
        else
            screen.SetStyleSheets(sheets);
    }

    private static IReadOnlyList<UiStyleSheet> GetSheets(UiScreen screen, bool baseStyles) =>
        baseStyles ? screen.BaseStyleSheets : screen.StyleSheets;

    private static IEnumerable<UiStyleSheet> ThrowingSheets(Exception exception)
    {
        yield return new UiStyleSheet([]);
        throw exception;
    }

    private static IEnumerable<UiStyleSheet> CallbackSheets(Action callback)
    {
        callback();
        yield return new UiStyleSheet([]);
    }

    private sealed class CallbackNode : UiNode
    {
        static CallbackNode()
        {
            UiCssRegistry.RegisterProperty<CallbackNode, double>("callback-value", ValueProperty, _ =>
            {
                OnConvert?.Invoke();
                return 1d;
            });
        }

        internal static Action? OnConvert;

        internal static readonly UiProperty<double> ValueProperty = UiProperty.Register<CallbackNode, double>(
            "Value", 0);
    }
}
