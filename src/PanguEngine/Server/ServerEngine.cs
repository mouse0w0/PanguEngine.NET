using System.Diagnostics;
using System.Net;
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
    private readonly LocalConnection _connection;
    private bool _joinRequested;
    private bool _connected;
    private bool _isPaused;
    private bool _resetInput;
    private bool _reschedule;
    private bool _shutdownRequested;
    private long _tick;
    private readonly IPEndPoint? _listenEndpoint;
    private uint _localPlayerNetworkId;

    internal ServerEngine(bool isDedicated, IPEndPoint? listenEndpoint = null)
    {
        IsDedicated = isDedicated;
        _listenEndpoint = listenEndpoint;
        _connection = new LocalConnection(_gate);
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

    /// <summary>Whether new gameplay requests and world updates are paused.</summary>
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
                _resetInput |= value;
                _reschedule = true;
                _connection.SetAcceptingRequests(!value && !_shutdownRequested, clearPending: value);
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

    internal LocalConnection ConnectLocal()
    {
        lock (_gate)
        {
            if (IsDedicated)
                throw new InvalidOperationException("Dedicated servers do not accept local connections.");
            _connection.ThrowIfFaulted();
            if (_connected || _joinRequested || _shutdownRequested)
                throw new InvalidOperationException("The server cannot accept another local connection.");
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
            _shutdownRequested = true;
            _connection.SetAcceptingRequests(false, clearPending: false);
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
            List<ServerboundPacket> packets = [];
            while (true)
            {
                packets.Clear();
                var joinRequested = false;
                var resetInput = false;
                var shutdownRequested = false;
                lock (_gate)
                {
                    if (_joinRequested)
                    {
                        _joinRequested = false;
                        _connected = true;
                        joinRequested = true;
                    }
                    else
                    {
                        resetInput = _resetInput;
                        _resetInput = false;
                        shutdownRequested = _shutdownRequested;
                        if (shutdownRequested)
                            _connection.DrainServerboundPackets(packets);
                        else if (!resetInput)
                        {
                            if (_isPaused)
                            {
                                Monitor.Wait(_gate);
                                continue;
                            }
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
                            _connection.DrainServerboundPackets(packets);
                        }
                    }
                }
                if (joinRequested)
                {
                    _localPlayerNetworkId = Game.JoinPlayer();
                    _connection.Publish(Game.CreateInitial(_localPlayerNetworkId));
                    foreach (var chunk in Game.CaptureChunks())
                        _connection.Publish(chunk);
                    _connection.Publish(new InitialWorldCompletePacket());
                    continue;
                }
                if (resetInput)
                    Game.ClearInput();
                if (shutdownRequested)
                {
                    if (packets.Count > 0)
                        _connection.Publish(UpdateLocal(packets, advance: false));
                    Game.Destroy();
                    _connection.Publish(new DisconnectPacket("The server is shutting down."));
                    break;
                }
                if (resetInput)
                    continue;
                var update = UpdateLocal(packets);
                Interlocked.Exchange(ref _tick, Game.Tick);
                if (_connected)
                    _connection.Publish(update);
                deadline += ServerGame.StepSeconds;
            }
            _connection.Complete();
        }
        catch (Exception error) when (!IsDedicated)
        {
            _connection.Complete(error);
        }
    }

    private WorldUpdatePacket UpdateLocal(IReadOnlyList<ServerboundPacket> packets, bool advance = true)
    {
        Game.BeginTick();
        IReadOnlyList<WorldUpdatePacket.InteractionResult> results = _connected ? Game.Apply(_localPlayerNetworkId, packets) : [];
        return Game.FinishTick(advance) with { Results = results };
    }

    private void RunDedicated()
    {
        Game = new ServerGame();
        Engine.ModManager.RunDedicatedServerSetup();
        Engine.ModManager.RunReady();
        var listener = new TcpConnectionListener(_listenEndpoint!);
        var manifest = new ContentManifestPacket(Engine.RegistryManager.ExportIdMappings());
        var mods = Engine.ModManager.LoadedMods.Select(mod => mod.Info).ToArray();
        var sessions = new List<RemoteSession>();
        var accepted = new List<TcpConnection>();
        var clock = Stopwatch.StartNew();
        var deadline = ServerGame.StepSeconds;
        while (true)
        {
            bool shutdown;
            bool paused;
            bool reset;
            lock (_gate)
            {
                shutdown = _shutdownRequested;
                paused = _isPaused;
                reset = _resetInput;
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
                _resetInput = false;
            }
            if (shutdown)
                listener.StopAccepting();
            accepted.Clear();
            listener.DrainAccepted(accepted);
            foreach (var connection in accepted)
                sessions.Add(new RemoteSession(connection, manifest, mods));
            if (reset)
                Game.ClearInput();
            Game.BeginTick();
            var batches = new List<(RemoteSession Session, IReadOnlyList<ServerboundPacket> Packets)>();
            foreach (var session in sessions)
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
            foreach (var session in sessions)
            {
                if (!paused || shutdown)
                    session.Publish(update);
                if (shutdown)
                    session.Disconnect("The server is shutting down.");
            }
            if (shutdown)
                break;
            foreach (var session in sessions.Where(session => session.Connection.IsClosed).ToArray())
            {
                session.Leave(Game);
                listener.Remove(session.Connection);
                sessions.Remove(session);
            }
            deadline += ServerGame.StepSeconds;
        }
        var drainStarted = Stopwatch.GetTimestamp();
        while (sessions.Any(session => !session.Connection.IsClosed) &&
               Stopwatch.GetElapsedTime(drainStarted) < NetworkLimits.FrameTimeout)
        {
            foreach (var session in sessions)
                session.Connection.ThrowIfFaulted();
            Thread.Sleep(1);
        }
        foreach (var session in sessions)
            listener.Remove(session.Connection);
        Game.Destroy();
    }
}
