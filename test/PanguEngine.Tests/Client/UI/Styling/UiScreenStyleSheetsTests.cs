using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;

using PanguEngine.ComponentModel;

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
        screen.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(4, 5, 6), button.Background);

        screen.SetStyleSheets([]);
        screen.Root!.UpdateStyles();

        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
        Assert.Equal(UiStyleOrigin.Base, Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty)).Origin);

        screen.SetBaseStyleSheets([]);
        screen.Root!.UpdateStyles();

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
        screen.Root!.UpdateStyles();
        var resolver = screen.StyleResolver;
        var changes = 0;
        button.PropertyChanged += (_, _) => changes++;

        SetSheets(screen, baseStyles, [sheet]);
        screen.Root!.UpdateStyles();

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
        screen.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingFailurePreservesNodeStylesAndKeepsNewConfiguration(bool baseStyles)
    {
        var button = new Button();
        var screen = new UiScreen(button);
        var previous = screen.StyleResolver;
        var background = button.Background;

        var invalid = UiStyleSheet.Parse("Button { background: nope; }");
        SetSheets(screen, baseStyles, [invalid]);
        var error = Assert.Throws<UiStyleParseException>(screen.Root!.UpdateStyles);

        Assert.Equal(UiStyleParseError.InvalidValue, error.Error);
        Assert.NotSame(previous, screen.StyleResolver);
        Assert.Same(invalid, Assert.Single(GetSheets(screen, baseStyles)));
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
    public void PreparationRejectsTreeAndLifecycleChangesButAllowsClasses(bool open)
    {
        var root = new Panel();
        var screen = new UiScreen(root);
        screen.Root!.UpdateStyles();
        if (open)
            screen.Open();
        var collectionNotifications = 0;
        root.Classes.Changed += (_, _) =>
        {
            collectionNotifications++;
            Assert.False(root.IsStyleValid);
        };
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
        Assert.Null(classesError);
        Assert.IsType<InvalidOperationException>(idError);
        Assert.Same(root, screen.Root);
        Assert.Empty(root.Children);
        Assert.Equal("blocked", Assert.Single(root.Classes));
        Assert.Equal(1, collectionNotifications);
        Assert.Null(root.StyleId);
        Assert.Equal(open, screen.IsOpen());
        if (open)
            screen.Close();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ClassChangesDuringEnumerationRemainPendingWhenConfigurationIsUnchanged(
        bool baseStyles, bool enumerationFails)
    {
        var node = new Panel();
        var screen = new UiScreen(node);
        var sheet = UiStyleSheet.Parse(".pending { opacity: 0.4; }");
        SetSheets(screen, baseStyles, [sheet]);
        screen.Root!.UpdateStyles();
        var resolver = screen.StyleResolver;
        var current = GetSheets(screen, baseStyles);
        var expected = new InvalidOperationException("enumeration failed");
        var collectionNotifications = 0;
        node.Classes.Changed += (_, _) =>
        {
            collectionNotifications++;
            Assert.False(node.IsStyleValid);
            Assert.Equal(1d, node.Opacity);
        };
        IEnumerable<UiStyleSheet> Input()
        {
            node.Classes.Add("pending");
            yield return sheet;
            if (enumerationFails)
                throw expected;
        }

        var error = Record.Exception(() => SetSheets(screen, baseStyles, Input()));

        if (enumerationFails)
            Assert.Same(expected, error);
        else
            Assert.Null(error);
        Assert.Same(resolver, screen.StyleResolver);
        Assert.Same(current, GetSheets(screen, baseStyles));
        Assert.Equal("pending", Assert.Single(node.Classes));
        Assert.Equal(1, collectionNotifications);
        Assert.False(node.IsStyleValid);
        Assert.False(node.IsStyleSubtreeValid);
        Assert.Equal(1d, node.Opacity);

        screen.Root!.UpdateStyles();

        Assert.Equal(0.4, node.Opacity);
        Assert.True(node.IsStyleValid);
        Assert.True(node.IsStyleSubtreeValid);
    }

    [Fact]
    public void ConverterCanDetachTheCurrentNodeWithoutLosingItsInvalidation()
    {
        var root = new Panel();
        var node = new CallbackNode();
        root.Children.Add(node);
        var screen = new UiScreen(root);
        var previous = screen.StyleResolver;
        CallbackNode.OnConvert = () => root.Children.Clear();
        try
        {
            screen.SetStyleSheets([UiStyleSheet.Parse("CallbackNode { callback-value: 1; }")]);
            screen.Root!.UpdateStyles();

            Assert.Empty(root.Children);
            Assert.Null(node.Parent);
            Assert.Null(node.Screen);
            Assert.False(node.IsStyleSubtreeValid);
            Assert.NotSame(previous, screen.StyleResolver);
        }
        finally
        {
            CallbackNode.OnConvert = null;
        }
    }

    [Fact]
    public void ConverterCanInvalidateStyleInputsButCannotReplaceScreenSources()
    {
        var node = new CallbackNode();
        var screen = new UiScreen(node);
        screen.Open();
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
            screen.PrepareFrame(new Size(100, 100), 0);

            Assert.Equal(4, errors.Count);
            Assert.IsType<InvalidOperationException>(errors[0]);
            Assert.IsType<InvalidOperationException>(errors[1]);
            Assert.Null(errors[2]);
            Assert.Null(errors[3]);
            Assert.Equal("blocked", Assert.Single(node.Classes));
            Assert.Equal("blocked", node.StyleId);
            Assert.True(node.IsStyleSubtreeValid);
            Assert.Equal(1d, node.GetValue(CallbackNode.ValueProperty));
        }
        finally
        {
            CallbackNode.OnConvert = null;
            screen.Close();
        }
    }

    [Fact]
    public void NotificationRejectsReplacingSourcesButAllowsInvalidatingClasses()
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
        screen.Open();
        try
        {
            screen.PrepareFrame(new Size(100, 100), 0);

            Assert.IsType<InvalidOperationException>(authorError);
            Assert.IsType<InvalidOperationException>(baseStylesError);
            Assert.Null(classesError);
            Assert.Equal("other", Assert.Single(button.Classes));
            Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
        }
        finally
        {
            screen.Close();
        }
    }

    [Fact]
    public void DetachingRestoresEngineDefaultInsteadOfScreenSources()
    {
        var button = new Button();
        var screen = new UiScreen(button);
        screen.SetBaseStyleSheets([UiStyleSheet.Parse("Button { background: #010203; }")]);
        screen.SetStyleSheets([UiStyleSheet.Parse("Button { background: #040506; }")]);

        screen.Root = null;
        button.UpdateStyles();

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

        internal static readonly Property<double> ValueProperty = Property.Register<CallbackNode, double>(
            "Value", 0);
    }
}
