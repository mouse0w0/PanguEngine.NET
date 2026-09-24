using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Tests.Client.UI.Styling;

public sealed class UiPseudoClassTests
{
    [Fact]
    public void GetReturnsSameInstanceForSameName()
    {
        Assert.Same(UiPseudoClass.Get("loading"), UiPseudoClass.Get("loading"));
    }

    [Fact]
    public void GetIsCaseInsensitiveAndNormalizesNameToLowerCase()
    {
        var lower = UiPseudoClass.Get("loading");
        var upper = UiPseudoClass.Get("LOADING");
        var mixed = UiPseudoClass.Get("LoAdInG");

        Assert.Same(lower, upper);
        Assert.Same(lower, mixed);
        Assert.Equal("loading", lower.Name);
        Assert.Equal("loading", upper.Name);
        Assert.Equal("loading", mixed.Name);
    }

    [Fact]
    public void DifferentNamesProduceDifferentInstances()
    {
        var hover = UiPseudoClass.Get("hover");
        var focus = UiPseudoClass.Get("focus");

        Assert.NotSame(hover, focus);
        Assert.NotEqual(hover.Name, focus.Name);
    }

    [Fact]
    public void BuiltInIdentifiersComeFromGetWithCanonicalNames()
    {
        Assert.Same(UiPseudoClass.Hover, UiPseudoClass.Get("hover"));
        Assert.Same(UiPseudoClass.Hover, UiPseudoClass.Get("HOVER"));
        Assert.Same(UiPseudoClass.Focus, UiPseudoClass.Get("focus"));
        Assert.Same(UiPseudoClass.Disabled, UiPseudoClass.Get("disabled"));
        Assert.Same(UiPseudoClass.Pressed, UiPseudoClass.Get("pressed"));

        Assert.Equal("hover", UiPseudoClass.Hover.Name);
        Assert.Equal("focus", UiPseudoClass.Focus.Name);
        Assert.Equal("disabled", UiPseudoClass.Disabled.Name);
        Assert.Equal("pressed", UiPseudoClass.Pressed.Name);
    }

    [Fact]
    public void BuiltInIdentifiersAreDistinct()
    {
        Assert.NotSame(UiPseudoClass.Hover, UiPseudoClass.Focus);
        Assert.NotSame(UiPseudoClass.Hover, UiPseudoClass.Disabled);
        Assert.NotSame(UiPseudoClass.Hover, UiPseudoClass.Pressed);
        Assert.NotSame(UiPseudoClass.Focus, UiPseudoClass.Disabled);
        Assert.NotSame(UiPseudoClass.Focus, UiPseudoClass.Pressed);
        Assert.NotSame(UiPseudoClass.Disabled, UiPseudoClass.Pressed);
    }

    [Theory]
    [InlineData("1bad")]
    [InlineData("bad id")]
    [InlineData(":hover")]
    [InlineData("")]
    public void GetRejectsInvalidIdentifier(string name)
    {
        Assert.Throws<ArgumentException>(() => UiPseudoClass.Get(name));
    }

    [Fact]
    public void ConcurrentGetPublishesSingleInstance()
    {
        const int count = 8;
        var results = new UiPseudoClass[count];
        var threads = new Thread[count];
        using var ready = new CountdownEvent(count);
        using var start = new ManualResetEventSlim();
        for (var i = 0; i < count; i++)
        {
            var index = i;
            threads[i] = new Thread(() =>
            {
                ready.Signal();
                start.Wait();
                results[index] = UiPseudoClass.Get(index % 2 == 0 ? "race-class" : "RACE-CLASS");
            });
        }

        foreach (var thread in threads)
            thread.Start();
        ready.Wait();
        start.Set();
        foreach (var thread in threads)
            thread.Join();

        var expected = UiPseudoClass.Get("race-class");
        Assert.All(results, result => Assert.Same(expected, result));
    }

    [Fact]
    public void SelectorDeduplicatesSortsAndExposesReadOnlyIdentifiers()
    {
        var loading = UiPseudoClass.Get("Loading");
        var selector = UiStyleSelector.For<Button>(
            pseudoClasses: [UiPseudoClass.Hover, loading, UiPseudoClass.Get("hover")]);

        Assert.Equal(2, selector.PseudoClasses.Count);
        Assert.Same(UiPseudoClass.Hover, selector.PseudoClasses[0]);
        Assert.Same(loading, selector.PseudoClasses[1]);
        Assert.Equal(new[] { "hover", "loading" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Equal("Button:hover:loading", selector.SelectorText);
        Assert.Equal(2, selector.ClassAndPseudoCount);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<UiPseudoClass>)selector.PseudoClasses).Add(UiPseudoClass.Get("focus")));
    }

    [Fact]
    public void CssParsingUsesTheSameIdentifiersAsTheFactory()
    {
        var selector = Assert.Single(UiStyleSheet.Parse("Button:LOADING:Hover { }").Rules).Selectors[0];

        Assert.Equal(2, selector.PseudoClasses.Count);
        Assert.Same(UiPseudoClass.Hover, selector.PseudoClasses[0]);
        Assert.Same(UiPseudoClass.Get("loading"), selector.PseudoClasses[1]);
        Assert.Equal("Button:hover:loading", selector.SelectorText);
    }
}
