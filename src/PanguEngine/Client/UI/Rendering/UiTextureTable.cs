using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI.Rendering;

internal readonly record struct UiTextureSlot(uint Index, ulong Generation);

internal sealed class UiTextureTable : IGlyphTextureSlotRegistry
{
    internal const uint SlotCount = 256;

    private readonly Texture _fallbackTexture;
    private readonly TextureView _fallbackView;
    private readonly Sampler _linearSampler;
    private readonly Sampler _nearestSampler;
    private readonly FrameState[] _frames;
    private readonly SlotState[] _slots;
    private readonly SortedSet<uint> _freeSlots = [];
    private readonly List<DescriptorSetBinding> _pendingUpdates = [];
    private readonly List<int> _changedSlots = [];
    private bool _descriptorSetsDestroyed;
    private bool _ownedResourcesDestroyed;

    internal UiTextureTable(
        GraphicsDevice graphicsDevice,
        DescriptorSetLayout descriptorSetLayout,
        uint frameSlotCount)
    {
        _fallbackTexture = graphicsDevice.CreateTexture(new TextureDescription
        {
            Dimension = TextureDimension.Type2D,
            Format = TextureFormat.R8G8B8A8Srgb,
            Width = 1,
            Height = 1,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Usage = TextureUsage.Sampled | TextureUsage.TransferDestination
        });
        _fallbackView = graphicsDevice.CreateTextureView(
            _fallbackTexture,
            new TextureViewDescription(TextureViewDimension.Type2D, 0, 1, 0, 1));
        _ = graphicsDevice.UploadTexture(_fallbackTexture, [255, 255, 255, 255]);
        _linearSampler = graphicsDevice.CreateSampler(CreateSamplerDescription(FilterMode.Linear));
        _nearestSampler = graphicsDevice.CreateSampler(CreateSamplerDescription(FilterMode.Nearest));

        _frames = new FrameState[checked((int)frameSlotCount)];
        for (var frameIndex = 0; frameIndex < _frames.Length; frameIndex++)
        {
            _frames[frameIndex] = new FrameState(graphicsDevice.CreateDescriptorSet(new DescriptorSetDescription(
                descriptorSetLayout,
                CreateInitialBindings(_fallbackView, _linearSampler, _nearestSampler))));
        }

        _slots = new SlotState[checked((int)SlotCount)];
        for (var index = 0; index < _slots.Length; index++)
            _slots[index] = new SlotState(_fallbackView, _frames.Length);
        for (uint index = 0; index < SlotCount; index++)
            _freeSlots.Add(index);
    }

    internal bool HasFreeSlot => _freeSlots.Count != 0;
    internal int FrameSlotCount => _frames.Length;

    bool IGlyphTextureSlotRegistry.HasFreeSlot => HasFreeSlot;

    bool IGlyphTextureSlotRegistry.TryRegister(TextureView view, out uint textureIndex)
    {
        if (!TryRegister(view, out var slot))
        {
            textureIndex = 0;
            return false;
        }

        textureIndex = slot.Index;
        return true;
    }

    internal bool TryRegister(TextureView view, out UiTextureSlot slot)
    {
        if (_freeSlots.Count == 0)
        {
            slot = default;
            return false;
        }

        var index = _freeSlots.Min;
        _freeSlots.Remove(index);
        var state = _slots[index];
        state.Status = SlotStatus.Active;
        state.RegisteredView = view;
        state.Generation++;
        slot = new UiTextureSlot(index, state.Generation);
        return true;
    }

    internal void Publish(UiTextureSlot slot)
    {
        var state = _slots[slot.Index];
        if (state.Status != SlotStatus.Active || state.Generation != slot.Generation)
            throw new InvalidOperationException("UI texture slot is not the active slot generation being published.");
        Publish(state);
    }

    internal void Publish(uint textureIndex)
    {
        var state = _slots[textureIndex];
        if (state.Status != SlotStatus.Active)
            throw new InvalidOperationException("UI texture slot is not active.");
        Publish(state);
    }

    internal void Retire(UiTextureSlot slot, Action release)
    {
        var state = _slots[slot.Index];
        if (state.Status != SlotStatus.Active || state.Generation != slot.Generation)
            throw new InvalidOperationException("UI texture slot is not the active slot generation being retired.");

        state.Status = SlotStatus.Retiring;
        state.RegisteredView = _fallbackView;
        state.Generation++;
        if (!ReferenceEquals(state.View, _fallbackView))
        {
            state.View = _fallbackView;
            state.DescriptorGeneration++;
        }

        Array.Fill(state.PendingFrames, true);
        state.Release = release;
    }

