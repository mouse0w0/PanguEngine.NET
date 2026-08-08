using System.ComponentModel;
using System.Linq.Expressions;

namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private Dictionary<UiProperty, IUiBinding>? _bindings;

    /// <summary>
    /// Creates a one-way binding from a notifying data source expression.
    /// </summary>
    /// <typeparam name="TRoot">The notifying source object type.</typeparam>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceExpression">The source value expression.</param>
    public void Bind<TRoot, TValue>(
        UiProperty<TValue> targetProperty,
        TRoot source,
        Expression<Func<TRoot, TValue>> sourceExpression)
        where TRoot : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceExpression);
        VerifyCanBind(targetProperty);

        var expression = BindingExpression<TRoot, TValue>.ParseOneWay(sourceExpression);
        VerifyPropertyAccess(targetProperty);
        var initialValue = expression.Getter(source);
        var binding = new NotifyPropertyChangedBinding<TRoot, TValue, TValue>(
            this,
            targetProperty,
            source,
            expression.Getter,
            null,
            expression.PropertyName,
            Identity,
            null);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a converted one-way binding from a notifying data source expression.
    /// </summary>
    /// <typeparam name="TRoot">The notifying source object type.</typeparam>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TTarget">The target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceExpression">The source value expression.</param>
    /// <param name="converter">The forward value converter.</param>
    public void Bind<TRoot, TSource, TTarget>(
        UiProperty<TTarget> targetProperty,
        TRoot source,
        Expression<Func<TRoot, TSource>> sourceExpression,
        Func<TSource, TTarget> converter)
        where TRoot : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceExpression);
        ArgumentNullException.ThrowIfNull(converter);
        VerifyCanBind(targetProperty);

        var expression = BindingExpression<TRoot, TSource>.ParseOneWay(sourceExpression);
        VerifyPropertyAccess(targetProperty);
        var initialValue = converter(expression.Getter(source));
        var binding = new NotifyPropertyChangedBinding<TRoot, TSource, TTarget>(
            this,
            targetProperty,
            source,
            expression.Getter,
            null,
            expression.PropertyName,
            converter,
            null);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a two-way binding to a writable direct property on a notifying data source.
    /// </summary>
    /// <typeparam name="TRoot">The notifying source object type.</typeparam>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceProperty">The writable direct source property expression.</param>
    public void BindTwoWay<TRoot, TValue>(
        UiProperty<TValue> targetProperty,
        TRoot source,
        Expression<Func<TRoot, TValue>> sourceProperty)
        where TRoot : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        VerifyCanBind(targetProperty);

        var expression = BindingExpression<TRoot, TValue>.ParseTwoWay(sourceProperty);
        VerifyPropertyAccess(targetProperty);
        var initialValue = expression.Getter(source);
        var binding = new NotifyPropertyChangedBinding<TRoot, TValue, TValue>(
            this,
            targetProperty,
            source,
            expression.Getter,
            expression.Setter,
            expression.PropertyName,
            Identity,
            TryIdentity);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a converted two-way binding to a writable direct property on a notifying data source.
    /// </summary>
    /// <typeparam name="TRoot">The notifying source object type.</typeparam>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TTarget">The target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceProperty">The writable direct source property expression.</param>
    /// <param name="converter">The forward value converter.</param>
    /// <param name="convertBack">The reverse value converter.</param>
    public void BindTwoWay<TRoot, TSource, TTarget>(
        UiProperty<TTarget> targetProperty,
        TRoot source,
        Expression<Func<TRoot, TSource>> sourceProperty,
        Func<TSource, TTarget> converter,
        TryConverter<TTarget, TSource> convertBack)
        where TRoot : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(convertBack);
        VerifyCanBind(targetProperty);

        var expression = BindingExpression<TRoot, TSource>.ParseTwoWay(sourceProperty);
        VerifyPropertyAccess(targetProperty);
        var initialValue = converter(expression.Getter(source));
        var binding = new NotifyPropertyChangedBinding<TRoot, TSource, TTarget>(
            this,
            targetProperty,
            source,
            expression.Getter,
            expression.Setter,
            expression.PropertyName,
            converter,
            convertBack);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a one-way binding from another UI property with the same value type.
    /// </summary>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The source UI node.</param>
    /// <param name="sourceProperty">The source UI property.</param>
    public void Bind<TValue>(
        UiProperty<TValue> targetProperty,
        UiNode source,
        UiProperty<TValue> sourceProperty)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        sourceProperty.VerifyOwner(source);
        VerifyCanBind(targetProperty);
        VerifyPropertyAccess(targetProperty);

        var initialValue = source.GetValue(sourceProperty);
        var binding = new UiPropertyBinding<TValue, TValue>(
            this,
            targetProperty,
            source,
            sourceProperty,
            Identity,
            null);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a converted one-way binding from another UI property.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TTarget">The target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The source UI node.</param>
    /// <param name="sourceProperty">The source UI property.</param>
    /// <param name="converter">The forward value converter.</param>
    public void Bind<TSource, TTarget>(
        UiProperty<TTarget> targetProperty,
        UiNode source,
        UiProperty<TSource> sourceProperty,
        Func<TSource, TTarget> converter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(converter);
        sourceProperty.VerifyOwner(source);
        VerifyCanBind(targetProperty);
        VerifyPropertyAccess(targetProperty);

        var initialValue = converter(source.GetValue(sourceProperty));
        var binding = new UiPropertyBinding<TSource, TTarget>(
            this,
            targetProperty,
            source,
            sourceProperty,
            converter,
            null);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a two-way binding to another UI property with the same value type.
    /// </summary>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The source UI node.</param>
    /// <param name="sourceProperty">The source UI property.</param>
    public void BindTwoWay<TValue>(
        UiProperty<TValue> targetProperty,
        UiNode source,
        UiProperty<TValue> sourceProperty)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        sourceProperty.VerifyOwner(source);
        sourceProperty.VerifyWritable();
        VerifyCanBind(targetProperty);
        VerifyPropertyAccess(targetProperty);

        var initialValue = source.GetValue(sourceProperty);
        var binding = new UiPropertyBinding<TValue, TValue>(
            this,
            targetProperty,
            source,
            sourceProperty,
            Identity,
            TryIdentity);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a converted two-way binding to another UI property.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TTarget">The target value type.</typeparam>
    /// <param name="targetProperty">The target UI property.</param>
    /// <param name="source">The source UI node.</param>
    /// <param name="sourceProperty">The source UI property.</param>
    /// <param name="converter">The forward value converter.</param>
    /// <param name="convertBack">The reverse value converter.</param>
    public void BindTwoWay<TSource, TTarget>(
        UiProperty<TTarget> targetProperty,
        UiNode source,
        UiProperty<TSource> sourceProperty,
        Func<TSource, TTarget> converter,
        TryConverter<TTarget, TSource> convertBack)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(convertBack);
        sourceProperty.VerifyOwner(source);
        sourceProperty.VerifyWritable();
        VerifyCanBind(targetProperty);
        VerifyPropertyAccess(targetProperty);

        var initialValue = converter(source.GetValue(sourceProperty));
        var binding = new UiPropertyBinding<TSource, TTarget>(
            this,
            targetProperty,
            source,
            sourceProperty,
            converter,
            convertBack);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Gets whether a target property currently has a binding.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The target UI property.</param>
    /// <returns>Whether the property is bound.</returns>
    public bool IsBound<T>(UiProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        return _bindings?.ContainsKey(property) == true;
    }

    /// <summary>
    /// Removes a binding while preserving its last effective value as a local value.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The target UI property.</param>
    public void Unbind<T>(UiProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        if (!TryGetBinding(property, out var binding))
            return;

        VerifyPropertyAccess(property);
        var currentValue = GetValueCore(property);
        binding.Detach();
        RemoveBinding(property, binding);
        _localValues ??= [];
        _localValues[property] = currentValue;
    }

    internal bool IsCurrentBinding(UiProperty property, IUiBinding binding) =>
        _bindings is not null &&
        _bindings.TryGetValue(property, out var currentBinding) &&
        ReferenceEquals(currentBinding, binding);

    internal void SetValueFromBinding<T>(UiProperty<T> property, T value, IUiBinding binding)
    {
        if (IsCurrentBinding(property, binding))
            SetValueCore(property, value);
    }

    private void VerifyCanBind<T>(UiProperty<T> targetProperty)
    {
        ArgumentNullException.ThrowIfNull(targetProperty);
        targetProperty.VerifyOwner(this);
        targetProperty.VerifyWritable();
        if (_bindings?.ContainsKey(targetProperty) == true)
            throw new InvalidOperationException($"Property '{targetProperty.Name}' is already bound.");
    }

    private void AddBinding<T>(UiProperty<T> property, IUiBinding binding, T initialValue)
    {
        try
        {
            VerifyPropertyAccess(property);
        }
        catch
        {
            binding.Detach();
            throw;
        }

        _bindings ??= [];
        if (!_bindings.TryAdd(property, binding))
        {
            binding.Detach();
            throw new InvalidOperationException($"Property '{property.Name}' is already bound.");
        }

        SetValueFromBinding(property, initialValue, binding);
    }

    private bool TryGetBinding<T>(UiProperty<T> property, out IUiBinding<T> binding)
    {
        if (_bindings is not null && _bindings.TryGetValue(property, out var untypedBinding))
        {
            binding = (IUiBinding<T>)untypedBinding;
            return true;
        }

        binding = null!;
        return false;
    }

    private void RemoveBinding(UiProperty property, IUiBinding binding)
    {
        if (!IsCurrentBinding(property, binding))
            return;

        _bindings!.Remove(property);
        if (_bindings.Count == 0)
            _bindings = null;
    }

    private static T Identity<T>(T value) =>
        value;

    private static bool TryIdentity<T>(T input, out T output)
    {
        output = input;
        return true;
    }
}
