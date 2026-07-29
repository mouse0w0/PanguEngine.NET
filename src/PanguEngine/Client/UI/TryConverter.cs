namespace PanguEngine.Client.UI;

/// <summary>
/// Attempts to convert an input value without ending an active binding when conversion fails.
/// </summary>
/// <typeparam name="TInput">The input value type.</typeparam>
/// <typeparam name="TOutput">The output value type.</typeparam>
/// <param name="input">The value to convert.</param>
/// <param name="output">The converted value when conversion succeeds.</param>
/// <returns>Whether conversion succeeded.</returns>
public delegate bool TryConverter<in TInput, TOutput>(TInput input, out TOutput output);