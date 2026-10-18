namespace PanguEngine.UI.Controls;

/// <summary>Describes a completed user adjustment to a slider.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="startValue">The value before the user adjustment.</param>
/// <param name="endValue">The value after the user adjustment.</param>
public sealed class SliderValueCommittedEventArgs<T>(T startValue, T endValue) : EventArgs
{
    /// <summary>Gets the value before the user adjustment.</summary>
    public T StartValue { get; } = startValue;

    /// <summary>Gets the value after the user adjustment.</summary>
    public T EndValue { get; } = endValue;
}
