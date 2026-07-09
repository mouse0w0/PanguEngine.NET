namespace PanguEngine.Graphics;

/// <summary>
/// Identifies a set of programmable shader stages.
/// </summary>
[Flags]
public enum ShaderStageFlags
{
    /// <summary>
    /// No shader stages.
    /// </summary>
    None = 0,

    /// <summary>
    /// Vertex shader stage.
    /// </summary>
    Vertex = 1,

    /// <summary>
    /// Fragment shader stage.
    /// </summary>
    Fragment = 2
}