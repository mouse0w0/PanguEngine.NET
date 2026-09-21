using System.Globalization;
using PanguEngine.Client.UI.Controls;
using PanguEngine.World.Chunking;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides the built-in client debug information HUD component.
/// </summary>
public sealed class DebugInfoHud : Hud
{
    private static readonly CultureInfo FormatCulture = CultureInfo.InvariantCulture;

    private readonly Text[] _lines;

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
        _lines = [new Text(), new Text(), new Text(), new Text(), new Text(), new Text(), new Text()];
        foreach (var line in _lines)
            ((StackPanel)Root).Children.Add(line);
    }

    /// <summary>
    /// Toggles the visibility of the debug information.
    /// </summary>
    public void Toggle() => Root.Visibility = Root.Visibility == Visibility.Visible
        ? Visibility.Collapsed
        : Visibility.Visible;

    /// <inheritdoc />
    protected override void OnFrameUpdate(double alpha)
    {
        if (Root.Visibility != Visibility.Visible)
            return;

        var engine = ClientEngine.Current;
        var game = engine.Game;
        var camera = game.Camera;
        var position = camera.CurrentPosition;
        var block = new BlockPos(
            (int)Math.Floor(position.X),
            (int)Math.Floor(position.Y),
            (int)Math.Floor(position.Z));
        var chunk = block.ToChunkPos();
        var statistics = engine.Renderer.FrameStatistics;
        SetText(0, string.Create(FormatCulture,
            $"FPS: {statistics.FramesPerSecond} | avg: {statistics.AverageFrameTimeMilliseconds:F1} ms | 1% low: {statistics.OnePercentLowFrameTimeMilliseconds:F1} ms"));
        SetText(1, string.Create(FormatCulture,
            $"Position: {position.X:F3}, {position.Y:F3}, {position.Z:F3}"));
        SetText(2, string.Create(FormatCulture,
            $"Block: {block.X}, {block.Y}, {block.Z}"));
        SetText(3, string.Create(FormatCulture,
            $"Facing: yaw: {camera.Yaw:F1}, pitch: {camera.Pitch:F1}"));
        var target = game.SelectedBlock is { } hit
            ? string.Create(FormatCulture,
                $"{hit.BlockState} @ {hit.BlockPosition.X}, {hit.BlockPosition.Y}, {hit.BlockPosition.Z}")
            : "none";
        SetText(4, $"Target: {target}");
        SetText(5, string.Create(FormatCulture,
            $"Chunk: {chunk.X}, {chunk.Y}, {chunk.Z}"));
        SetText(6, string.Create(FormatCulture,
            $"Loaded chunks: {game.World.Chunks.Count}"));
    }

    private void SetText(int index, string value)
    {
        if (_lines[index].Content != value)
            _lines[index].Content = value;
    }
}
