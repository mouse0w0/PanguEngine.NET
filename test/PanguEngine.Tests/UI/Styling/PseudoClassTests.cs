using PanguEngine.UI.Controls;
using PanguEngine.UI.Styling;

namespace PanguEngine.Tests.UI.Styling;

public sealed class PseudoClassTests
{
    [Fact]
    public void GetReturnsSameInstanceForSameName()
    {
        Assert.Same(PseudoClass.Get("loading"), PseudoClass.Get("loading"));
    }

    [Fact]
    public void GetIsCaseInsensitiveAndNormalizesNameToLowerCase()
    {
        var lower = PseudoClass.Get("loading");
        var upper = PseudoClass.Get("LOADING");
        var mixed = PseudoClass.Get("LoAdInG");

        Assert.Same(lower, upper);
        Assert.Same(lower, mixed);
        Assert.Equal("loading", lower.Name);
        Assert.Equal("loading", upper.Name);
        Assert.Equal("loading", mixed.Name);
    }

    [Fact]
    public void DifferentNamesProduceDifferentInstances()
    {
        var hover = PseudoClass.Get("hover");
        var focus = PseudoClass.Get("focus");

        Assert.NotSame(hover, focus);
        Assert.NotEqual(hover.Name, focus.Name);
    }

    [Fact]
    public void BuiltInIdentifiersComeFromGetWithCanonicalNames()
    {
        Assert.Same(PseudoClass.Hover, PseudoClass.Get("hover"));
        Assert.Same(PseudoClass.Hover, PseudoClass.Get("HOVER"));
        Assert.Same(PseudoClass.Focus, PseudoClass.Get("focus"));
        Assert.Same(PseudoClass.FocusWithin, PseudoClass.Get("focus-within"));
        Assert.Same(PseudoClass.Disabled, PseudoClass.Get("disabled"));
        Assert.Same(PseudoClass.Pressed, PseudoClass.Get("pressed"));
        Assert.Same(PseudoClass.Checked, PseudoClass.Get("checked"));
        Assert.Same(PseudoClass.Indeterminate, PseudoClass.Get("indeterminate"));
        Assert.Same(PseudoClass.ReadOnly, PseudoClass.Get("readonly"));
        Assert.Same(PseudoClass.Horizontal, PseudoClass.Get("horizontal"));
        Assert.Same(PseudoClass.Vertical, PseudoClass.Get("vertical"));
        Assert.Same(PseudoClass.Empty, PseudoClass.Get("empty"));
        Assert.Same(PseudoClass.NoValue, PseudoClass.Get("no-value"));
        Assert.Same(PseudoClass.Selected, PseudoClass.Get("selected"));
        Assert.Same(PseudoClass.Dragging, PseudoClass.Get("dragging"));

        Assert.Equal("hover", PseudoClass.Hover.Name);
        Assert.Equal("focus", PseudoClass.Focus.Name);
        Assert.Equal("disabled", PseudoClass.Disabled.Name);
        Assert.Equal("pressed", PseudoClass.Pressed.Name);
    }

    [Fact]
    public void BuiltInIdentifiersAreDistinct()
    {
        Assert.NotSame(PseudoClass.Hover, PseudoClass.Focus);
        Assert.NotSame(PseudoClass.Hover, PseudoClass.Disabled);
        Assert.NotSame(PseudoClass.Hover, PseudoClass.Pressed);
        Assert.NotSame(PseudoClass.Focus, PseudoClass.Disabled);
        Assert.NotSame(PseudoClass.Focus, PseudoClass.Pressed);
        Assert.NotSame(PseudoClass.Disabled, PseudoClass.Pressed);
    }

    [Theory]
    [InlineData("1bad")]
    [InlineData("bad id")]
    [InlineData(":hover")]
    [InlineData("")]
    public void GetRejectsInvalidIdentifier(string name)
    {
        Assert.Throws<ArgumentException>(() => PseudoClass.Get(name));
    }

    [Fact]
    public void ConcurrentGetPublishesSingleInstance()
    {
        const int count = 8;
        var results = new PseudoClass[count];
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
                results[index] = PseudoClass.Get(index % 2 == 0 ? "race-class" : "RACE-CLASS");
            });
        }

        foreach (var thread in threads)
            thread.Start();
        ready.Wait();
        start.Set();
        foreach (var thread in threads)
            thread.Join();

        var expected = PseudoClass.Get("race-class");
        Assert.All(results, result => Assert.Same(expected, result));
    }

    [Fact]
    public void SelectorDeduplicatesSortsAndExposesReadOnlyIdentifiers()
    {
        var loading = PseudoClass.Get("Loading");
        var selector = StyleSelector.For<Button>(
            pseudoClasses: [PseudoClass.Hover, loading, PseudoClass.Get("hover")]);

        Assert.Equal(2, selector.PseudoClasses.Count);
        Assert.Same(PseudoClass.Hover, selector.PseudoClasses[0]);
        Assert.Same(loading, selector.PseudoClasses[1]);
        Assert.Equal(new[] { "hover", "loading" }, selector.PseudoClasses.Select(pseudoClass => pseudoClass.Name));
        Assert.Equal("Button:hover:loading", selector.SelectorText);
        Assert.Equal(2, selector.ClassAndPseudoCount);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PseudoClass>)selector.PseudoClasses).Add(PseudoClass.Get("focus")));
    }

    [Fact]
    public void CssParsingUsesTheSameIdentifiersAsTheFactory()
    {
        var selector = Assert.Single(StyleSheet.Parse("Button:LOADING:Hover { }").Rules).Selectors[0];

        Assert.Equal(2, selector.PseudoClasses.Count);
        Assert.Same(PseudoClass.Hover, selector.PseudoClasses[0]);
        Assert.Same(PseudoClass.Get("loading"), selector.PseudoClasses[1]);
        Assert.Equal("Button:hover:loading", selector.SelectorText);
    }
}
