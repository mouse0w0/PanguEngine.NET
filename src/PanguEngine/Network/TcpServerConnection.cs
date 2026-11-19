using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using PanguEngine.Modding;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Server;
using PanguEngine.Versioning;

namespace PanguEngine.Network;

/// <summary>Prepares a server-side TCP connection and exchanges typed packets.</summary>
internal sealed class TcpServerConnection(TcpConnection connection) : IServerConnection
{
    private readonly Lock _gate = new();
    private ConnectionPhase _phase = ConnectionPhase.Handshake;
    private bool _closing;

    /// <summary>Prepares the endpoint for Play and reports any username reserved by the connection.</summary>
    /// <remarks>The game thread may receive packets only after this task completes successfully with a ready result.</remarks>
    internal async Task<(string? ReservedUsername, bool IsReady)> PrepareAsync(
        ContentManifestPacket manifest, IReadOnlyList<ModInfo> mods, PlayerNames playerNames)
    {
        string? username = null;
        try
        {
            var hello = await ReadExpectedAsync<ClientHelloPacket>().ConfigureAwait(false);
            if (hello.ProtocolVersion != PacketCodec.ProtocolVersion)
                throw new InvalidDataException("Unsupported network protocol version.");
            if (!SendTransition(new LoginStartPacket(), ConnectionPhase.Login))
                return (username, false);

            var login = await ReadExpectedAsync<LoginRequestPacket>().ConfigureAwait(false);
            playerNames.Reserve(login.Username);
            username = login.Username;
            if (!SendTransition(new LoginSuccessPacket(), ConnectionPhase.Configuration))
                return (username, false);

            var clientMods = await ReadExpectedAsync<ClientModsPacket>().ConfigureAwait(false);
            var rejection = CheckMods(mods, clientMods.Mods);
            if (rejection is not null)
                throw new InvalidDataException(rejection);
            try
            {
                if (!TrySend(manifest))
                    return (username, false);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("Server content manifest exceeds the 1 MiB message limit.");
            }
            await ReadExpectedAsync<ContentReadyPacket>().ConfigureAwait(false);
            RemainingPreparationTime();
            lock (_gate)
            {
                if (_closing)
                    return (username, false);
                _phase = ConnectionPhase.Play;
                connection.EnableHeartbeat();
            }
            return (username, true);
        }
        catch (Exception error) when (error is InvalidDataException or EndOfStreamException or DecoderFallbackException or ConnectionClosedException)
        {
            Disconnect(error.Message);
            return (username, false);
        }
    }

    private async Task<T> ReadExpectedAsync<T>() where T : ServerboundPacket
    {
        await connection.WaitForFrameAsync(RemainingPreparationTime()).ConfigureAwait(false);
        return TryReceive(out var packet) && packet is T expected
            ? expected
            : throw new InvalidDataException("Message is not valid in the current connection phase.");
    }

    private TimeSpan RemainingPreparationTime()
    {
        var remaining = NetworkLimits.HandshakeTimeout - Stopwatch.GetElapsedTime(connection.ConnectedAt);
        return remaining > TimeSpan.Zero ? remaining : throw new ConnectionClosedException("Connection setup timed out.");
    }

    public bool IsClosed => connection.IsClosed;
    public int IncomingCount => connection.IncomingCount;

    public bool TryReceive([NotNullWhen(true)] out ServerboundPacket? packet)
    {
        if (!connection.TryPeek(out var frame))
        {
            packet = null;
            return false;
        }
        packet = PacketCodec.ReadServerbound(_phase, frame.Span);
        connection.Consume();
        if (_phase == ConnectionPhase.Play)
            ValidatePlayPacket(packet);
        return true;
    }

    public bool TrySend(ClientboundPacket packet)
    {
        lock (_gate)
            return connection.TrySend(PacketCodec.Encode(_phase, packet));
    }

    private bool SendTransition(ClientboundPacket packet, ConnectionPhase nextPhase)
    {
        lock (_gate)
        {
            if (!connection.TrySend(PacketCodec.Encode(_phase, packet)))
                return false;
            _phase = nextPhase;
            return true;
        }
    }

    public void Disconnect(string reason)
    {
        lock (_gate)
        {
            if (_closing)
                return;
            var notice = DisconnectPacket.FromReason(reason);
            connection.TrySend(PacketCodec.Encode(_phase, notice));
            _closing = true;
            connection.CompleteSending(notice.Reason);
        }
    }

    public void ThrowIfFaulted() => connection.ThrowIfFaulted();

    private static string? CheckMods(IReadOnlyList<ModInfo> server, IReadOnlyList<ModVersionEntry> client)
    {
        var versions = new Dictionary<string, SemVersion>(StringComparer.Ordinal);
        foreach (var mod in client)
        {
            if (!SemVersion.TryParse(mod.Version, out var version) || !versions.TryAdd(mod.Id, version))
                throw new InvalidDataException("Invalid or duplicate client mod version.");
        }
        foreach (var mod in server)
        {
            if (!versions.TryGetValue(mod.Id, out var version))
                return $"Required client mod '{mod.Id}' version {mod.Version} is missing.";
            if (!mod.Version.Equals(version))
                return $"Required client mod '{mod.Id}' needs {mod.Version}, but client has {version}.";
        }
        return null;
    }

    private static void ValidatePlayPacket(ServerboundPacket packet)
    {
        switch (packet)
        {
            case InputStatePacket input:
                if (!double.IsFinite(input.Forward) || input.Forward is < -1 or > 1 ||
                    !double.IsFinite(input.Right) || input.Right is < -1 or > 1)
                    throw new InvalidDataException("Invalid movement axes.");
                ValidateAngles(input.Yaw, input.Pitch);
                return;
            case BlockInteractionPacket interaction:
                if (interaction.Operation is not (BlockOperation.Break or BlockOperation.Place))
                    throw new InvalidDataException("Invalid block operation.");
                ValidateAngles(interaction.Yaw, interaction.Pitch);
                return;
            default:
                throw new InvalidDataException("Message is not valid in the current connection phase.");
        }
    }

    private static void ValidateAngles(double yaw, double pitch)
    {
        if (!double.IsFinite(yaw) || !double.IsFinite(pitch) || pitch is < -89 or > 89)
            throw new InvalidDataException("Invalid player angles.");
    }
}
