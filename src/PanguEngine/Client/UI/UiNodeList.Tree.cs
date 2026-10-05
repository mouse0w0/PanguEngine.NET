using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI;

public sealed partial class UiNodeList
{
    private void VerifyTreeAccess() => _owner.Screen?.VerifyTreeMutationAccess();

    private void ReplaceChildren(int index, int count, IReadOnlyList<UiNode> newItems, Action updateStorage)
    {
        VerifyTreeAccess();
        var oldItems = this.Skip(index).Take(count).ToArray();
        var replacements = new HashSet<UiNode>(oldItems, ReferenceEqualityComparer.Instance);
        var additions = new HashSet<UiNode>(ReferenceEqualityComparer.Instance);
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

    private void VerifyIncomingChild(UiNode child)
    {
        child.Screen?.VerifyTreeMutationAccess();
        for (UiNode? ancestor = _owner; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child))
                throw new InvalidOperationException("Adding the UI node would create a parent cycle.");
        }
        if (child.Parent is null && child.Screen is not null)
            throw new InvalidOperationException("A UI screen root must be cleared before it can become a child.");
    }

    private void UpdateChildren(UiNode[] incoming, UiNode[] outgoing, Action updateStorage)
    {
        var changedNodes = outgoing.Concat(incoming)
            .Select(child => (Child: child, OldScreen: child.Screen)).ToArray();
        var affectedScreens = new List<UiScreen>();
        AddAffectedScreen(affectedScreens, _owner.Screen);
        foreach (var (_, oldScreen) in changedNodes)
            AddAffectedScreen(affectedScreens, oldScreen);
        var activeScreens = BeginRuntimeOperations(affectedScreens);
        try
        {
            updateStorage();

            foreach (var child in outgoing)
                child.SetParent(null);
            foreach (var child in incoming)
                child.SetParent(_owner);
            foreach (var child in outgoing)
                child.SetScreenRecursive(null);
            foreach (var child in incoming)
                child.SetScreenRecursive(_owner.Screen);
            foreach (var (child, oldScreen) in changedNodes)
            {
                if (!ReferenceEquals(oldScreen, child.Screen))
                    child.InvalidateMeasureSubtree();
            }
            _owner.InvalidateTreeStructure();
            _owner.InvalidateStyle();

            var styleEntries = new List<UiNode>();
            UiNode.AddRelationshipRefreshEntry(styleEntries, _owner, _owner.Screen?.StyleResolver ?? UiStyleResolver.Default);
            foreach (var (child, oldScreen) in changedNodes)
            {
                UiNode.AddSubtreeRefreshEntry(styleEntries, child,
                    oldScreen?.StyleResolver ?? UiStyleResolver.Default,
                    child.Screen?.StyleResolver ?? UiStyleResolver.Default,
                    screenChanged: !ReferenceEquals(oldScreen, child.Screen));
            }
            CommitAndNotifyWithStyle(activeScreens, styleEntries);
        }
        finally
        {
            EndRuntimeOperations(activeScreens);
        }
    }

    private void ReorderChildren(Action updateStorage)
    {
        var resolver = _owner.Screen?.StyleResolver ?? UiStyleResolver.Default;
        var affectedScreens = new List<UiScreen>();
        if (resolver.HasSiblingRelationships)
            AddAffectedScreen(affectedScreens, _owner.Screen);
        var activeScreens = BeginRuntimeOperations(affectedScreens);
        try
        {
            var preserveHitTestLayout = _owner.CanPreserveHitTestLayoutAfterChildOrderChange();
            updateStorage();
            _owner.InvalidateTreeStructure();
            if (preserveHitTestLayout)
                _owner.RestoreHitTestLayoutAfterChildOrderChange();
            if (resolver.HasSiblingRelationships)
                CommitAndNotifyWithStyle(activeScreens, [_owner]);
        }
        finally
        {
            EndRuntimeOperations(activeScreens);
        }
    }

    private static void AddAffectedScreen(List<UiScreen> screens, UiScreen? screen)
    {
        if (screen is not null && !screens.Contains(screen))
            screens.Add(screen);
    }

    private static List<UiScreen> BeginRuntimeOperations(List<UiScreen> screens)
    {
        var activeScreens = new List<UiScreen>(screens.Count);
        try
        {
            foreach (var screen in screens)
            {
                if (screen.BeginRuntimeOperationIfOpen())
                    activeScreens.Add(screen);
            }
        }
        catch
        {
            EndRuntimeOperations(activeScreens);
            throw;
        }
        return activeScreens;
    }

    private static void EndRuntimeOperations(List<UiScreen> screens)
    {
        for (var index = screens.Count - 1; index >= 0; index--)
            screens[index].EndRuntimeOperation();
    }

    private static void CommitAndNotifyWithStyle(
        List<UiScreen> screens,
        IReadOnlyList<UiNode> styleEntries)
    {
        var snapshots = new List<(UiScreen Screen, UiScreen.InputStateCleanupSnapshot Snapshot)>();
        foreach (var screen in screens)
        {
            if (screen.CommitInputStateAfterTreeChange() is { } snapshot)
                snapshots.Add((screen, snapshot));
        }
        UiNode.InvalidateStyleSubtreeBatch(styleEntries);
        foreach (var (screen, snapshot) in snapshots)
            screen.NotifyInputStateLoss(snapshot);
    }
}
