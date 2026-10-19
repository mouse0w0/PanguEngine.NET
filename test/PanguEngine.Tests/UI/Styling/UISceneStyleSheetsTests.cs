using PanguEngine.ComponentModel;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

[Collection(UIToolkitCollection.Name)]
public sealed class UISceneStyleSheetsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementCopiesInputAndPreservesOtherSource(bool baseStyles)
    {
        var scene = new UIScene(new Button());
        var other = GetSheets(scene, !baseStyles).ToArray();
        var first = StyleSheet.Parse("Button { background: #010203; }");
        var second = new StyleSheet([]);
        var input = new List<StyleSheet> { first, second };

        SetSheets(scene, baseStyles, input);
        var snapshot = GetSheets(scene, baseStyles);
        input.Clear();

        Assert.Equal(new[] { first, second }, snapshot);
        Assert.Equal(other, GetSheets(scene, !baseStyles));
        var collection = Assert.IsAssignableFrom<IList<StyleSheet>>(snapshot);
        Assert.Throws<NotSupportedException>(() => collection[0] = second);
        Assert.Throws<NotSupportedException>(() => collection.Add(first));
    }

    [Fact]
    public void ClearingSourcesFallsBackWithoutReinsertingDefaultSheet()
    {
        var button = new Button();
        var scene = new UIScene(button);
        scene.SetBaseStyleSheets([StyleSheet.Parse("Button { background: #010203; }")]);
        scene.SetStyleSheets([StyleSheet.Parse("Button { background: #040506; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(4, 5, 6), button.Background);

        scene.SetStyleSheets([]);
        scene.Root!.UpdateStyles();

        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
        Assert.Equal(StyleOrigin.Base, Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty)).Origin);

        scene.SetBaseStyleSheets([]);
        scene.Root!.UpdateStyles();

        Assert.Empty(scene.BaseStyleSheets);
        Assert.Empty(scene.StyleSheets);
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
        var scene = new UIScene(button);
        var sheet = StyleSheet.Parse("Button { background: #010203; }");
        SetSheets(scene, baseStyles, [sheet]);
        scene.Root!.UpdateStyles();
        var resolver = scene.StyleResolver;
        var changes = 0;
        button.PropertyChanged += (_, _) => changes++;

        SetSheets(scene, baseStyles, [sheet]);
        scene.Root!.UpdateStyles();

        Assert.Same(resolver, scene.StyleResolver);
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerationFailurePreservesBothSourcesAndAllowsRetry(bool baseStyles)
    {
        var button = new Button();
        var scene = new UIScene(button);
        var previous = scene.StyleResolver;
        var background = button.Background;
        var expected = new InvalidOperationException("enumeration failed");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            SetSheets(scene, baseStyles, ThrowingSheets(expected)));

        Assert.Same(expected, actual);
        Assert.Same(previous, scene.StyleResolver);
        Assert.Same(previous.BaseStyleSheets, scene.BaseStyleSheets);
        Assert.Same(previous.StyleSheets, scene.StyleSheets);
        Assert.Equal(background, button.Background);

        SetSheets(scene, baseStyles, [StyleSheet.Parse("Button { background: #010203; }")]);
        scene.Root!.UpdateStyles();
        Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingFailurePreservesNodeStylesAndKeepsNewConfiguration(bool baseStyles)
    {
        var button = new Button();
        var scene = new UIScene(button);
        var previous = scene.StyleResolver;
        var background = button.Background;

        var invalid = StyleSheet.Parse("Button { background: nope; }");
        SetSheets(scene, baseStyles, [invalid]);
        var error = Assert.Throws<StyleParseException>(scene.Root!.UpdateStyles);

        Assert.Equal(StyleParseError.InvalidValue, error.Error);
        Assert.NotSame(previous, scene.StyleResolver);
        Assert.Same(invalid, Assert.Single(GetSheets(scene, baseStyles)));
        Assert.Equal(background, button.Background);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongThreadRejectsNewSequenceButAllowsCurrentSnapshot(bool baseStyles)
    {
        var scene = new UIScene(new Button());
        var current = GetSheets(scene, baseStyles);
        scene.Open();
        Exception? mutationError = null;
        Exception? noOpError = null;
        var thread = new Thread(() =>
        {
            mutationError = Record.Exception(() => SetSheets(scene, baseStyles, [new StyleSheet([])]));
            noOpError = Record.Exception(() => SetSheets(scene, baseStyles, current));
        });
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(mutationError);
        Assert.Null(noOpError);
        Assert.Same(current, GetSheets(scene, baseStyles));
        scene.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparationRejectsTreeAndLifecycleChangesButAllowsClasses(bool open)
    {
        var root = new Panel();
        var scene = new UIScene(root);
        scene.Root!.UpdateStyles();
        if (open)
            scene.Open();
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
            rootError = Record.Exception(() => scene.Root = new Panel());
            childError = Record.Exception(() => root.Children.Add(new Panel()));
            authorError = Record.Exception(() => scene.SetStyleSheets([]));
            baseStylesError = Record.Exception(() => scene.SetBaseStyleSheets([]));
            classesError = Record.Exception(() => root.Classes.Add("blocked"));
            idError = Record.Exception(() => root.StyleId = "blocked");
            lifecycleError = Record.Exception(() =>
            {
                if (open)
                    scene.Close();
                else
                    scene.Open();
            });
        });

        scene.SetStyleSheets(sheets);

        Assert.IsType<InvalidOperationException>(rootError);
        Assert.IsType<InvalidOperationException>(childError);
        Assert.IsType<InvalidOperationException>(lifecycleError);
        Assert.IsType<InvalidOperationException>(authorError);
        Assert.IsType<InvalidOperationException>(baseStylesError);
        Assert.Null(classesError);
        Assert.IsType<InvalidOperationException>(idError);
        Assert.Same(root, scene.Root);
        Assert.Empty(root.Children);
        Assert.Equal("blocked", Assert.Single(root.Classes));
        Assert.Equal(1, collectionNotifications);
        Assert.Null(root.StyleId);
        Assert.Equal(open, scene.IsOpen());
        if (open)
            scene.Close();
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
        var scene = new UIScene(node);
        var sheet = StyleSheet.Parse(".pending { opacity: 0.4; }");
        SetSheets(scene, baseStyles, [sheet]);
        scene.Root!.UpdateStyles();
        var resolver = scene.StyleResolver;
        var current = GetSheets(scene, baseStyles);
        var expected = new InvalidOperationException("enumeration failed");
        var collectionNotifications = 0;
        node.Classes.Changed += (_, _) =>
        {
            collectionNotifications++;
            Assert.False(node.IsStyleValid);
            Assert.Equal(1d, node.Opacity);
        };

        IEnumerable<StyleSheet> Input()
        {
            node.Classes.Add("pending");
            yield return sheet;
            if (enumerationFails)
                throw expected;
        }

        var error = Record.Exception(() => SetSheets(scene, baseStyles, Input()));

        if (enumerationFails)
            Assert.Same(expected, error);
        else
            Assert.Null(error);
        Assert.Same(resolver, scene.StyleResolver);
        Assert.Same(current, GetSheets(scene, baseStyles));
        Assert.Equal("pending", Assert.Single(node.Classes));
        Assert.Equal(1, collectionNotifications);
        Assert.False(node.IsStyleValid);
        Assert.False(node.IsStyleSubtreeValid);
        Assert.Equal(1d, node.Opacity);

        scene.Root!.UpdateStyles();

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
        var scene = new UIScene(root);
        var previous = scene.StyleResolver;
        CallbackNode.OnConvert = () => root.Children.Clear();
        try
        {
            scene.SetStyleSheets([StyleSheet.Parse("CallbackNode { callback-value: 1; }")]);
            scene.Root!.UpdateStyles();

            Assert.Empty(root.Children);
            Assert.Null(node.Parent);
            Assert.Null(node.Scene);
            Assert.False(node.IsStyleSubtreeValid);
            Assert.NotSame(previous, scene.StyleResolver);
        }
        finally
        {
            CallbackNode.OnConvert = null;
        }
    }

    [Fact]
    public void ConverterCanInvalidateStyleInputsButCannotReplaceSceneSources()
    {
        var node = new CallbackNode();
        var scene = new UIScene(node);
        scene.Open();
        var errors = new List<Exception?>();
        CallbackNode.OnConvert = () =>
        {
            errors.Add(Record.Exception(() => scene.SetStyleSheets([])));
            errors.Add(Record.Exception(() => scene.SetBaseStyleSheets([])));
            errors.Add(Record.Exception(() => node.Classes.Add("blocked")));
            errors.Add(Record.Exception(() => node.StyleId = "blocked"));
        };
        try
        {
            scene.SetStyleSheets([StyleSheet.Parse("CallbackNode { callback-value: 1; }")]);
            scene.PrepareFrame(new Size(100, 100));

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
            scene.Close();
        }
    }

    [Fact]
    public void NotificationRejectsReplacingSourcesButAllowsInvalidatingClasses()
    {
        var button = new Button();
        var scene = new UIScene(button);
        Exception? authorError = null;
        Exception? baseStylesError = null;
        Exception? classesError = null;
        button.PropertyChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Property, Region.BackgroundProperty))
                return;
            authorError = Record.Exception(() => scene.SetStyleSheets([]));
            baseStylesError = Record.Exception(() => scene.SetBaseStyleSheets([]));
            classesError = Record.Exception(() => button.Classes.Add("other"));
        };

        scene.SetStyleSheets([StyleSheet.Parse("Button { background: #010203; }")]);
        scene.Open();
        try
        {
            scene.PrepareFrame(new Size(100, 100));

            Assert.IsType<InvalidOperationException>(authorError);
            Assert.IsType<InvalidOperationException>(baseStylesError);
            Assert.Null(classesError);
            Assert.Equal("other", Assert.Single(button.Classes));
            Assert.Equal(new SolidColorBrush(1, 2, 3), button.Background);
        }
        finally
        {
            scene.Close();
        }
    }

    [Fact]
    public void DetachingRestoresEngineDefaultInsteadOfSceneSources()
    {
        var button = new Button();
        var scene = new UIScene(button);
        scene.SetBaseStyleSheets([StyleSheet.Parse("Button { background: #010203; }")]);
        scene.SetStyleSheets([StyleSheet.Parse("Button { background: #040506; }")]);

        scene.Root = null;
        button.UpdateStyles();

        Assert.Null(button.Scene);
        Assert.Equal(new SolidColorBrush(48, 54, 62), button.Background);
        Assert.Equal(StyleOrigin.Base, Assert.Single(button.GetStyleValueSources(Region.BackgroundProperty)).Origin);
    }

    private static void SetSheets(UIScene scene, bool baseStyles, IEnumerable<StyleSheet> sheets)
    {
        if (baseStyles)
            scene.SetBaseStyleSheets(sheets);
        else
            scene.SetStyleSheets(sheets);
    }

    private static IReadOnlyList<StyleSheet> GetSheets(UIScene scene, bool baseStyles) =>
        baseStyles ? scene.BaseStyleSheets : scene.StyleSheets;

    private static IEnumerable<StyleSheet> ThrowingSheets(Exception exception)
    {
        yield return new StyleSheet([]);
        throw exception;
    }

    private static IEnumerable<StyleSheet> CallbackSheets(Action callback)
    {
        callback();
        yield return new StyleSheet([]);
    }

    private sealed class CallbackNode : UIElement
    {
        static CallbackNode()
        {
            CssRegistry.RegisterProperty<CallbackNode, double>("callback-value", ValueProperty, _ =>
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