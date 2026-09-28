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
    private bool _isWritingSource;
    private bool _isWritingTarget;

    public void Initialize(TTarget initialValue)
    {
        if (_target.TryGetTarget(out var targetNode))
        {
            if (convertBack is not null)
                targetNode.PropertyChanged += OnTargetPropertyChanged;
            SetTargetValue(targetNode, initialValue);
        }
    }

    public void Detach()
    {
        if (_isDetached)
            return;

        DetachSource();
        if (convertBack is not null && _target.TryGetTarget(out var targetNode))
            targetNode.PropertyChanged -= OnTargetPropertyChanged;
        _isDetached = true;
    }

    protected void UpdateTarget()
    {
        if (_isWritingSource)
            return;
        UpdateTargetCore();
    }

    private void UpdateTargetCore()
    {
        if (_isDetached)
            return;
        if (!_target.TryGetTarget(out var targetNode))
        {
            Detach();
            return;
        }

        if (!targetNode.IsCurrentBinding(targetProperty, this))
            return;

        SetTargetValue(targetNode, converter(ReadSource()));
    }

    private void SetTargetValue(ObservableObject targetNode, TTarget value)
    {
        var wasWritingTarget = _isWritingTarget;
        _isWritingTarget = true;
        try
        {
            targetNode.SetValueFromBinding(targetProperty, value, this);
        }
        finally
        {
            _isWritingTarget = wasWritingTarget;
        }
    }

    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (_isDetached || _isWritingSource || _isWritingTarget || !ReferenceEquals(eventArgs.Property, targetProperty))
            return;
        if (!_target.TryGetTarget(out var targetNode) || !targetNode.IsCurrentBinding(targetProperty, this))
            return;

        _isWritingSource = true;
        try
        {
            if (!convertBack!(targetNode.GetValue(targetProperty), out var sourceValue))
                return;
            if (EqualityComparer<TSource>.Default.Equals(ReadSource(), sourceValue))
                return;
            if (_isDetached || !targetNode.IsCurrentBinding(targetProperty, this))
                return;
            WriteSource(sourceValue);
            UpdateTargetCore();
        }
        finally
        {
            _isWritingSource = false;
        }
    }

    protected abstract TSource ReadSource();
    protected abstract void WriteSource(TSource value);
    protected abstract void DetachSource();
}
