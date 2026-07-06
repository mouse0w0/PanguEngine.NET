namespace PanguEngine.Graphics;

/// <summary>
/// Specifies comparison operations used by depth and stencil tests.
/// </summary>
public enum CompareOperation
{
    /// <summary>
    /// The comparison never passes.
    /// </summary>
    Never,

    /// <summary>
    /// The comparison passes when the source value is less than the destination value.
    /// </summary>
    Less,

    /// <summary>
    /// The comparison passes when the source value equals the destination value.
    /// </summary>
    Equal,

    /// <summary>
    /// The comparison passes when the source value is less than or equal to the destination value.
    /// </summary>
    LessOrEqual,

    /// <summary>
    /// The comparison passes when the source value is greater than the destination value.
    /// </summary>
    Greater,

    /// <summary>
    /// The comparison passes when the source value does not equal the destination value.
    /// </summary>
    NotEqual,

    /// <summary>
    /// The comparison passes when the source value is greater than or equal to the destination value.
    /// </summary>
    GreaterOrEqual,

    /// <summary>
    /// The comparison always passes.
    /// </summary>
    Always
}