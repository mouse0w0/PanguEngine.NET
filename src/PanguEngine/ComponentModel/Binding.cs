namespace PanguEngine.ComponentModel;

internal interface IBinding
{
    void Detach();
}

internal interface IBinding<in TTarget> : IBinding
{
    bool IsTwoWay { get; }
    void UpdateSource(TTarget value);
}

internal abstract class Binding<TSource, TTarget>(
    ObservableObject target,
    Property<TTarget> targetProperty,
    Func<TSource, TTarget> converter,
    TryConverter<TTarget, TSource>? convertBack) : IBinding<TTarget>
{
    private readonly WeakReference<ObservableObject> _target = new(target);
    private bool _isDetached;
    private bool _isWritingSource;

    public bool IsTwoWay => convertBack is not null;

    public void UpdateSource(TTarget value)
    {
        if (_isDetached || _isWritingSource)
            return;
        if (convertBack is null)
            throw new InvalidOperationException($"Property '{targetProperty.Name}' has a one-way binding.");
        if (!convertBack(value, out var sourceValue))
            return;

        _isWritingSource = true;
        try
        {
            WriteSource(sourceValue);
        }
        finally
        {
            _isWritingSource = false;
        }

        UpdateTarget();
    }

    public void Detach()
    {
        if (_isDetached)
            return;

        DetachSource();
        _isDetached = true;
    }

    protected void UpdateTarget()
    {
        if (_isDetached || _isWritingSource)
            return;
        if (!_target.TryGetTarget(out var targetNode))
        {
            Detach();
            return;
        }

        if (!targetNode.IsCurrentBinding(targetProperty, this))
            return;

        var targetValue = converter(ReadSource());
        targetNode.SetValueFromBinding(targetProperty, targetValue, this);
    }

    protected abstract TSource ReadSource();
    protected abstract void WriteSource(TSource value);
    protected abstract void DetachSource();
}
