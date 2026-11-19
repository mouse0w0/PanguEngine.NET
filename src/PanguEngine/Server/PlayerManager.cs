using System.Diagnostics.CodeAnalysis;
using PanguEngine.Server.Game;
using PanguEngine.World.Entities;

namespace PanguEngine.Server;

/// <summary>Tracks the players in a server's play phase.</summary>
/// <remarks>Access play-phase players and enumerate the live collection from the server game thread.</remarks>
public sealed class PlayerManager
{
    private readonly Dictionary<Guid, Player> _players = [];
    private readonly Dictionary<string, Player> _playersByUsername = new(StringComparer.OrdinalIgnoreCase);

    internal PlayerManager() { }

    /// <summary>The live, read-only collection of players in the play phase.</summary>
    public IReadOnlyCollection<Player> OnlinePlayers => _players.Values;

    /// <summary>Attempts to find a player by identifier.</summary>
    /// <param name="id">The player identifier.</param>
    /// <param name="player">The player when found, or null.</param>
    /// <returns>Whether the player is in the play phase.</returns>
    public bool TryGetPlayer(Guid id, [NotNullWhen(true)] out Player? player) => _players.TryGetValue(id, out player);

    /// <summary>Attempts to find a player by username without regard to case.</summary>
    /// <param name="username">The username to find.</param>
    /// <param name="player">The player when found, or null.</param>
    /// <returns>Whether the player is in the play phase.</returns>
    public bool TryGetPlayer(string username, [NotNullWhen(true)] out Player? player) =>
        _playersByUsername.TryGetValue(username, out player);

    internal Player Add(string username, PlayerEntity entity, RemoteSession session)
    {
        var player = new Player(username, entity, session);
        _players.Add(player.Id, player);
        _playersByUsername.Add(player.Username, player);
        return player;
    }

    internal void Remove(Player player)
    {
        _players.Remove(player.Id);
        _playersByUsername.Remove(player.Username);
    }
}
