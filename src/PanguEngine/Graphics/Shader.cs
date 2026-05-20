namespace PanguEngine.Graphics;

/// <summary>
/// Represents a GPU shader resource.
/// </summary>
public abstract class Shader : GraphicsResource
{
    /// <summary>
    /// Gets the shader stage.
    /// </summary>
    public abstract ShaderStage Stage { get; }

    /// <summary>
    /// Gets the shader entry point name.
    /// </summary>
    public abstract string EntryPoint { get; }
}