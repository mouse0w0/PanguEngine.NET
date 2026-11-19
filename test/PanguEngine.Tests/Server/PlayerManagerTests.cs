using PanguEngine.Network;
using PanguEngine.Server;
using PanguEngine.Server.Game;

namespace PanguEngine.Tests.Server;

public sealed class PlayerManagerTests
{
    [Fact]
    public void OnlineViewAndQueriesTrackTheSamePlayers()
    {
        var game = new ServerGame();
        try
        {
            var manager = new PlayerManager();
            var names = new PlayerNames();
            var online = manager.OnlinePlayers;
            Assert.Empty(online);
            var alice = manager.Add("Alice", game.SpawnPlayerEntity(), CreateSession(names, "Alice"));
            Assert.Same(alice, Assert.Single(online));
            Assert.Equal("Alice", alice.Username);
            Assert.True(game.World.Entities.TryGetEntity(alice.Entity.Handle, out var entity));
            Assert.Same(alice.Entity, entity);
            Assert.True(manager.TryGetPlayer(alice.Id, out var byId));
            Assert.Same(alice, byId);
            Assert.True(manager.TryGetPlayer("ALICE", out var byName));
            Assert.Same(alice, byName);

            var bob = manager.Add("Bob", game.SpawnPlayerEntity(), CreateSession(names, "Bob"));
            Assert.True(manager.TryGetPlayer("BOB", out var byOtherName));
            Assert.Same(bob, byOtherName);
            manager.Remove(alice);
            Assert.Same(bob, Assert.Single(online));
            Assert.False(manager.TryGetPlayer(alice.Id, out var missingId));
            Assert.Null(missingId);
            Assert.False(manager.TryGetPlayer("alice", out var missingName));
            Assert.Null(missingName);
            alice.Session.Disconnect("The test client stopped.");
            alice.Session.ReleaseNameIfClosed();
            var reconnected = manager.Add("ALICE", game.SpawnPlayerEntity(), CreateSession(names, "ALICE"));
            Assert.NotSame(alice, reconnected);
            Assert.Equal(alice.Id, reconnected.Id);
            Assert.True(manager.TryGetPlayer("alice", out var byReconnectedName));
            Assert.Same(reconnected, byReconnectedName);
            Assert.True(manager.TryGetPlayer(alice.Id, out var byReconnectedId));
            Assert.Same(reconnected, byReconnectedId);
            manager.Remove(reconnected);
            Assert.Same(bob, Assert.Single(online));
            Assert.True(manager.TryGetPlayer("BOB", out var retainedBob));
            Assert.Same(bob, retainedBob);
            manager.Remove(bob);
            Assert.Empty(online);
        }
        finally { game.Destroy(); }
    }

    [Fact]
    public void OfflineIdsUseTheFixedNamespaceAndIgnoreUsernameCase()
    {
        var firstGame = new ServerGame();
        var secondGame = new ServerGame();
        try
        {
            var first = new PlayerManager();
            var second = new PlayerManager();
            var firstNames = new PlayerNames();
            var secondNames = new PlayerNames();
            var alice = first.Add("Alice", firstGame.SpawnPlayerEntity(), CreateSession(firstNames, "Alice"));
            var otherAlice = second.Add("alice", secondGame.SpawnPlayerEntity(), CreateSession(secondNames, "alice"));
            var bob = first.Add("Bob", firstGame.SpawnPlayerEntity(), CreateSession(firstNames, "Bob"));

            Assert.Equal(Guid.Parse("f4ac5135-0979-5da5-8c52-f333569305ba"), alice.Id);
            Assert.Equal(alice.Id, otherAlice.Id);
            Assert.NotSame(alice.Entity, otherAlice.Entity);
            Assert.Equal("alice", otherAlice.Username);
            Assert.NotEqual(alice.Id, bob.Id);
        }
        finally
        {
            secondGame.Destroy();
            firstGame.Destroy();
        }
    }

    private static RemoteSession CreateSession(PlayerNames names, string username)
    {
        names.Reserve(username);
        return new RemoteSession(new InMemoryConnection(), username, names);
    }
}
