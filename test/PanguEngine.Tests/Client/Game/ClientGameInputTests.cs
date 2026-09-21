using PanguEngine.Client.Game;
using PanguEngine.Client.Input;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.Game;

public sealed class ClientGameInputTests
{
    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void PressAndReleaseBeforeTickPreservesInteraction(MouseButton button)
    {
        var action = button == MouseButton.Left ? BuiltinInputActions.BreakBlock : BuiltinInputActions.PlaceBlock;
        var press = new InputActionEvent(action, BuiltinInputContexts.Game,
            InputActionPhase.Started, InputActionValue.FromButton(true), InputSource.FromMouseButton(button));
        var release = press with { Phase = InputActionPhase.Stopped, Value = InputActionValue.Zero };
        var requested = false;

        Assert.Equal(InputHandling.Handled, ClientGame.HandleInteractionRequest(ref requested, press));
        Assert.Equal(InputHandling.Pass, ClientGame.HandleInteractionRequest(ref requested, release));

        Assert.True(requested);
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void SyntheticCancellationClearsQueuedInteraction(MouseButton button)
    {
        var action = button == MouseButton.Left ? BuiltinInputActions.BreakBlock : BuiltinInputActions.PlaceBlock;
        var press = new InputActionEvent(action, BuiltinInputContexts.Game,
            InputActionPhase.Started, InputActionValue.FromButton(true), InputSource.FromMouseButton(button));
        var cancellation = press with
        {
            Phase = InputActionPhase.Stopped,
            Value = InputActionValue.Zero,
            Source = null
        };
        var requested = false;
        ClientGame.HandleInteractionRequest(ref requested, press);

        ClientGame.HandleInteractionRequest(ref requested, cancellation);

        Assert.False(requested);
    }
}
