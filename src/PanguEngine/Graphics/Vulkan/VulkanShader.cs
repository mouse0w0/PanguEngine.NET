using Silk.NET.Shaderc;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of <see cref="Shader"/>.
/// </summary>
internal sealed unsafe class VulkanShader : Shader
{
    private bool _destroyed;

    /// <summary>
    /// Creates a Vulkan shader from the given description.
    /// </summary>
    /// <param name="description">The shader description.</param>
    public VulkanShader(in ShaderDescription description)
    {
        Stage = description.Stage;
        EntryPoint = description.EntryPoint;
        Name = description.Name;
        Module = CreateShaderModule(description);
    }

    /// <inheritdoc/>
    public override ShaderStage Stage { get; }

    /// <inheritdoc/>
    public override string EntryPoint { get; }

    /// <inheritdoc/>
    public override bool IsDestroyed => _destroyed;

    /// <summary>
    /// Gets the shader name used for diagnostics.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the Vulkan shader module.
    /// </summary>
    internal ShaderModule Module { get; private set; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        if (_destroyed)
            return;

        if (Module.Handle != 0)
        {
            VulkanContext.Vk.DestroyShaderModule(VulkanContext.Device, Module, null);
            Module = default;
        }

        _destroyed = true;
    }

    private static ShaderModule CreateShaderModule(in ShaderDescription description)
    {
        var shaderc = Shaderc.GetApi();
        var compiler = shaderc.CompilerInitialize();

        try
        {
            var result = shaderc.CompileIntoSpv(compiler, description.Source, (nuint)description.Source.Length,
                ToShaderKind(description.Stage), description.Name, description.EntryPoint, null);

            try
            {
                var status = shaderc.ResultGetCompilationStatus(result);
                if (status != CompilationStatus.Success)
                {
                    var errorMessage = shaderc.ResultGetErrorMessageS(result);
                    throw new InvalidOperationException(
                        $"Shader compilation failed ({description.Name}, {description.Stage}): {errorMessage}");
                }

                var codePtr = shaderc.ResultGetBytes(result);
                var codeSize = shaderc.ResultGetLength(result);

                ShaderModuleCreateInfo createInfo = new()
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = codeSize,
                    PCode = (uint*)codePtr,
                };

                if (VulkanContext.Vk.CreateShaderModule(VulkanContext.Device, in createInfo, null, out var module) !=
                    Result.Success)
                    throw new InvalidOperationException(
                        $"Failed to create shader module ({description.Name}, {description.Stage}).");

                return module;
            }
            finally
            {
                shaderc.ResultRelease(result);
            }
        }
        finally
        {
            shaderc.CompilerRelease(compiler);
            shaderc.Dispose();
        }
    }

    private static ShaderKind ToShaderKind(ShaderStage stage)
    {
        return stage switch
        {
            ShaderStage.Vertex => ShaderKind.VertexShader,
            ShaderStage.Fragment => ShaderKind.FragmentShader,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), "Unsupported shader stage."),
        };
    }
}