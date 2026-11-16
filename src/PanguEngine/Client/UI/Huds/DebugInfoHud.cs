using System.Diagnostics;
using System.Globalization;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.UI.Huds;

/// <summary>
/// Provides the built-in client debug information HUD component.
/// </summary>
public sealed class DebugInfoHud : Hud
{
    private const double BytesPerMiB = 1024 * 1024;

    private static readonly CultureInfo FormatCulture = CultureInfo.InvariantCulture;

    private readonly Text[] _lines;
    private Process? _process;
    private long? _sampleTimestamp;
    private TimeSpan _sampleProcessorTime;
    private long _sampleAllocatedBytes;

    /// <summary>
    /// Initializes a hidden debug information component.
    /// </summary>
    public DebugInfoHud()
        : base(new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8),
            Spacing = 2,
            Visibility = Visibility.Collapsed
        })
    {
        _lines =
        [
            new Text(), new Text(), new Text(), new Text(), new Text(),
            new Text(), new Text(), new Text(), new Text(), new Text()
        ];
        foreach (var line in _lines)
            ((StackPanel)Root).Children.Add(line);
    }

    /// <summary>
    /// Toggles the visibility of the debug information.
    /// </summary>
    public void Toggle()
    {
        Root.Visibility = Root.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        _sampleTimestamp = null;
    }

    /// <inheritdoc />
    protected override void OnFrameUpdate(double alpha)
    {
        if (Root.Visibility != Visibility.Visible)
            return;

        var engine = ClientEngine.Current;
        var game = engine.Game;
        var camera = game.Camera;
        var position = game.World.Player.Position.Current;
        var block = new BlockPos(
            (int)Math.Floor(position.X),
            (int)Math.Floor(position.Y),
            (int)Math.Floor(position.Z));
        var chunk = block.ToChunkPos();
        var statistics = engine.Renderer.FrameStatistics;
        SetText(0, string.Create(FormatCulture,
            $"FPS: {statistics.FramesPerSecond} | avg: {statistics.AverageFrameTimeMilliseconds:F1} ms | 1% low: {statistics.OnePercentLowFrameTimeMilliseconds:F1} ms"));
        UpdateProcessStatistics();
        SetText(4, string.Create(FormatCulture,
            $"Position: {position.X:F3}, {position.Y:F3}, {position.Z:F3}"));
        SetText(5, string.Create(FormatCulture,
            $"Block: {block.X}, {block.Y}, {block.Z}"));
        SetText(6, string.Create(FormatCulture,
            $"Facing: yaw: {camera.Yaw:F1}, pitch: {camera.Pitch:F1}"));
        var target = game.SelectedBlock is { } hit
            ? string.Create(FormatCulture,
                $"{hit.BlockState} @ {hit.BlockPosition.X}, {hit.BlockPosition.Y}, {hit.BlockPosition.Z}")
            : "none";
        SetText(7, $"Target: {target}");
        SetText(8, string.Create(FormatCulture,
            $"Chunk: {chunk.X}, {chunk.Y}, {chunk.Z}"));
        SetText(9, string.Create(FormatCulture,
            $"Loaded chunks: {game.World.Chunks.Count}"));
    }

    /// <inheritdoc />
    protected override void OnDestroy() => _process?.Dispose();

    private void UpdateProcessStatistics()
    {
        var timestamp = Stopwatch.GetTimestamp();
        var elapsedSeconds = _sampleTimestamp is { } previousTimestamp
            ? Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalSeconds
            : 0;
        if (_sampleTimestamp.HasValue && elapsedSeconds < 1)
            return;

        _process ??= Process.GetCurrentProcess();
        _process.Refresh();
        var processorTime = _process.TotalProcessorTime;
        var allocatedBytes = GC.GetTotalAllocatedBytes(false);
        var allocationRate = "--";
        if (_sampleTimestamp.HasValue)
        {
            var cpuPercent = (processorTime - _sampleProcessorTime).TotalSeconds
                / elapsedSeconds / Environment.ProcessorCount * 100;
            var allocatedMiBPerSecond = (allocatedBytes - _sampleAllocatedBytes) / elapsedSeconds / BytesPerMiB;
            SetText(1, string.Create(FormatCulture, $"CPU: {cpuPercent:F1}%"));
            allocationRate = string.Create(FormatCulture, $"{allocatedMiBPerSecond:F1} MiB/s");
        }
        else
        {
            SetText(1, "CPU: --");
        }

        SetText(2, string.Create(FormatCulture,
            $"Memory: managed {GC.GetTotalMemory(false) / BytesPerMiB:F1} MiB | working set {_process.WorkingSet64 / BytesPerMiB:F1} MiB"));
        SetText(3, string.Create(FormatCulture,
            $"GC: Gen0 {GC.CollectionCount(0)} | Gen1 {GC.CollectionCount(1)} | Gen2 {GC.CollectionCount(2)} | alloc {allocationRate}"));

        _sampleTimestamp = timestamp;
        _sampleProcessorTime = processorTime;
        _sampleAllocatedBytes = allocatedBytes;
    }

    private void SetText(int index, string value)
    {
        if (_lines[index].Content != value)
            _lines[index].Content = value;
    }
}