    internal void SynchronizeFrame(uint frameSlot)
    {
        var frameIndex = checked((int)frameSlot);
        var frame = _frames[frameIndex];
        _pendingUpdates.Clear();
        _changedSlots.Clear();
        for (var slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
        {
            var slot = _slots[slotIndex];
            if (frame.AppliedDescriptorGenerations[slotIndex] == slot.DescriptorGeneration)
                continue;

            _pendingUpdates.Add(DescriptorSetBinding.SampledImage(0, (uint)slotIndex, slot.View));
            _changedSlots.Add(slotIndex);
        }

        if (_pendingUpdates.Count != 0)
        {
            frame.DescriptorSet.Update([.. _pendingUpdates]);
            foreach (var slotIndex in _changedSlots)
                frame.AppliedDescriptorGenerations[slotIndex] = _slots[slotIndex].DescriptorGeneration;
        }

        for (var slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
        {
            var slot = _slots[slotIndex];
            if (slot.Status == SlotStatus.Retiring)
                slot.PendingFrames[frameIndex] = false;
        }

        CompleteRetirements();
    }

    internal DescriptorSet GetDescriptorSet(uint frameSlot) =>
        _frames[checked((int)frameSlot)].DescriptorSet;

    internal void DestroyDescriptorSets()
    {
        if (_descriptorSetsDestroyed)
            return;
        _descriptorSetsDestroyed = true;

        for (var index = _frames.Length - 1; index >= 0; index--)
            _frames[index].DescriptorSet.Destroy();
    }

    internal void DestroyOwnedResources()
    {
        if (_ownedResourcesDestroyed)
            return;
        _ownedResourcesDestroyed = true;

        foreach (var slot in _slots)
        {
            if (slot.Status != SlotStatus.Retiring || slot.Release is null)
                continue;
            slot.Release();
            slot.Release = null;
        }

        _nearestSampler.Destroy();
        _linearSampler.Destroy();
        _fallbackView.Destroy();
        _fallbackTexture.Destroy();
        _freeSlots.Clear();
    }

    private void CompleteRetirements()
    {
        for (uint slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            var slot = _slots[slotIndex];
            if (slot.Status != SlotStatus.Retiring || slot.PendingFrames.Contains(true))
                continue;

            var release = slot.Release!;
            slot.Release = null;
            release();
            slot.Status = SlotStatus.Free;
            _freeSlots.Add(slotIndex);
        }
    }

    private static void Publish(SlotState state)
    {
        if (ReferenceEquals(state.View, state.RegisteredView))
            return;
        state.View = state.RegisteredView;
        state.DescriptorGeneration++;
    }

    private static DescriptorSetBinding[] CreateInitialBindings(
        TextureView fallbackView,
        Sampler linearSampler,
        Sampler nearestSampler)
    {
        var bindings = new DescriptorSetBinding[checked((int)SlotCount + 2)];
        for (uint index = 0; index < SlotCount; index++)
            bindings[index] = DescriptorSetBinding.SampledImage(0, index, fallbackView);
        bindings[SlotCount] = DescriptorSetBinding.SamplerDescriptor(1, linearSampler);
        bindings[SlotCount + 1] = DescriptorSetBinding.SamplerDescriptor(2, nearestSampler);
        return bindings;
    }

    private static SamplerDescription CreateSamplerDescription(FilterMode filter) =>
        new(
            filter,
            filter,
            MipmapMode.Nearest,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            WrapMode.ClampToEdge,
            1,
            0,
            0,
            0);

    private enum SlotStatus
    {
        Free,
        Active,
        Retiring
    }

    private sealed class SlotState(TextureView view, int frameSlotCount)
    {
        internal TextureView View { get; set; } = view;
        internal TextureView RegisteredView { get; set; } = view;
        internal ulong Generation { get; set; }
        internal ulong DescriptorGeneration { get; set; }
        internal SlotStatus Status { get; set; }
        internal bool[] PendingFrames { get; } = new bool[frameSlotCount];
        internal Action? Release { get; set; }
    }

    private sealed class FrameState(DescriptorSet descriptorSet)
    {
        internal DescriptorSet DescriptorSet { get; } = descriptorSet;
        internal ulong[] AppliedDescriptorGenerations { get; } = new ulong[checked((int)SlotCount)];
    }
}