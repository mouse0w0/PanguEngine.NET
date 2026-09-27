using System.ComponentModel;
using System.Linq.Expressions;

namespace PanguEngine.ComponentModel;

public abstract partial class ObservableObject
{
    private Dictionary<Property, IBinding>? _bindings;

    /// <summary>
    /// Creates a one-way binding from a notifying data source expression.
    /// </summary>
    /// <typeparam name="TRoot">The notifying source object type.</typeparam>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceExpression">The source value expression.</param>
    public void Bind<TRoot, TValue>(
        Property<TValue> targetProperty,
        TRoot source,
        Expression<Func<TRoot, TValue>> sourceExpression)
        where TRoot : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceExpression);
        VerifyCanBind(targetProperty);

        var expression = BindingExpression<TRoot, TValue>.ParseOneWay(sourceExpression);
        VerifyMutationAccess();
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
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceExpression">The source value expression.</param>
    /// <param name="converter">The forward value converter.</param>
    public void Bind<TRoot, TSource, TTarget>(
        Property<TTarget> targetProperty,
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
        VerifyMutationAccess();
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
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceProperty">The writable direct source property expression.</param>
    public void BindTwoWay<TRoot, TValue>(
        Property<TValue> targetProperty,
        TRoot source,
        Expression<Func<TRoot, TValue>> sourceProperty)
        where TRoot : class, INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        VerifyCanBind(targetProperty);

        var expression = BindingExpression<TRoot, TValue>.ParseTwoWay(sourceProperty);
        VerifyMutationAccess();
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
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The notifying source object.</param>
    /// <param name="sourceProperty">The writable direct source property expression.</param>
    /// <param name="converter">The forward value converter.</param>
    /// <param name="convertBack">The reverse value converter.</param>
    public void BindTwoWay<TRoot, TSource, TTarget>(
        Property<TTarget> targetProperty,
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
        VerifyMutationAccess();
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
    /// Creates a one-way binding from another registered property with the same value type.
    /// </summary>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The source property host.</param>
    /// <param name="sourceProperty">The source property.</param>
    public void Bind<TValue>(
        Property<TValue> targetProperty,
        ObservableObject source,
        Property<TValue> sourceProperty)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        sourceProperty.VerifyOwner(source);
        VerifyCanBind(targetProperty);
        VerifyMutationAccess();

        var initialValue = source.GetValue(sourceProperty);
        var binding = new PropertyBinding<TValue, TValue>(
            this,
            targetProperty,
            source,
            sourceProperty,
            Identity,
            null);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a converted one-way binding from another registered property.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TTarget">The target value type.</typeparam>
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The source property host.</param>
    /// <param name="sourceProperty">The source property.</param>
    /// <param name="converter">The forward value converter.</param>
    public void Bind<TSource, TTarget>(
        Property<TTarget> targetProperty,
        ObservableObject source,
        Property<TSource> sourceProperty,
        Func<TSource, TTarget> converter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        ArgumentNullException.ThrowIfNull(converter);
        sourceProperty.VerifyOwner(source);
        VerifyCanBind(targetProperty);
        VerifyMutationAccess();

        var initialValue = converter(source.GetValue(sourceProperty));
        var binding = new PropertyBinding<TSource, TTarget>(
            this,
            targetProperty,
            source,
            sourceProperty,
            converter,
            null);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a two-way binding to another registered property with the same value type.
    /// </summary>
    /// <typeparam name="TValue">The source and target value type.</typeparam>
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The source property host.</param>
    /// <param name="sourceProperty">The source property.</param>
    public void BindTwoWay<TValue>(
        Property<TValue> targetProperty,
        ObservableObject source,
        Property<TValue> sourceProperty)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceProperty);
        sourceProperty.VerifyOwner(source);
        sourceProperty.VerifyWritable();
        VerifyCanBind(targetProperty);
        VerifyMutationAccess();

        var initialValue = source.GetValue(sourceProperty);
        var binding = new PropertyBinding<TValue, TValue>(
            this,
            targetProperty,
            source,
            sourceProperty,
            Identity,
            TryIdentity);
        AddBinding(targetProperty, binding, initialValue);
    }

    /// <summary>
    /// Creates a converted two-way binding to another registered property.
    /// </summary>
    /// <typeparam name="TSource">The source value type.</typeparam>
    /// <typeparam name="TTarget">The target value type.</typeparam>
    /// <param name="targetProperty">The target property.</param>
    /// <param name="source">The source property host.</param>
    /// <param name="sourceProperty">The source property.</param>
    /// <param name="converter">The forward value converter.</param>
    /// <param name="convertBack">The reverse value converter.</param>
    public void BindTwoWay<TSource, TTarget>(
        Property<TTarget> targetProperty,
        ObservableObject source,
        Property<TSource> sourceProperty,
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
        VerifyMutationAccess();

        var initialValue = converter(source.GetValue(sourceProperty));
        var binding = new PropertyBinding<TSource, TTarget>(
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
    /// <param name="property">The target property.</param>
    /// <returns>Whether the property is bound.</returns>
    public bool IsBound<T>(Property<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        return _bindings?.ContainsKey(property) == true;
    }

    /// <summary>
    /// Removes a binding while preserving its last effective value as a local value.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The target property.</param>
    public void Unbind<T>(Property<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        if (!TryGetBinding(property, out var binding))
            return;

        VerifyMutationAccess();
        var currentValue = GetValueCore(property);
        binding.Detach();
        RemoveBinding(property, binding);
        _localValues ??= [];
        _localValues[property] = currentValue;
    }

    internal bool IsCurrentBinding(Property property, IBinding binding) =>
        _bindings is not null &&
        _bindings.TryGetValue(property, out var currentBinding) &&
        ReferenceEquals(currentBinding, binding);

    internal void SetValueFromBinding<T>(Property<T> property, T value, IBinding binding)
    {
        if (IsCurrentBinding(property, binding))
        {
            VerifyMutationAccess();
            SetValueCore(property, value);
        }
    }

    private void VerifyCanBind<T>(Property<T> targetProperty)
    {
        ArgumentNullException.ThrowIfNull(targetProperty);
        targetProperty.VerifyOwner(this);
        targetProperty.VerifyWritable();
        if (_bindings?.ContainsKey(targetProperty) == true)
            throw new InvalidOperationException($"Property '{targetProperty.Name}' is already bound.");
    }

    private void AddBinding<T>(Property<T> property, IBinding binding, T initialValue)
    {
        try
        {
            VerifyMutationAccess();
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

        SetValueCore(property, initialValue);
    }

    private bool TryGetBinding<T>(Property<T> property, out IBinding<T> binding)
    {
        if (_bindings is not null && _bindings.TryGetValue(property, out var untypedBinding))
        {
            binding = (IBinding<T>)untypedBinding;
            return true;
        }

        binding = null!;
        return false;
    }

    private void RemoveBinding(Property property, IBinding binding)
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
