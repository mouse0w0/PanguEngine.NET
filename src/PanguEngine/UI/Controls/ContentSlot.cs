using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

/// <summary>Manages one content value and its directly hosted presentation without adding a UI node.</summary>
internal sealed class ContentSlot
{
    private readonly Container _host;
    private readonly UIElement _owner;
    private readonly Action _verifyOwnerAccess;
    private readonly Action<object?, object?> _contentChanged;
    private readonly Action<IDataTemplate?, IDataTemplate?> _templateChanged;
    private bool _isUpdating;

    internal ContentSlot(
        Container host,
        UIElement owner,
        Action verifyOwnerAccess,
        Action<object?, object?> contentChanged,
        Action<IDataTemplate?, IDataTemplate?> templateChanged)
    {
        _host = host;
        _owner = owner;
        _verifyOwnerAccess = verifyOwnerAccess;
        _contentChanged = contentChanged;
        _templateChanged = templateChanged;
    }

    internal string? DefaultTextClass { get; set; }

    internal UIElement? Root { get; private set; }

    internal object? Content
    {
        get;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(field, value))
                return;
            BeginUpdate();
            try
            {
                var previous = field;
                field = value;
                ApplyContent();
                _contentChanged(previous, value);
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    internal IDataTemplate? Template
    {
        get;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(field, value))
                return;
            BeginUpdate();
            try
            {
                var previous = field;
                field = value;
                ApplyContent();
                _templateChanged(previous, value);
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    private void VerifyAccess()
    {
        _verifyOwnerAccess();
        if (!ReferenceEquals(_owner, _host))
            _host.Scene?.VerifyTreeMutationAccess();
    }

    private void BeginUpdate()
    {
        if (_isUpdating)
            throw new InvalidOperationException("Content cannot change during its update or notifications.");
        _isUpdating = true;
    }

    private void ApplyContent()
    {
        var root = BuildContent();
        if (!ReferenceEquals(Root, root))
        {
            if (Root is null)
            {
                if (root is not null)
                    _host.Children.Add(root);
            }
            else if (root is null)
                _host.Children.Remove(Root);
            else
                _host.Children[_host.Children.IndexOf(Root)] = root;
        }
        Root = root;
    }

    private UIElement? BuildContent()
    {
        if (Content is not { } content)
            return null;
        if (Template is { } template)
            return template.Build(content);
        if (content is UIElement element)
            return element;
        var value = content as string ?? content.ToString();
        if (string.IsNullOrEmpty(value))
            return null;
        var text = new Text { Content = value, IsHitTestVisible = false };
        if (DefaultTextClass is { } className)
            text.Classes.Add(className);
        return text;
    }
}
