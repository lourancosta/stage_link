using System.Net;
using System.Net.Sockets;
using StageLink.Protocol;

namespace StageLink.Simulator;

// Accepts TCP connections and answers every frame with the speaker's reply.
// Each connected app is served independently, so several can be connected at once.
// Subscribed connections also receive Telemetry on a timer.
public class SpeakerServer
{
    private static readonly TimeSpan SimulationStep = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromMilliseconds(50);

    private readonly Speaker _speaker;
    private readonly TcpListener _listener;
    private readonly TextWriter _log;
    private readonly MeasurementSimulator? _simulator;

    // Connections, telemetry timers and the simulation all touch the speaker's
    // state from different tasks, but SpeakerState is not thread-safe, so every
    // access goes through this lock.
    private readonly Lock _speakerLock = new();

    public SpeakerServer(
        Speaker speaker,
        IPAddress address,
        int port,
        TextWriter? log = null,
        MeasurementSimulator? simulator = null)
    {
        _speaker = speaker;
        _listener = new TcpListener(address, port);
        _log = log ?? TextWriter.Null;
        _simulator = simulator;
    }

    // The port actually in use (useful when port 0 asked the OS to pick one).
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
    }

    // Accepts connections until the token is cancelled. Call Start() first.
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Task simulation = _simulator is null
            ? Task.CompletedTask
            : RunSimulationAsync(_simulator, cancellationToken);

        try
        {
            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);

                // Don't await: serve this client in the background and go straight
                // back to accepting the next one.
                _ = ServeClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _listener.Stop();
            await simulation;
        }
    }

    // Advances the measurements every 100 ms.
    private async Task RunSimulationAsync(MeasurementSimulator simulator, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SimulationStep);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                lock (_speakerLock)
                {
                    simulator.Tick(SimulationStep);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Server is shutting down.
        }
    }

    private async Task ServeClientAsync(TcpClient client, CancellationToken serverToken)
    {
        // Cancelled when this connection ends (or the whole server stops),
        // which also stops this connection's telemetry.
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(serverToken);

        // Replies and Telemetry share one stream, so only one may write at a time.
        // SemaphoreSlim instead of lock: we need to await the write while holding it.
        using var writeLock = new SemaphoreSlim(1, 1);

        using (client)
        {
            EndPoint? remote = client.Client.RemoteEndPoint;
            _log.WriteLine($"Connected: {remote}");

            NetworkStream stream = client.GetStream();
            var reader = new FrameReader(stream);
            var session = new Session();

            Task telemetry = SendTelemetryAsync(stream, session, writeLock, connection.Token);

            try
            {
                while (true)
                {
                    Frame? request;
                    try
                    {
                        request = await reader.ReadFrameAsync(connection.Token);
                    }
                    catch (InvalidFrameException ex)
                    {
                        // A damaged frame was read completely, so the stream is still in
                        // step: skip it and keep going.
                        _log.WriteLine($"Skipped invalid frame from {remote}: {ex.Message}");
                        continue;
                    }

                    if (request is null)
                    {
                        break; // The app closed the connection.
                    }

                    Frame reply;
                    lock (_speakerLock)
                    {
                        reply = _speaker.Handle(request, session);
                    }

                    _log.WriteLine($"{request.Command} #{request.MessageNumber} -> {reply.Command}");
                    await WriteFrameAsync(stream, reply, writeLock, connection.Token);
                }
            }
            catch (Exception ex) when (ex is IOException or EndOfStreamException or OperationCanceledException)
            {
                // Connection dropped, or the server is shutting down.
            }
            finally
            {
                connection.Cancel();
                await telemetry;
            }

            _log.WriteLine($"Disconnected: {remote}");
        }
    }

    // Runs for the whole connection. While subscribed, sends Telemetry every
    // interval; while not, checks again shortly.
    private async Task SendTelemetryAsync(
        NetworkStream stream,
        Session session,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                TimeSpan? interval;
                lock (_speakerLock)
                {
                    interval = session.TelemetryInterval;
                }

                await Task.Delay(interval ?? IdleCheckInterval, cancellationToken);
                if (interval is null)
                {
                    continue;
                }

                // Hold the write lock while checking and building, so a Telemetry frame
                // can never be sent after the Ack of an Unsubscribe.
                await writeLock.WaitAsync(cancellationToken);
                try
                {
                    Frame? frame = null;
                    lock (_speakerLock)
                    {
                        if (session.TelemetryInterval is not null)
                        {
                            frame = _speaker.BuildTelemetry(session.TelemetryMessageNumber);
                        }
                    }

                    if (frame is not null)
                    {
                        await stream.WriteAsync(frame.Encode(), cancellationToken);
                    }
                }
                finally
                {
                    writeLock.Release();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Connection closed or server stopping.
        }
    }

    private static async Task WriteFrameAsync(
        NetworkStream stream,
        Frame frame,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await stream.WriteAsync(frame.Encode(), cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
