using System.Diagnostics.CodeAnalysis;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Registries;

namespace PanguEngine.Client.UI.Huds;

/// <summary>
/// Provides the persistent, non-interactive client HUD and hosts registered HUD components.
/// </summary>
public sealed class HudScreen
{
    private readonly Panel _root;
    private readonly Dictionary<ResourceKey, Hud> _hudsByKey = [];
    private readonly List<Hud> _hudsInOrder = [];

    internal HudScreen()
    {
        _root = new Panel();
        Screen = new HudUiScreen(this, _root);
    }

    /// <summary>
    /// Posts a HUD tree operation to the client UI owner thread.
    /// </summary>
    /// <param name="action">The operation to execute during the next HUD frame preparation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the HUD is not accepting posted actions.
    /// </exception>
    public void Post(Action action) => Screen.Post(action);

    /// <summary>
    /// Gets a running HUD component by its registry key.
    /// </summary>
    /// <param name="key">The key of the HUD component.</param>
    /// <returns>The running HUD component.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the HUD screen is closed or access is not on its owner thread.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Thrown when no component with the key is running.</exception>
    public Hud Get(ResourceKey key)
    {
        Screen.VerifyOwnerThread();
        return _hudsByKey[key];
    }

    /// <summary>
    /// Attempts to get a running HUD component by its registry key.
    /// </summary>
    /// <param name="key">The key of the HUD component.</param>
    /// <param name="hud">The running HUD component when found.</param>
    /// <returns>Whether a component with the key is running.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the HUD screen is closed or access is not on its owner thread.
    /// </exception>
    public bool TryGet(ResourceKey key, [NotNullWhen(true)] out Hud? hud)
    {
        Screen.VerifyOwnerThread();
        return _hudsByKey.TryGetValue(key, out hud);
    }

    internal UiScreen Screen { get; }

    internal void Open() => Screen.Open();

    internal void Initialize(IRegistry<HudDefinition> definitions)
    {
        foreach (var entry in definitions.Entries)
            Accept(entry.Key, entry.Value.Create());
    }

    internal void Update() => Screen.Update();

    internal void PrepareFrame(Size viewportSize, double alpha) => Screen.PrepareFrame(viewportSize, alpha);

    internal void Close()
    {
        Screen.StopAcceptingPosts();
        for (var index = _hudsInOrder.Count - 1; index >= 0; index--)
            _hudsInOrder[index].DestroyHud();

        Screen.Close();
        _hudsByKey.Clear();
        _hudsInOrder.Clear();
    }

    private void Accept(ResourceKey key, Hud hud)
    {
        if (hud is null)
            throw new InvalidOperationException($"HUD factory for '{key}' returned null.");
        if (hud.Root.Parent is not null || hud.Root.Screen is not null)
            throw new InvalidOperationException($"HUD factory for '{key}' returned an already mounted root node.");

        _hudsByKey.Add(key, hud);
        _hudsInOrder.Add(hud);
        _root.Children.Add(hud.Root);
    }

    private void DispatchFixedUpdate()
    {
        foreach (var hud in _hudsInOrder)
            hud.UpdateFixed();
    }

    private void DispatchFrameUpdate(double alpha)
    {
        foreach (var hud in _hudsInOrder)
            hud.UpdateFrame(alpha);
    }

    private sealed class HudUiScreen(HudScreen owner, Panel root) : UiScreen(root)
    {
        protected override void OnFixedUpdate() => owner.DispatchFixedUpdate();

        protected override void OnFrameUpdate(double alpha) => owner.DispatchFrameUpdate(alpha);
    }
}
