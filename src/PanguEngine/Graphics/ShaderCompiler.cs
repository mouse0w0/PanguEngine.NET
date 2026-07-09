using Silk.NET.Shaderc;

namespace PanguEngine.Graphics;

/// <summary>
/// Compiles GLSL shader source to SPIR-V bytecode.
/// </summary>
public static unsafe class ShaderCompiler
{
    /// <summary>
    /// Compiles GLSL source code to SPIR-V bytecode.
    /// </summary>
    /// <param name="stage">The shader stage.</param>
    /// <param name="source">The GLSL source code.</param>
    /// <param name="entryPoint">The entry point name.</param>
    /// <param name="name">The shader name used for diagnostics.</param>
    /// <returns>The compiled SPIR-V bytecode.</returns>
    public static byte[] CompileGlsl(
        ShaderStage stage,
        string source,
        string entryPoint = "main",
        string name = "shader")
    {
        var shaderc = Shaderc.GetApi();
        var compiler = shaderc.CompilerInitialize();

        try
        {
            var kind = ToShaderKind(stage);
            var result = shaderc.CompileIntoSpv(compiler, source, (nuint)source.Length,
                kind, name, entryPoint, null);

            try
            {
                var status = shaderc.ResultGetCompilationStatus(result);
                if (status != CompilationStatus.Success)
                {
                    var errorMessage = shaderc.ResultGetErrorMessageS(result);
                    throw new InvalidOperationException(
                        $"Shader compilation failed ({name}, {stage}): {errorMessage}");
                }

                var codePtr = shaderc.ResultGetBytes(result);
                var codeSize = shaderc.ResultGetLength(result);
                var byteCount = (int)codeSize;
                var bytecode = new byte[byteCount];
                fixed (byte* dst = bytecode)
                {
                    System.Buffer.MemoryCopy(codePtr, dst, byteCount, byteCount);
                }

                return bytecode;
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
            ShaderStage.None => throw new ArgumentException("Shader stage must not be None.", nameof(stage)),
            ShaderStage.Vertex => ShaderKind.VertexShader,
            ShaderStage.Fragment => ShaderKind.FragmentShader,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage,
                "Shader stage must identify a supported single shader stage.")
        };
    }
}