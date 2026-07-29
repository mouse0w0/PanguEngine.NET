namespace PanguEngine.Client.UI;

internal sealed class UiPropertyBinding<TSource, TTarget> : UiBinding<TSource, TTarget>
{
    private readonly UiNode _source;
    private readonly UiProperty<TSource> _sourceProperty;
    private readonly IDisposable _subscription;

    internal UiPropertyBinding(
        UiNode target,
        UiProperty<TTarget> targetProperty,
        UiNode source,
        UiProperty<TSource> sourceProperty,
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

    private void OnSourcePropertyChanged(object? sender, UiPropertyChangedEventArgs<TSource> eventArgs) =>
        UpdateTarget();
}