namespace PanguEngine.UI.Templates;

/// <summary>Creates visual content through a strongly typed factory.</summary>
/// <typeparam name="T">The supported content type.</typeparam>
public sealed class FuncDataTemplate<T> : IDataTemplate
{
    private readonly Func<T, UIElement?> _build;

    /// <summary>Initializes a template with a content factory.</summary>
    /// <param name="build">The factory that creates an independent, unparented root.</param>
    /// <exception cref="ArgumentNullException">Thrown when the factory is null.</exception>
    public FuncDataTemplate(Func<T, UIElement?> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        _build = build;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when the content is not assignable to T.</exception>
    public UIElement? Build(object data)
    {
        if (data is not T value)
            throw new ArgumentException($"Content must be assignable to {typeof(T)}.", nameof(data));
        return _build(value);
    }
}
