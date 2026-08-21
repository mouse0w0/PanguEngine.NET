namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanDescriptorSetBindingState
{
    private readonly Dictionary<uint, DescriptorSetLayoutBinding> _layoutByBinding = [];
    private readonly Dictionary<(uint Binding, uint ArrayElement), int> _indexByKey = new();
    private DescriptorSetBinding[] _bindings;
    private DescriptorSetBinding[]? _pendingCommit;

    internal VulkanDescriptorSetBindingState(
        IReadOnlyList<DescriptorSetLayoutBinding> layoutBindings,
        ReadOnlySpan<DescriptorSetBinding> bindings)
    {
        foreach (var layoutBinding in layoutBindings)
        {
            if (layoutBinding.DescriptorCount == 0)
                throw new ArgumentOutOfRangeException(
                    nameof(layoutBindings),
                    "Descriptor set layout binding count must be greater than zero.");
            if (!_layoutByBinding.TryAdd(layoutBinding.Binding, layoutBinding))
                throw new ArgumentException(
                    "Descriptor set layout bindings must not contain duplicate binding indices.",
                    nameof(layoutBindings));

            for (uint element = 0; element < layoutBinding.DescriptorCount; element++)
                _indexByKey.Add((layoutBinding.Binding, element), _indexByKey.Count);
        }

        if (bindings.Length != _indexByKey.Count)
            throw new ArgumentException(
                "Descriptor bindings must contain exactly one element per layout (binding, array element).",
                nameof(bindings));

        _bindings = new DescriptorSetBinding[_indexByKey.Count];
        var assigned = new bool[_bindings.Length];
        foreach (var binding in bindings)
        {
            if (!_layoutByBinding.TryGetValue(binding.Binding, out var layoutBinding))
                throw new ArgumentException(
                    $"Descriptor binding {binding.Binding} does not exist in the layout.",
                    nameof(bindings));
            if (binding.ArrayElement >= layoutBinding.DescriptorCount)
                throw new ArgumentOutOfRangeException(
                    nameof(bindings),
                    $"Descriptor array element {binding.ArrayElement} is out of range for binding {binding.Binding}.");
            if (binding.Type != layoutBinding.Type)
                throw new ArgumentException(
                    $"Descriptor binding type {binding.Type} does not match the layout type {layoutBinding.Type}.",
                    nameof(bindings));

            var index = _indexByKey[(binding.Binding, binding.ArrayElement)];
            if (assigned[index])
                throw new ArgumentException(
                    "Descriptor bindings must not contain duplicate (binding, array element) pairs.",
                    nameof(bindings));
            assigned[index] = true;
            _bindings[index] = binding;
        }
    }

    internal IReadOnlyList<DescriptorSetBinding> Bindings => _bindings;

    internal DescriptorSetBinding[] CreateUpdatedBindings(ReadOnlySpan<DescriptorSetBinding> updates)
    {
        if (updates.Length == 0)
            throw new ArgumentException("Sparse descriptor update must contain at least one binding.",
                nameof(updates));

        var candidate = (DescriptorSetBinding[])_bindings.Clone();
        var updatedKeys = new HashSet<(uint, uint)>();
        foreach (var update in updates)
        {
            if (!_layoutByBinding.TryGetValue(update.Binding, out var layoutBinding))
                throw new ArgumentException(
                    $"Descriptor binding {update.Binding} does not exist in the layout.", nameof(updates));
            if (update.ArrayElement >= layoutBinding.DescriptorCount)
                throw new ArgumentOutOfRangeException(nameof(updates),
                    $"Descriptor array element {update.ArrayElement} is out of range for binding {update.Binding}.");
            if (!_indexByKey.TryGetValue((update.Binding, update.ArrayElement), out var index))
                throw new InvalidOperationException(
                    "Descriptor update targets an unknown (binding, array element) pair.");
            if (layoutBinding.Type != update.Type)
                throw new ArgumentException(
                    $"Descriptor update type {update.Type} does not match the layout type {layoutBinding.Type}.",
                    nameof(updates));
            if (!updatedKeys.Add((update.Binding, update.ArrayElement)))
                throw new ArgumentException(
                    "Sparse descriptor update contains duplicate (binding, array element) keys.", nameof(updates));

            candidate[index] = update;
        }

        _pendingCommit = candidate;
        return candidate;
    }

    internal void Commit(DescriptorSetBinding[] bindings)
    {
        if (!ReferenceEquals(bindings, _pendingCommit))
            throw new ArgumentException(
                "Descriptor binding candidate was not produced by this binding state.", nameof(bindings));

        _bindings = bindings;
        _pendingCommit = null;
    }
}
