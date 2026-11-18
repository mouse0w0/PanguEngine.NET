using System.Diagnostics;
using System.Net;
using PanguEngine.World.Blocks;
using PanguEngine.Server.Game;
using PanguEngine.Network;
using PanguEngine.Network.Packets;
using PanguEngine.Network.Packets.Clientbound;

namespace PanguEngine.Server;

/// <summary>Owns the authoritative game and its dedicated or integrated server lifetime.</summary>
public sealed class ServerEngine
{
    private readonly object _gate = new();
    private readonly Thread _thread;
    private readonly InMemoryConnection _connection;
    private bool _joinRequested;
    private bool _connectionClaimed;
    private bool _isPaused;
    private bool _reschedule;
    private bool _shutdownRequested;
    private long _tick;
    private readonly IPEndPoint? _listenEndpoint;

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

    /// <summary>Reserves the integrated server's in-process session and returns its client endpoint.</summary>
    internal IClientConnection ConnectInMemory()
    {
        lock (_gate)
        {
            if (IsDedicated)
                throw new InvalidOperationException("Dedicated servers do not accept local connections.");
            _connection.ThrowIfFaulted();
            if (_connectionClaimed || _shutdownRequested)
                throw new InvalidOperationException("The server cannot accept another local connection.");
            _connectionClaimed = true;
            _joinRequested = true;
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
            while (true)
            {
                IReadOnlyList<ServerboundPacket> packets = [];
                var joinRequested = false;
                var clientClosed = false;
                var paused = false;
                var shutdownRequested = false;
                lock (_gate)
                {
                    if (_joinRequested)
                    {
                        _joinRequested = false;
                        joinRequested = true;
                    }
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
                            packets = session.Receive(Game);
                    }
                }
                if (joinRequested)
                {
                    if (!_connection.IsClosed)
                        session = new RemoteSession(_connection, Game);
                    continue;
                }
                if (clientClosed)
                {
                    session!.Leave(Game);
                    session = null;
                    continue;
                }
                if (shutdownRequested)
                {
                    if (session is not null && packets.Count > 0)
                    {
                        Game.BeginTick();
                        session.Apply(Game, packets);
                        session.Publish(Game.FinishTick(advance: false));
                    }
                    Game.Destroy();
                    if (session is not null)
                        session.Disconnect("The server is shutting down.");
                    else
                        _connection.Complete();
                    break;
                }
                deadline += ServerGame.StepSeconds;
                if (paused)
                {
                    Game.ClearInput();
                    continue;
                }
                Game.BeginTick();
                session?.Apply(Game, packets);
                var update = Game.FinishTick();
                Interlocked.Exchange(ref _tick, Game.Tick);
                session?.Publish(update);
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
        var sessions = new List<(TcpConnection Connection, RemoteSession Session)>();
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
                sessions.Add((connection, new RemoteSession(new TcpServerConnection(connection), manifest, mods)));
            if (paused)
                Game.ClearInput();
            Game.BeginTick();
            var batches = new List<(RemoteSession Session, IReadOnlyList<ServerboundPacket> Packets)>();
            foreach (var (_, session) in sessions)
            {
                if (shutdown && !session.IsPlaying)
                    session.Disconnect("The server is shutting down.");
                batches.Add((session, session.Receive(Game)));
            }
            foreach (var batch in batches)
            {
                if (!paused)
                    batch.Session.Apply(Game, batch.Packets);
            }
            var update = Game.FinishTick(advance: !paused && !shutdown);
            Interlocked.Exchange(ref _tick, Game.Tick);
            foreach (var (_, session) in sessions)
            {
                if (!paused || shutdown)
                    session.Publish(update);
                if (shutdown)
                    session.Disconnect("The server is shutting down.");
            }
            if (shutdown)
                break;
            foreach (var (connection, session) in sessions.Where(entry => entry.Connection.IsClosed).ToArray())
            {
                session.Leave(Game);
                listener.Remove(connection);
                sessions.Remove((connection, session));
            }
            deadline += ServerGame.StepSeconds;
        }
        var drainStarted = Stopwatch.GetTimestamp();
        while (sessions.Any(entry => !entry.Connection.IsClosed) &&
               Stopwatch.GetElapsedTime(drainStarted) < NetworkLimits.FrameTimeout)
        {
            foreach (var (connection, _) in sessions)
                connection.ThrowIfFaulted();
            Thread.Sleep(1);
        }
        foreach (var (connection, _) in sessions)
            listener.Remove(connection);
        Game.Destroy();
    }
}
