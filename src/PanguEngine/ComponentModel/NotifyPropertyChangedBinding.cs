using System.ComponentModel;
using SourcePropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;

namespace PanguEngine.ComponentModel;

internal sealed class NotifyPropertyChangedBinding<TRoot, TSource, TTarget> : Binding<TSource, TTarget>
    where TRoot : class, INotifyPropertyChanged
{
    private readonly TRoot _source;
    private readonly Func<TRoot, TSource> _getter;
    private readonly Action<TRoot, TSource>? _setter;
    private readonly string? _propertyName;

    internal NotifyPropertyChangedBinding(
        ObservableObject target,
        Property<TTarget> targetProperty,
        TRoot source,
        Func<TRoot, TSource> getter,
        Action<TRoot, TSource>? setter,
        string? propertyName,
        Func<TSource, TTarget> converter,
        TryConverter<TTarget, TSource>? convertBack)
        : base(target, targetProperty, converter, convertBack)
    {
        _source = source;
        _getter = getter;
        _setter = setter;
        _propertyName = propertyName;
        _source.PropertyChanged += OnSourcePropertyChanged;
    }

    protected override TSource ReadSource() =>
        _getter(_source);

    protected override void WriteSource(TSource value) =>
        _setter!(_source, value);

    protected override void DetachSource() =>
        _source.PropertyChanged -= OnSourcePropertyChanged;

    private void OnSourcePropertyChanged(object? sender, SourcePropertyChangedEventArgs eventArgs)
    {
        if (_propertyName is not null &&
            !string.IsNullOrEmpty(eventArgs.PropertyName) &&
            !string.Equals(_propertyName, eventArgs.PropertyName, StringComparison.Ordinal))
        {
            return;
        }

        UpdateTarget();
    }
}
