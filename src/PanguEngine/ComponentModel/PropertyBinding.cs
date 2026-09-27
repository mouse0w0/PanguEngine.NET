namespace PanguEngine.ComponentModel;

internal sealed class PropertyBinding<TSource, TTarget> : Binding<TSource, TTarget>
{
    private readonly ObservableObject _source;
    private readonly Property<TSource> _sourceProperty;
    private readonly IDisposable _subscription;

    internal PropertyBinding(
        ObservableObject target,
        Property<TTarget> targetProperty,
        ObservableObject source,
        Property<TSource> sourceProperty,
        Func<TSource, TTarget> converter,
        TryConverter<TTarget, TSource>? convertBack)
        : base(target, targetProperty, converter, convertBack)
    {
        _source = source;
        _sourceProperty = sourceProperty;
        _subscription = source.Subscribe(sourceProperty, OnSourcePropertyChanged);
    }

    protected override TSource ReadSource() =>
        _source.GetValue(_sourceProperty);

    protected override void WriteSource(TSource value) =>
        _source.SetValue(_sourceProperty, value);

    protected override void DetachSource() =>
        _subscription.Dispose();

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs<TSource> eventArgs) =>
        UpdateTarget();
}
