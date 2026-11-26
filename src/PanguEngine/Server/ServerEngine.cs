using System.Diagnostics;
using System.Net;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;
using PanguEngine.Server.Game;
using PanguEngine.World.Blocks;

namespace PanguEngine.Server;

/// <summary>Owns the authoritative game and its dedicated or integrated server lifetime.</summary>
public sealed class ServerEngine
{
    private readonly InMemoryConnection _connection;
    private readonly object _gate = new();
    private readonly IPEndPoint? _listenEndpoint;
    private readonly PlayerNames _playerNames = new();
    private readonly Thread _thread;
    private bool _connectionClaimed;
    private bool _isPaused;
    private RemoteSession? _pendingLocalSession;
    private bool _reschedule;
    private bool _shutdownRequested;
    private long _tick;

    internal ServerEngine(bool isDedicated, IPEndPoint? listenEndpoint = null)
    {
        IsDedicated = isDedicated;
        _listenEndpoint = listenEndpoint;
        _connection = new InMemoryConnection(_gate);
        _thread = isDedicated ? Thread.CurrentThread : new Thread(Run)
        {
            IsBackground = true,
            Name = "Pangu integrated server"
        };
    }

    /// <summary>The server running in this process.</summary>
    public static ServerEngine Current { get; private set; } = null!;

    /// <summary>Whether this instance runs without a client engine.</summary>
    public bool IsDedicated { get; }

    /// <summary>The game owned by the server thread.</summary>
    public ServerGame Game { get; private set; } = null!;

    /// <summary>The play-phase players, accessed from the server game thread.</summary>
    public PlayerManager PlayerManager { get; } = new();

    /// <summary>The number of completed world simulation steps.</summary>
    public long Tick => Interlocked.Read(ref _tick);

    /// <summary>Whether server rounds discard gameplay input instead of advancing the world.</summary>
    public bool IsPaused
    {
        get
        {
            lock (_gate)
                return _isPaused;
        }
        set
        {
            lock (_gate)
            {
                if (_isPaused == value)
                    return;
                _isPaused = value;
                _reschedule = true;
                Monitor.PulseAll(_gate);
            }
        }
    }

    internal static void StartIntegrated()
    {
        Current = new ServerEngine(false);
        Current.Start();
    }

