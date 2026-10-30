using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Templates;

namespace PanguEngine.Tests.UI;

public sealed class ContentTemplateTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 1)]
    [InlineData("value", 1)]
    public void TemplateIsOnlyCalledForNonNullContentAndNullResultsStayEmpty(string? content, int expectedCalls)
    {
        var calls = 0;
        var presenter = new ContentHost
        {
            ContentTemplate = new FuncDataTemplate<string>(_ =>
            {
                calls++;
                return null;
            }),
            Content = content
        };

        Assert.Null(presenter.Root);
        Assert.Equal(expectedCalls, calls);
    }

    [Fact]
    public void ExplicitTemplateUsesElementAsDataWithoutMovingIt()
    {
        var source = new Panel();
        var data = new Panel();
        source.Children.Add(data);
        var root = new Panel();
        var presenter = new ContentHost
        {
            ContentTemplate = new FuncDataTemplate<UIElement>(value =>
            {
                Assert.Same(data, value);
                return root;
            }),
            Content = data
        };

        Assert.Same(root, presenter.Root);
        Assert.Same(source, data.Parent);
        Assert.Same(data, presenter.Content);
    }

    [Fact]
    public void TypeFailureCommitsInputWithoutFallback()
    {
        var presenter = new ContentHost
        {
            ContentTemplate = new FuncDataTemplate<string>(_ => new Panel()),
            Content = "initial"
        };
        var next = new object();

        Assert.Throws<ArgumentException>(() => presenter.Content = next);

        Assert.Same(next, presenter.Content);
    }

    [Fact]
    public void TemplateReentryIsRejectedAndTheGuardIsReleasedAfterFailure()
    {
        var presenter = new ContentHost();
        var data = new object();
        presenter.ContentTemplate = new FuncDataTemplate<object>(_ =>
        {
            presenter.Content = new object();
            return new Panel();
        });

        Assert.Throws<InvalidOperationException>(() => presenter.Content = data);
        Assert.Same(data, presenter.Content);

        presenter.ContentTemplate = new FuncDataTemplate<object>(_ => new Panel());
        Assert.NotNull(presenter.Root);
    }

    [Fact]
    public void InputNotificationAcceptsTheSameReferenceButRejectsTemplateChanges()
    {
        var presenter = new ContentHost
        {
            ContentTemplate = new FuncDataTemplate<object>(_ => new Panel())
        };
        var data = new object();
        using var subscription = presenter.Subscribe(ContentControl.ContentProperty, (_, _) =>
        {
            Assert.NotNull(presenter.Root);
            presenter.Content = data;
            Assert.Throws<InvalidOperationException>(() => presenter.ContentTemplate = null);
        });

        presenter.Content = data;

        Assert.Same(data, presenter.Content);
    }

    private sealed class ContentHost : ContentControl
    {
        internal UIElement? Root => ContentRoot;
    }
}
