using PanguEngine.UI.Styling;

namespace PanguEngine.UI;

public sealed partial class UIElementList
{
    private void VerifyTreeAccess() => _owner.Scene?.VerifyTreeMutationAccess();

    private void ReplaceChildren(int index, int count, IReadOnlyList<UIElement> newItems, Action updateStorage)
    {
        VerifyTreeAccess();
        var oldItems = this.Skip(index).Take(count).ToArray();
        var replacements = new HashSet<UIElement>(oldItems, ReferenceEqualityComparer.Instance);
        var additions = new HashSet<UIElement>(ReferenceEqualityComparer.Instance);
        foreach (var child in newItems)
        {
            ArgumentNullException.ThrowIfNull(child);
            if (!additions.Add(child) || (ReferenceEquals(child.Parent, _owner) && !replacements.Contains(child)))
                throw new InvalidOperationException("The UI node is already a child of this parent.");
        }

        var incoming = newItems.Where(child => !ReferenceEquals(child.Parent, _owner)).ToArray();
        var outgoing = oldItems.Where(child => !additions.Contains(child)).ToArray();
        foreach (var child in incoming)
        {
            VerifyIncomingChild(child);
        }

        var hasSource = incoming.Any(child => child.Parent is not null);
        if (hasSource)
        {
            foreach (var child in incoming)
                child.Parent?.Children.Remove(child);
            foreach (var child in incoming)
            {
                if (child.Parent is not null)
                    throw new InvalidOperationException("The UI node was reparented during removal notification.");
                VerifyIncomingChild(child);
            }

            VerifyTreeAccess();
        }

        UpdateChildren(incoming, outgoing, updateStorage);
    }

    private void VerifyIncomingChild(UIElement child)
    {
        child.Scene?.VerifyTreeMutationAccess();
        for (UIElement? ancestor = _owner; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child))
                throw new InvalidOperationException("Adding the UI node would create a parent cycle.");
        }

        if (child.Parent is null && child.Scene is not null)
            throw new InvalidOperationException("A UI scene root must be cleared before it can become a child.");
    }

    private void UpdateChildren(UIElement[] incoming, UIElement[] outgoing, Action updateStorage)
    {
        var changedNodes = outgoing.Concat(incoming)
            .Select(child => (Child: child, OldScene: child.Scene)).ToArray();
        var affectedScenes = new List<UIScene>();
        AddAffectedScene(affectedScenes, _owner.Scene);
        foreach (var (_, oldScene) in changedNodes)
            AddAffectedScene(affectedScenes, oldScene);
        var activeScenes = BeginRuntimeOperations(affectedScenes);
        try
        {
            updateStorage();

            foreach (var child in outgoing)
                child.SetParent(null);
            foreach (var child in incoming)
                child.SetParent(_owner);
            foreach (var child in outgoing)
                child.SetSceneRecursive(null);
            foreach (var child in incoming)
                child.SetSceneRecursive(_owner.Scene);
            foreach (var (child, oldScene) in changedNodes)
            {
                if (!ReferenceEquals(oldScene, child.Scene))
                    child.InvalidateMeasureSubtree();
            }

            _owner.InvalidateTreeStructure();
            _owner.InvalidateStyle();

            var styleEntries = new List<UIElement>();
            UIElement.AddRelationshipRefreshEntry(styleEntries, _owner,
                _owner.Scene?.StyleResolver ?? StyleResolver.Default);
            foreach (var (child, oldScene) in changedNodes)
            {
                UIElement.AddSubtreeRefreshEntry(styleEntries, child,
                    oldScene?.StyleResolver ?? StyleResolver.Default,
                    child.Scene?.StyleResolver ?? StyleResolver.Default,
                    sceneChanged: !ReferenceEquals(oldScene, child.Scene));
            }

            CommitAndNotifyWithStyle(activeScenes, styleEntries);
        }
        finally
        {
            EndRuntimeOperations(activeScenes);
        }
    }

    private void ReorderChildren(Action updateStorage)
    {
        var resolver = _owner.Scene?.StyleResolver ?? StyleResolver.Default;
        var refreshStyles = resolver.HasSiblingRelationships || resolver.HasChildIndexedPseudoClasses;
        var affectedScenes = new List<UIScene>();
        if (refreshStyles)
            AddAffectedScene(affectedScenes, _owner.Scene);
        var activeScenes = BeginRuntimeOperations(affectedScenes);
        try
        {
            var preserveHitTestLayout = _owner.CanPreserveHitTestLayoutAfterChildOrderChange();
            updateStorage();
            _owner.InvalidateTreeStructure();
            if (preserveHitTestLayout)
                _owner.RestoreHitTestLayoutAfterChildOrderChange();
            if (refreshStyles)
                CommitAndNotifyWithStyle(activeScenes, [_owner]);
        }
        finally
        {
            EndRuntimeOperations(activeScenes);
        }
    }

    private static void AddAffectedScene(List<UIScene> scenes, UIScene? scene)
    {
        if (scene is not null && !scenes.Contains(scene))
            scenes.Add(scene);
    }

    private static List<UIScene> BeginRuntimeOperations(List<UIScene> scenes)
    {
        var activeScenes = new List<UIScene>(scenes.Count);
        try
        {
            foreach (var scene in scenes)
            {
                if (scene.BeginRuntimeOperationIfOpen())
                    activeScenes.Add(scene);
            }
        }
        catch
        {
            EndRuntimeOperations(activeScenes);
            throw;
        }

        return activeScenes;
    }

    private static void EndRuntimeOperations(List<UIScene> scenes)
    {
        for (var index = scenes.Count - 1; index >= 0; index--)
            scenes[index].EndRuntimeOperation();
    }

    private static void CommitAndNotifyWithStyle(
        List<UIScene> scenes,
        IReadOnlyList<UIElement> styleEntries)
    {
        var snapshots = new List<(UIScene Scene, UIScene.InputStateCleanupSnapshot Snapshot)>();
        foreach (var scene in scenes)
        {
            if (scene.CommitInputStateAfterTreeChange() is { } snapshot)
                snapshots.Add((scene, snapshot));
        }

        UIElement.InvalidateStyleSubtreeBatch(styleEntries);
        foreach (var (scene, snapshot) in snapshots)
            scene.NotifyInputStateLoss(snapshot);
    }
}