    internal static void StartDedicated(LaunchOptions options)
    {
        Engine.Initialize(options);
        Current = new ServerEngine(true, new IPEndPoint(options.ListenAddress ?? IPAddress.Loopback, options.Port));
        var server = Current;
        ConsoleCancelEventHandler cancel = (_, args) =>
        {
            args.Cancel = true;
            server.RequestShutdown();
        };
        Console.CancelKeyPress += cancel;
        try
        {
            server.Start();
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
        Engine.Shutdown();
    }

    internal void Start()
    {
        Engine.RegistryManager.AllocateIdTables();
        BlockStateIds.Build();
        if (IsDedicated)
            Run();
        else
            _thread.Start();
    }

    /// <summary>Submits a ready in-process session to the game thread and returns its client endpoint.</summary>
    internal IClientConnection ConnectInMemory(string username = "Player")
    {
        lock (_gate)
        {
            if (IsDedicated)
                throw new InvalidOperationException("Dedicated servers do not accept local connections.");
            _connection.ThrowIfFaulted();
            if (_connectionClaimed || _shutdownRequested)
                throw new InvalidOperationException("The server cannot accept another local connection.");
            _playerNames.Reserve(username);
            _pendingLocalSession = new RemoteSession(_connection, username, _playerNames);
            _connectionClaimed = true;
            Monitor.PulseAll(_gate);
            return _connection;
        }
    }

    /// <summary>Requests normal shutdown without waiting for the server thread.</summary>
    public void RequestShutdown()
    {
        lock (_gate)
        {
            if (_shutdownRequested)
                return;
            _shutdownRequested = true;
            if (!IsDedicated)
                _connection.StopAcceptingRequests(clearPending: _isPaused);
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>Stops the server and waits for its world to be destroyed.</summary>
    public void Destroy()
    {
        RequestShutdown();
        WaitForExit();
    }

    private void WaitForExit()
    {
        _thread.Join();
        _connection.ThrowIfFaulted();
    }

    private void Run()
    {
        if (IsDedicated)
        {
            RunDedicated();
            return;
        }
        try
        {
            Game = new ServerGame();
            var clock = Stopwatch.StartNew();
            var deadline = ServerGame.StepSeconds;
            RemoteSession? session = null;
            Player? player = null;
            while (true)
            {
                IReadOnlyList<ServerboundPacket> packets = [];
                RemoteSession? pendingSession;
                var clientClosed = false;
                var paused = false;
                var shutdownRequested = false;
                lock (_gate)
                {
                    pendingSession = _pendingLocalSession;
                    if (pendingSession is not null)
                        _pendingLocalSession = null;
                    else if (session is not null && _connection.IsClosed)
                        clientClosed = true;
                    else
                    {
                        paused = _isPaused;
                        shutdownRequested = _shutdownRequested;
                        if (!shutdownRequested)
                        {
                            if (_reschedule)
                            {
                                deadline = clock.Elapsed.TotalSeconds + ServerGame.StepSeconds;
                                _reschedule = false;
                            }
                            var remaining = deadline - clock.Elapsed.TotalSeconds;
                            if (remaining > 0)
                            {
                                Monitor.Wait(_gate, (int)Math.Ceiling(remaining * 1000));
                                continue;
                            }
                        }
                        if (session is not null)
                            packets = CollectPlayPackets(Game, PlayerManager, session, ref player);
                    }
                }
                if (pendingSession is not null)
                {
                    if (TryEnterPlay(Game, PlayerManager, pendingSession, ref player))
                        session = pendingSession;
                    else
                    {
                        LeavePlay(Game, PlayerManager, ref player);
                        pendingSession.ReleaseNameIfClosed();
                    }
                    continue;
                }
                if (clientClosed)
                {
                    LeavePlay(Game, PlayerManager, ref player);
                    session!.ReleaseNameIfClosed();
                    session = null;
                    continue;
                }
                if (shutdownRequested)
                {
                    if (player is { } finalPlayer && packets.Any(ServerGame.IsFinalInput))
                    {
                        Game.BeginTick();
                        var results = Game.Apply(finalPlayer, packets, shutdown: true);
                        PublishPlayUpdate(finalPlayer, Game.FinishTick(advance: false), results);
                    }
                    if (session is not null)
                    {
                        session.Disconnect("The server is shutting down.");
                        LeavePlay(Game, PlayerManager, ref player);
                        session.ReleaseNameIfClosed();
                    }
                    else
                        _connection.Complete();
                    Game.Destroy();
                    break;
                }
                deadline += ServerGame.StepSeconds;
                if (paused)
                {
                    Game.ClearInput();
                    continue;
                }
                Game.BeginTick();
                IReadOnlyList<WorldUpdatePacket.InteractionResult> roundResults =
                    player is not null ? Game.Apply(player, packets) : [];
                var update = Game.FinishTick();
                Interlocked.Exchange(ref _tick, Game.Tick);
                if (player is not null)
                    PublishPlayUpdate(player, update, roundResults);
            }
        }
        catch (Exception error) when (!IsDedicated)
        {
            _connection.Complete(error);
        }
    }

    private void RunDedicated()
    {
        Game = new ServerGame();
        Engine.ModManager.RunDedicatedServerSetup();
        Engine.ModManager.RunReady();
        var listener = new TcpConnectionListener(_listenEndpoint!);
        var manifest = new ContentManifestPacket(Engine.RegistryManager.ExportIdMappings());
        var mods = Engine.ModManager.LoadedMods.Select(mod => mod.Info).ToArray();
        var sessions = new List<DedicatedSession>();
        var accepted = new List<TcpConnection>();
        var clock = Stopwatch.StartNew();
        var deadline = ServerGame.StepSeconds;
        while (true)
        {
            bool shutdown;
            bool paused;
            lock (_gate)
            {
                shutdown = _shutdownRequested;
                paused = _isPaused;
                if (_reschedule)
                {
                    deadline = clock.Elapsed.TotalSeconds + ServerGame.StepSeconds;
                    _reschedule = false;
                }
                var remaining = deadline - clock.Elapsed.TotalSeconds;
                if (!shutdown && remaining > 0)
                {
                    Monitor.Wait(_gate, (int)Math.Ceiling(remaining * 1000));
                    continue;
                }
            }
            if (shutdown)
                listener.StopAccepting();
            accepted.Clear();
            listener.DrainAccepted(accepted);
            foreach (var connection in accepted)
            {
                var endpoint = new TcpServerConnection(connection);
                var preparation = endpoint.PrepareAsync(manifest, mods, _playerNames);
                sessions.Add(new DedicatedSession(connection, new RemoteSession(endpoint, preparation, _playerNames)));
            }
            RunDedicatedPlayRound(Game, PlayerManager, sessions, paused, shutdown, ref _tick);
            if (shutdown)
                break;
            RemoveClosedDedicatedSessions(Game, PlayerManager, listener, sessions);
            deadline += ServerGame.StepSeconds;
        }
        var drainStarted = Stopwatch.GetTimestamp();
        while (sessions.Any(entry => !entry.Connection.IsClosed) &&
               Stopwatch.GetElapsedTime(drainStarted) < NetworkLimits.FrameTimeout)
        {
            foreach (var entry in sessions)
                entry.Connection.ThrowIfFaulted();
            Thread.Sleep(1);
        }
        foreach (var entry in sessions)
        {
            entry.Connection.Close("The server is shutting down.");
            entry.Session.Preparation.GetAwaiter().GetResult();
            LeavePlay(Game, PlayerManager, ref entry.Player);
            entry.Session.ReleaseNameIfClosed();
            listener.Remove(entry.Connection);
        }
        Game.Destroy();
    }

    /// <summary>Runs one dedicated Play round, preserving the existing connection order for applying player input.</summary>
    internal static void RunDedicatedPlayRound(ServerGame game, PlayerManager players,
        IReadOnlyList<DedicatedSession> sessions, bool paused, bool shutdown, ref long tick)
    {
        if (paused)
            game.ClearInput();
        game.BeginTick();
        var batches = new (Player? Player, IReadOnlyList<ServerboundPacket> Packets,
            IReadOnlyList<WorldUpdatePacket.InteractionResult> Results)[sessions.Count];
        for (var i = 0; i < sessions.Count; i++)
        {
            var entry = sessions[i];
            if (shutdown && entry.Player is null)
                entry.Session.Disconnect("The server is shutting down.");
            var packets = CollectPlayPackets(game, players, entry.Session, ref entry.Player);
            batches[i] = (entry.Player, packets, []);
        }
        if (!paused)
        {
            for (var i = 0; i < batches.Length; i++)
            {
                ref var batch = ref batches[i];
                if (batch.Player is { } player)
                    batch.Results = game.Apply(player, batch.Packets, shutdown);
            }
        }
        var update = game.FinishTick(advance: !paused && !shutdown);
        Interlocked.Exchange(ref tick, game.Tick);
        for (var i = 0; i < batches.Length; i++)
        {
            var batch = batches[i];
            if ((!paused || shutdown) && batch.Player is { } player)
                PublishPlayUpdate(player, update, batch.Results);
            if (shutdown)
                sessions[i].Session.Disconnect("The server is shutting down.");
        }
    }

    internal static void RemoveClosedDedicatedSessions(ServerGame game, PlayerManager players,
        TcpConnectionListener listener, List<DedicatedSession> sessions)
    {
        foreach (var entry in sessions.Where(entry => entry.Connection.IsClosed &&
                     entry.Session.Preparation.IsCompleted).ToArray())
        {
            entry.Session.Preparation.GetAwaiter().GetResult();
            LeavePlay(game, players, ref entry.Player);
            entry.Session.ReleaseNameIfClosed();
            listener.Remove(entry.Connection);
            sessions.Remove(entry);
        }
    }

    /// <summary>Hands a ready session to Play, or ends its participation when receiving fails or the session closes.</summary>
    internal static IReadOnlyList<ServerboundPacket> CollectPlayPackets(ServerGame game, PlayerManager players,
        RemoteSession session, ref Player? player)
    {
        if (TryEnterPlay(game, players, session, ref player) && session.TryReceiveBatch(out var packets))
            return packets;
        LeavePlay(game, players, ref player);
        session.ReleaseNameIfClosed();
        return [];
    }

    /// <summary>Checks Play readiness and admits a ready player without consuming queued input.</summary>
    /// <remarks>On an initial send rejection, the caller must remove the registered player before processing input.</remarks>
    private static bool TryEnterPlay(ServerGame game, PlayerManager players, RemoteSession session, ref Player? player)
    {
        if (session.GetReadyUsername() is not { } username)
            return false;
        if (player is not null)
            return true;

        var entity = game.SpawnPlayerEntity();
        var joined = players.Add(username, entity, session);
        player = joined;
        if (!joined.Session.TrySend(game.CreateInitial(entity.NetworkId)))
            return false;
        foreach (var chunk in game.CaptureChunks())
        {
            if (!joined.Session.TrySend(chunk))
                return false;
        }
        return joined.Session.FinishInitialWorld();
    }

    private static void LeavePlay(ServerGame game, PlayerManager players, ref Player? player)
    {
        if (player is null)
            return;
        game.RemovePlayerEntity(player.Entity);
        players.Remove(player);
        player = null;
    }

    private static void PublishPlayUpdate(Player player, WorldUpdatePacket update,
        IReadOnlyList<WorldUpdatePacket.InteractionResult> results)
    {
        if (!player.Session.IsClosing)
            player.Session.TrySend(update with { Results = results });
    }

    /// <summary>Tracks the accepted connection separately from its current Play player.</summary>
    internal sealed class DedicatedSession(TcpConnection connection, RemoteSession session)
    {
        internal Player? Player;
        internal TcpConnection Connection { get; } = connection;
        internal RemoteSession Session { get; } = session;
    }
}
