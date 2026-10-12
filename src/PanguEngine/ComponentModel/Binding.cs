namespace PanguEngine.ComponentModel;

internal interface IBinding
{
    void Detach();
}

internal interface IBinding<in TTarget> : IBinding
{
    void Initialize(TTarget initialValue);
}

internal abstract class Binding<TSource, TTarget>(
    ObservableObject target,
    Property<TTarget> targetProperty,
    Func<TSource, TTarget> converter,
    TryConverter<TTarget, TSource>? convertBack) : IBinding<TTarget>
{
    private readonly WeakReference<ObservableObject> _target = new(target);
    private bool _isDetached;

    public void Initialize(TTarget initialValue)
    {
        if (_target.TryGetTarget(out var target))
        {
            if (convertBack is not null)
                target.PropertyChanged += OnTargetPropertyChanged;
            target.SetValueFromBinding(targetProperty, initialValue, this);
        }
    }

    public void Detach()
    {
        if (_isDetached)
            return;

        DetachSource();
        if (convertBack is not null && _target.TryGetTarget(out var target))
            target.PropertyChanged -= OnTargetPropertyChanged;
        _isDetached = true;
    }

    protected void UpdateTarget()
    {
        if (_isDetached)
            return;
        if (!_target.TryGetTarget(out var target))
        {
            Detach();
            return;
        }

        if (!target.IsCurrentBinding(targetProperty, this))
            return;

        target.SetValueFromBinding(targetProperty, converter(ReadSource()), this);
    }

    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (_isDetached || !ReferenceEquals(eventArgs.Property, targetProperty))
            return;
        if (!_target.TryGetTarget(out var target) || !target.IsCurrentBinding(targetProperty, this))
            return;

        if (!convertBack!(target.GetValue(targetProperty), out var sourceValue))
            return;
        if (_isDetached || !target.IsCurrentBinding(targetProperty, this))
            return;
        WriteSource(sourceValue);
    }

    protected abstract TSource ReadSource();
    protected abstract void WriteSource(TSource value);
    protected abstract void DetachSource();
}
