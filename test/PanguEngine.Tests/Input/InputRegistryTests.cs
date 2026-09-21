using PanguEngine.Input;
using PanguEngine.Registries;

namespace PanguEngine.Tests.Input;

public sealed class InputRegistryTests
{
    [Fact]
    public void BuiltinRegistriesExposeInputDefinitionRegistries()
    {
        Assert.Equal("pangu:input_action", RegistryKeys.InputAction.ToString());
        Assert.Equal("pangu:input_context", RegistryKeys.InputContext.ToString());
        Assert.IsType<Registry<InputAction>>(BuiltinRegistries.InputAction);
        Assert.IsType<Registry<InputContext>>(BuiltinRegistries.InputContext);
    }

    [Fact]
    public void ActionsAndContextsUseRegistryObjectIdentity()
    {
        var firstAction = new InputAction(InputValueType.Button);
        var secondAction = new InputAction(InputValueType.Button);
        var firstContext = new InputContext(InputScope.Game);
        var secondContext = new InputContext(InputScope.Game);

        Assert.NotEqual(firstAction, secondAction);
        Assert.NotEqual(firstContext, secondContext);
    }

    [Fact]
    public void ActionCopiesBindings()
    {
        var context = new InputContext(InputScope.Game);
        var bindings = new List<InputBinding>
        {
            InputBinding.Button("default", context, Key.E)
        };

        var action = new InputAction(InputValueType.Button, bindings);
        bindings.Clear();

        Assert.Single(action.Bindings);
    }

    [Fact]
    public void BuiltinDefinitionsRegisterExpectedKeysAndPolicies()
    {
        var actions = new Registry<InputAction>(RegistryKeys.InputAction);
        var contexts = new Registry<InputContext>(RegistryKeys.InputContext);

        BuiltinInputActions.Register(actions);
        BuiltinInputContexts.Register(contexts);

        Assert.Same(BuiltinInputActions.Move, actions.Get(ResourceKey.Parse("pangu:move")));
        Assert.Same(BuiltinInputActions.Look, actions.Get(ResourceKey.Parse("pangu:look")));
        Assert.Equal(InputValueType.Axis2D, BuiltinInputActions.Move.ValueType);
        Assert.Equal(InputValueType.Axis2D, BuiltinInputActions.Look.ValueType);
        Assert.Equal(InputScope.Game, BuiltinInputContexts.Game.Scope);
        Assert.Equal(InputCaptureMask.None, BuiltinInputContexts.Game.CaptureMask);
        Assert.Equal(PointerCapturePolicy.Preserve, BuiltinInputContexts.Game.PointerCapturePolicy);
        Assert.Equal(InputScope.Ui, BuiltinInputContexts.Ui.Scope);
        Assert.Equal(InputCaptureMask.All, BuiltinInputContexts.Ui.CaptureMask);
        Assert.Equal(PointerCapturePolicy.Suspend, BuiltinInputContexts.Ui.PointerCapturePolicy);
        Assert.Equal(["forward", "backward", "left", "right"],
            BuiltinInputActions.Move.Bindings.Select(binding => binding.Name));
        Assert.True(
            BuiltinInputActions.CapturePointer.Bindings[0].Priority >
            BuiltinInputActions.BreakBlock.Bindings[0].Priority);
        Assert.Same(BuiltinInputContexts.Game, BuiltinInputActions.TogglePause.Bindings[0].Context);
        Assert.Same(BuiltinInputContexts.Ui, BuiltinInputActions.TogglePause.Bindings[1].Context);
    }
}
