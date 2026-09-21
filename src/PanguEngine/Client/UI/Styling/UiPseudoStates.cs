namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Describes composable framework pseudo states that a style rule can require.
/// </summary>
/// <remarks>
/// The flags represent independent conditions rather than a mutually exclusive state machine.
/// A rule matches only when every required bit is active. Built-in bits are fixed for the first version.
/// </remarks>
[Flags]
public enum UiPseudoStates
{
    /// <summary>No pseudo state is required.</summary>
    None = 0,

    /// <summary>The node is hovered by the pointer.</summary>
    Hovered = 1,

    /// <summary>The node is focused.</summary>
    Focused = 2,

    /// <summary>The node is pressed; interactive controls report this in addition to base states.</summary>
    Pressed = 4,

    /// <summary>The node is disabled.</summary>
    Disabled = 8
}
