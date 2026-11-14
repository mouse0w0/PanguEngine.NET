using System.Diagnostics;
using System.Text;
using PanguEngine.Modding;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Network.Packets.Serverbound;
using PanguEngine.Versioning;

namespace PanguEngine.Server.Game;

internal sealed class RemoteSession(TcpConnection connection, ContentManifestPacket manifest, IReadOnlyList<ModInfo> mods)
{
    private readonly long _startedAt = connection.ConnectedAt;
    private Stage _stage;
    private uint? _playerNetworkId;
    private IReadOnlyList<WorldUpdatePacket.InteractionResult> _results = [];

    internal TcpConnection Connection => connection;
    internal bool IsPlaying => _stage == Stage.Playing;

    internal IReadOnlyList<ServerboundPacket> Receive(ServerGame game)
    {
        connection.ThrowIfFaulted();
        _results = [];
        if (connection.IsClosed || _stage == Stage.Closing)
        {
            Leave(game);
            return [];
        }
        if (_stage != Stage.Playing && Stopwatch.GetElapsedTime(_startedAt) >= NetworkLimits.HandshakeTimeout)
        {
            Disconnect("Content handshake timed out.");
            return [];
        }
        var packets = new List<ServerboundPacket>();
        var count = connection.IncomingCount;
        for (var i = 0; i < count; i++)
        {
            ServerboundPacket packet;
            try
            {
                if (!connection.TryPeek(out var frame))
                    break;
                packet = PacketCodec.ReadServerbound(frame.Span);
                connection.Consume();
                Validate(packet);
            }
            catch (Exception error) when (error is InvalidDataException or EndOfStreamException or DecoderFallbackException)
            {
                Disconnect(error.Message);
                Leave(game);
                return [];
            }
            catch (ConnectionClosedException)
            {
                Leave(game);
                return [];
            }
            switch (packet)
            {
                case ClientHelloPacket hello:
                    string? rejection;
                    try { rejection = CheckMods(mods, hello.Mods); }
                    catch (InvalidDataException error) { rejection = error.Message; }
                    if (rejection is not null)
                    {
                        Disconnect(rejection);
                        return [];
                    }
                    try { Send(manifest); }
                    catch (InvalidDataException)
                    {
                        Disconnect("Server content manifest exceeds the 1 MiB message limit.");
                        return [];
                    }
                    _stage = Stage.WaitingReady;
                    break;
                case ContentReadyPacket:
                    connection.EnableHeartbeat();
                    _playerNetworkId = game.JoinPlayer();
                    Send(game.CreateInitial(_playerNetworkId.Value));
                    foreach (var chunk in game.CaptureChunks())
                    {
                        if (!Send(chunk))
                            break;
                    }
                    Send(new InitialWorldCompletePacket());
                    _stage = Stage.Playing;
                    break;
                default:
                    packets.Add(packet);
                    break;
            }
        }
        return packets;
    }

    internal void Apply(ServerGame game, IReadOnlyList<ServerboundPacket> packets)
    {
        if (_playerNetworkId is { } playerNetworkId)
            _results = game.Apply(playerNetworkId, packets);
    }

    internal void Publish(WorldUpdatePacket update)
    {
        if (_stage == Stage.Playing && !connection.IsClosed)
            Send(update with { Results = _results });
    }

    internal void Leave(ServerGame game)
    {
        if (_playerNetworkId is { } playerNetworkId)
        {
            game.LeavePlayer(playerNetworkId);
            _playerNetworkId = null;
        }
    }

    internal void Disconnect(string reason)
    {
        if (_stage == Stage.Closing)
            return;
        if (reason.Length > 512)
            reason = "The connection was closed; the diagnostic message exceeded its length limit.";
        Send(new DisconnectPacket(reason));
        _stage = Stage.Closing;
        connection.CompleteSending(reason);
    }

    private bool Send(ClientboundPacket packet) => connection.TrySend(PacketCodec.Encode(packet));

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

    private void Validate(ServerboundPacket packet)
    {
        switch (packet)
        {
            case ClientHelloPacket hello when _stage == Stage.WaitingHello:
                if (hello.ProtocolVersion != PacketCodec.ProtocolVersion)
                    throw new InvalidDataException("Unsupported network protocol version.");
                return;
            case ContentReadyPacket when _stage == Stage.WaitingReady:
                return;
            case InputStatePacket input when _stage == Stage.Playing:
                if (!double.IsFinite(input.Forward) || input.Forward is < -1 or > 1 ||
                    !double.IsFinite(input.Right) || input.Right is < -1 or > 1)
                    throw new InvalidDataException("Invalid movement axes.");
                ValidateAngles(input.Yaw, input.Pitch);
                return;
            case BlockInteractionPacket interaction when _stage == Stage.Playing:
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

    private enum Stage { WaitingHello, WaitingReady, Playing, Closing }
}
