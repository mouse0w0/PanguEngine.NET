using PanguEngine.Graphics.Vulkan;
using Silk.NET.Shaderc;
using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Test;

/// <summary>
/// Provides Vulkan shader module helpers for graphics test scenes.
/// </summary>
public static unsafe class VulkanTestShader
{
    /// <summary>
    /// Compiles a GLSL vertex shader to SPIR-V and creates a Vulkan shader module.
    /// </summary>
    /// <param name="glslSource">The GLSL source code of the vertex shader.</param>
    /// <param name="fileName">The filename used for diagnostic messages during compilation.</param>
    /// <returns>The created <see cref="ShaderModule"/>.</returns>
    public static ShaderModule CreateVertexShader(string glslSource, string fileName = "vertex.glsl")
    {
        return CreateShaderModule(ShaderKind.VertexShader, glslSource, fileName, "Vertex");
    }

    /// <summary>
    /// Compiles a GLSL fragment shader to SPIR-V and creates a Vulkan shader module.
    /// </summary>
    /// <param name="glslSource">The GLSL source code of the fragment shader.</param>
    /// <param name="fileName">The filename used for diagnostic messages during compilation.</param>
    /// <returns>The created <see cref="ShaderModule"/>.</returns>
    public static ShaderModule CreateFragmentShader(string glslSource, string fileName = "fragment.glsl")
    {
        return CreateShaderModule(ShaderKind.FragmentShader, glslSource, fileName, "Fragment");
    }

    /// <summary>
    /// Destroys a Vulkan shader module and releases its resources.
    /// </summary>
    /// <param name="module">The shader module to destroy.</param>
    public static void DestroyShaderModule(ShaderModule module)
    {
        VulkanContext.Vk.DestroyShaderModule(VulkanContext.Device, module, null);
    }

    private static ShaderModule CreateShaderModule(
        ShaderKind kind,
        string glslSource,
        string fileName,
        string stageName)
    {
        var shaderc = Shaderc.GetApi();
        var compiler = shaderc.CompilerInitialize();

        try
        {
            var result = shaderc.CompileIntoSpv(compiler, glslSource, (nuint)glslSource.Length,
                kind, fileName, "main", null);

            try
            {
                var status = shaderc.ResultGetCompilationStatus(result);
                if (status != CompilationStatus.Success)
                {
                    var errorMessage = shaderc.ResultGetErrorMessageS(result);
                    throw new InvalidOperationException(
                        $"Shader compilation failed ({fileName}, {stageName}): {errorMessage}");
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
                    throw new InvalidOperationException($"Failed to create shader module ({fileName}, {stageName}).");

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
}