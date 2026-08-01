using Silk.NET.Vulkan;

namespace PanguEngine.Graphics.Vulkan;

internal sealed class VulkanUploadPacket
{
    internal VulkanUploadPacket(CommandBuffer commandBuffer)
    {
        CommandBuffer = commandBuffer;
    }

    internal CommandBuffer CommandBuffer { get; }

    internal bool IsSubmitted { get; private set; }

    internal ulong SubmissionValue { get; private set; }

    internal VulkanStagingLease? Lease { get; private set; }

    internal VulkanUploadHandle[] Handles { get; private set; } = [];

    internal void MarkSubmitted(
        ulong submissionValue,
        VulkanStagingLease lease,
        VulkanUploadHandle[] handles)
    {
        SubmissionValue = submissionValue;
        Lease = lease;
        Handles = handles;
        IsSubmitted = true;
    }

    internal void Reset()
    {
        IsSubmitted = false;
        SubmissionValue = 0;
        Lease = null;
        Handles = [];
    }
}

internal sealed class VulkanUploadPacketRing
{
    internal const int Capacity = 3;

    private readonly VulkanUploadPacket[] _packets;
    private int _nextIndex;
    private int _oldestIndex;

    internal VulkanUploadPacketRing(IReadOnlyList<CommandBuffer> commandBuffers)
    {
        if (commandBuffers.Count != Capacity)
            throw new ArgumentException($"A Vulkan upload packet ring requires {Capacity} command buffers.",
                nameof(commandBuffers));

        _packets = new VulkanUploadPacket[Capacity];
        for (var index = 0; index < Capacity; index++)
            _packets[index] = new VulkanUploadPacket(commandBuffers[index]);
    }

    internal int SubmittedCount { get; private set; }

    internal VulkanUploadPacket NextPacket => _packets[_nextIndex];

    internal VulkanUploadPacket OldestSubmittedPacket => SubmittedCount > 0
        ? _packets[_oldestIndex]
        : throw new InvalidOperationException("The Vulkan upload packet ring has no submitted packets.");

    internal void EnsureNextAvailable()
    {
        if (NextPacket.IsSubmitted)
            throw new InvalidOperationException("The next Vulkan upload packet is still submitted.");
    }

    internal void CommitNextSubmission(
        ulong submissionValue,
        VulkanStagingLease lease,
        VulkanUploadHandle[] handles)
    {
        if (SubmittedCount == 0)
            _oldestIndex = _nextIndex;

        NextPacket.MarkSubmitted(submissionValue, lease, handles);
        _nextIndex = (_nextIndex + 1) % Capacity;
        SubmittedCount++;
    }

    internal void RetireCompleted(ulong timelineValue, VulkanStagingPagePool stagingPages)
    {
        while (SubmittedCount > 0)
        {
            var packet = _packets[_oldestIndex];
            if (packet.SubmissionValue > timelineValue)
                return;

            CompleteHandles(packet);
            stagingPages.Recycle(packet.Lease!);
            ResetOldestPacket(packet);
        }
    }

    internal void FaultSubmittedHandles(Exception exception)
    {
        for (var offset = 0; offset < SubmittedCount; offset++)
        {
            var packet = _packets[(_oldestIndex + offset) % Capacity];
            foreach (var handle in packet.Handles)
            {
                if (!handle.IsCompleted)
                    handle.SignalFailure(exception);
            }
        }
    }

    internal void ReclaimAll(
        VulkanStagingPagePool stagingPages,
        bool completeSuccessfully,
        Exception? exception)
    {
        while (SubmittedCount > 0)
        {
            var packet = _packets[_oldestIndex];
            foreach (var handle in packet.Handles)
            {
                if (handle.IsCompleted)
                    continue;
                if (completeSuccessfully)
                    handle.SignalSuccess();
                else
                    handle.SignalFailure(exception!);
            }

            stagingPages.Recycle(packet.Lease!);
            ResetOldestPacket(packet);
        }
    }

    private static void CompleteHandles(VulkanUploadPacket packet)
    {
        foreach (var handle in packet.Handles)
        {
            if (!handle.IsCompleted)
                handle.SignalSuccess();
        }
    }

    private void ResetOldestPacket(VulkanUploadPacket packet)
    {
        packet.Reset();
        _oldestIndex = (_oldestIndex + 1) % Capacity;
        SubmittedCount--;
    }
}
