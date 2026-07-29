using System.Linq.Expressions;
using System.Reflection;

namespace PanguEngine.Client.UI;

internal sealed class BindingExpression<TRoot, TValue>(
    Func<TRoot, TValue> getter,
    Action<TRoot, TValue>? setter,
    string? propertyName)
{
    internal Func<TRoot, TValue> Getter { get; } = getter;
    internal Action<TRoot, TValue>? Setter { get; } = setter;
    internal string? PropertyName { get; } = propertyName;

    internal static BindingExpression<TRoot, TValue> ParseOneWay(
        Expression<Func<TRoot, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var directBody = expression.Body;
        if (directBody is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            directBody = unary.Operand;

        var property = GetDirectPublicProperty(directBody, expression.Parameters[0]);
        return new BindingExpression<TRoot, TValue>(
            expression.Compile(),
            null,
            property?.Name);
    }

    internal static BindingExpression<TRoot, TValue> ParseTwoWay(
        Expression<Func<TRoot, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var property = GetDirectPublicProperty(expression.Body, expression.Parameters[0]);
        if (property is not { SetMethod: { IsPublic: true, IsStatic: false } })
            throw new ArgumentException(
                "A two-way binding source must be a directly accessed public instance property with a public setter.",
                nameof(expression));

        var memberExpression = (MemberExpression)expression.Body;
        var valueParameter = Expression.Parameter(typeof(TValue), "value");
        var assignment = Expression.Assign(memberExpression, valueParameter);
        var setter = Expression.Lambda<Action<TRoot, TValue>>(
            assignment,
            expression.Parameters[0],
            valueParameter).Compile();

        return new BindingExpression<TRoot, TValue>(
            expression.Compile(),
            setter,
            property.Name);
    }

    private static PropertyInfo? GetDirectPublicProperty(
        Expression body,
        ParameterExpression rootParameter)
    {
        if (body is not MemberExpression { Member: PropertyInfo property } memberExpression ||
            !ReferenceEquals(memberExpression.Expression, rootParameter) ||
            property.GetIndexParameters().Length != 0 ||
            property.GetMethod is not { IsPublic: true, IsStatic: false })
        {
            return null;
        }

        return property;
    }
}