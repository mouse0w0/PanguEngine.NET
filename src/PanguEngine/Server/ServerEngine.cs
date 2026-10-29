using System.Diagnostics;
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

    internal ServerEngine(bool isDedicated)
    {
        IsDedicated = isDedicated;
        _connection = new LocalConnection(_gate);
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = isDedicated ? "Pangu dedicated server" : "Pangu integrated server"
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
        Current = new ServerEngine(true);
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
            server.WaitForExit();
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
        Engine.Shutdown();
    }

    internal void Start() => _thread.Start();

    internal LocalConnection ConnectLocal()
    {
        lock (_gate)
        {
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
        try
        {
            Game = new ServerGame();
            if (IsDedicated)
            {
                Engine.ModManager.RunDedicatedServerSetup();
                Engine.ModManager.RunReady();
            }
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
                    Game.JoinLocal(_connection);
                    continue;
                }
                if (resetInput)
                    Game.ClearInput();
                if (shutdownRequested)
                {
                    if (packets.Count > 0)
                        _connection.Publish(Game.Update(packets, advance: false)!);
                    Game.Destroy();
                    _connection.Publish(new ServerClosedPacket(Game.Tick));
                    break;
                }
                if (resetInput)
                    continue;
                var update = Game.Update(packets);
                Interlocked.Exchange(ref _tick, Game.Tick);
                if (update is not null)
                    _connection.Publish(update);
                deadline += ServerGame.StepSeconds;
            }
            _connection.Complete();
        }
        catch (Exception error)
        {
            _connection.Complete(error);
        }
    }
}
