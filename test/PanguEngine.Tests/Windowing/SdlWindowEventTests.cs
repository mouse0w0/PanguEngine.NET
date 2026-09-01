using PanguEngine.Graphics.Vulkan;
using PanguEngine.Windowing;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Windowing;

public sealed class SdlWindowEventTests
{
    [Fact]
    public void RelativeMouseMotionAccumulatesVirtualPosition()
    {
        var state = new SdlWindowEventState();
        state.EnterRelativeMode(new Vector2D<float>(10, 20));

        var position = state.ApplyMouseMotion(999, 999, 2.5f, -4f);

        Assert.Equal(new Vector2D<float>(12.5f, 16f), position);
        Assert.Equal(position, state.MousePosition);
    }

    [Fact]
    public void DropEventsAreAggregatedUntilCompletion()
    {
        var state = new SdlWindowEventState();
        state.BeginDrop();
        state.AddDropFile("first.txt");
        state.AddDropFile("second.txt");

        Assert.Equal(["first.txt", "second.txt"], state.CompleteDrop());
        Assert.Empty(state.CompleteDrop());
    }

    [Fact]
    public void StartingANewDropDiscardsThePreviousBatch()
    {
        var state = new SdlWindowEventState();
        state.BeginDrop();
        state.AddDropFile("stale.txt");
        state.BeginDrop();
        state.AddDropFile("current.txt");

        Assert.Equal(["current.txt"], state.CompleteDrop());
    }

    [Fact]
    public void DisplayMonitorDoesNotExposeGamma()
    {
        Assert.Null(typeof(DisplayMonitor).GetProperty("Gamma"));
    }
}
