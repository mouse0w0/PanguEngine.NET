using System.Diagnostics.CodeAnalysis;

namespace PanguEngine.Graphics;

/// <summary>
/// Describes a compiled shader resource to create.
/// </summary>
public readonly record struct ShaderDescription
{
    public ShaderDescription()
    {
    }

    /// <summary>
    /// Creates a shader description.
    /// </summary>
    /// <param name="stage">The shader stage.</param>
    /// <param name="bytecode">The compiled SPIR-V bytecode.</param>
    /// <param name="name">The shader name used for diagnostics.</param>
    /// <param name="entryPoint">The shader entry point name.</param>
    [SetsRequiredMembers]
    public ShaderDescription(
        ShaderStage stage,
        byte[] bytecode,
        string name = "shader",
        string entryPoint = "main")
    {
        Stage = stage;
        Bytecode = bytecode;
        Name = name;
        EntryPoint = entryPoint;
    }

    /// <summary>
    /// The shader stage.
    /// </summary>
    public required ShaderStage Stage { get; init; }

    /// <summary>
    /// The compiled SPIR-V bytecode.
    /// </summary>
    public required byte[] Bytecode { get; init; }

    /// <summary>
    /// The shader name used for diagnostics.
    /// </summary>
    public string Name { get; init; } = "shader";

    /// <summary>
    /// The shader entry point name.
    /// </summary>
    public string EntryPoint { get; init; } = "main";
}