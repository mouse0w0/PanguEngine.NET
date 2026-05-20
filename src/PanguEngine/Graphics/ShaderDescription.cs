namespace PanguEngine.Graphics;

/// <summary>
/// Describes a shader resource to create.
/// </summary>
/// <param name="Stage">The shader stage.</param>
/// <param name="Source">The complete shader source code.</param>
/// <param name="EntryPoint">The shader entry point name.</param>
/// <param name="Name">The shader name used for diagnostics.</param>
/// <param name="SourceLanguage">The shader source language.</param>
public readonly record struct ShaderDescription(
    ShaderStage Stage,
    string Source,
    string EntryPoint = "main",
    string Name = "shader",
    ShaderSourceLanguage SourceLanguage = ShaderSourceLanguage.Glsl);