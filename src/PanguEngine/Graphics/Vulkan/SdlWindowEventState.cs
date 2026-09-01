using Silk.NET.Maths;

namespace PanguEngine.Graphics.Vulkan;

internal sealed class SdlWindowEventState
{
    private readonly List<string> _dropPaths = [];
    private bool _relativeMode;

    internal Vector2D<float> MousePosition { get; private set; }

    internal void EnterRelativeMode(Vector2D<float> position)
    {
        MousePosition = position;
        _relativeMode = true;
    }

    internal void ExitRelativeMode(Vector2D<float> position)
    {
        MousePosition = position;
        _relativeMode = false;
    }

    internal Vector2D<float> ApplyMouseMotion(float x, float y, float xrel, float yrel)
    {
        MousePosition = _relativeMode
            ? new Vector2D<float>(MousePosition.X + xrel, MousePosition.Y + yrel)
            : new Vector2D<float>(x, y);
        return MousePosition;
    }

    internal void BeginDrop() => _dropPaths.Clear();

    internal void AddDropFile(string path) => _dropPaths.Add(path);

    internal string[] CompleteDrop()
    {
        var paths = _dropPaths.ToArray();
        _dropPaths.Clear();
        return paths;
    }
}
