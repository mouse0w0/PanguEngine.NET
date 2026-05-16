namespace PanguEngine.Graphics;

/// <summary>
/// Describes the intended memory usage pattern for a resource.
/// </summary>
public readonly record struct MemoryUsage
{
    /// <summary>
    /// GPU-only memory with optimal device access.
    /// </summary>
    public static readonly MemoryUsage GpuOnly = new(0);

    /// <summary>
    /// CPU writes to memory, GPU reads from it.
    /// </summary>
    public static readonly MemoryUsage CpuToGpu = new(1);

    /// <summary>
    /// GPU writes to memory, CPU reads from it.
    /// </summary>
    public static readonly MemoryUsage GpuToCpu = new(2);

    /// <summary>
    /// VMA automatic memory type selection.
    /// </summary>
    public static Vma.MemoryUsage Auto => Vma.MemoryUsage.Auto;

    internal int Value { get; }

    private MemoryUsage(int value)
    {
        Value = value;
    }
}