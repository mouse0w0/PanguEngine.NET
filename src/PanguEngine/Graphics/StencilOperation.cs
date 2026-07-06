namespace PanguEngine.Graphics;

/// <summary>
/// Specifies operations that update stencil values.
/// </summary>
public enum StencilOperation
{
    /// <summary>
    /// Keeps the current stencil value.
    /// </summary>
    Keep,

    /// <summary>
    /// Sets the stencil value to zero.
    /// </summary>
    Zero,

    /// <summary>
    /// Replaces the stencil value with the reference value.
    /// </summary>
    Replace,

    /// <summary>
    /// Increments the stencil value and clamps it to the maximum value.
    /// </summary>
    IncrementAndClamp,

    /// <summary>
    /// Decrements the stencil value and clamps it to zero.
    /// </summary>
    DecrementAndClamp,

    /// <summary>
    /// Bitwise inverts the stencil value.
    /// </summary>
    Invert,

    /// <summary>
    /// Increments the stencil value and wraps on overflow.
    /// </summary>
    IncrementAndWrap,

    /// <summary>
    /// Decrements the stencil value and wraps on underflow.
    /// </summary>
    DecrementAndWrap
}