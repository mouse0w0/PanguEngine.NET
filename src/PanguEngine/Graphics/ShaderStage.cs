namespace PanguEngine.Graphics;

/// <summary>
/// Identifies the programmable stage of a shader.
/// </summary>
[Flags]
public enum ShaderStage
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