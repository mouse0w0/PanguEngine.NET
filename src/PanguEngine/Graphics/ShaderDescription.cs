namespace PanguEngine.Graphics;

/// <summary>
/// Describes a compiled shader resource to create.
/// </summary>
/// <param name="Stage">The shader stage.</param>
/// <param name="Bytecode">The compiled SPIR-V bytecode.</param>
/// <param name="EntryPoint">The shader entry point name.</param>
/// <param name="Name">The shader name used for diagnostics.</param>
public readonly record struct ShaderDescription(
    ShaderStage Stage,
    ReadOnlyMemory<byte> Bytecode,
    string EntryPoint = "main",
    string Name = "shader");