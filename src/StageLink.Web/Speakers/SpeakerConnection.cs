using System.Collections.Concurrent;
using System.Net.Sockets;
using StageLink.Protocol;

namespace StageLink.Web.Speakers;

// A TCP connection to one speaker.
// Replies, Telemetry and other requests' replies all arrive mixed together, so
// each request registers a "promise" under its message number, and a single
// read loop fulfils the right promise when the matching reply arrives.
public sealed class SpeakerConnection : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly TimeSpan _replyTimeout;

    // Requests waiting for their reply, by message number.
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<Frame>> _pending = new();

    // Several requests can be sent at the same time, but only one may write at once.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private readonly CancellationTokenSource _closing = new();
    private readonly Task _readLoop;
    private int _lastMessageNumber;
    private volatile bool _isConnected = true;

    private SpeakerConnection(TcpClient client, TimeSpan replyTimeout)
    {
        _client = client;
        _stream = client.GetStream();
        _replyTimeout = replyTimeout;
        _readLoop = ReadLoopAsync();
    }

    // Raised for every Telemetry frame, on the read loop's thread.
    public event Action<Frame>? TelemetryReceived;

    public bool IsConnected => _isConnected;

    // Constructors can't be async, so connecting happens in a static factory method.
    public static async Task<SpeakerConnection> ConnectAsync(
        string host,
        int port,
        TimeSpan replyTimeout,
        CancellationToken cancellationToken = default)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return new SpeakerConnection(client, replyTimeout);
    }

    // Sends a request and waits for the reply with the same message number.
    // Throws TimeoutException if no reply arrives in time, IOException if the
    // connection is (or becomes) closed.
    public async Task<Frame> SendAsync(Command command, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new IOException("The connection to the speaker is closed.");
        }

        ushort messageNumber = NextMessageNumber();

        // RunContinuationsAsynchronously: the read loop completes this, and we don't
        // want the caller's code to run on (and block) the read loop.
        var reply = new TaskCompletionSource<Frame>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(messageNumber, reply))
        {
            throw new InvalidOperationException($"Message number {messageNumber} is already waiting for a reply.");
        }

        // The read loop may have ended between the first check and TryAdd; if so it
        // has already failed everything it saw, so fail this one ourselves.
        if (!_isConnected)
        {
            reply.TrySetException(new IOException("The connection to the speaker is closed."));
        }

        try
        {
            byte[] bytes = new Frame(command, messageNumber, payload).Encode();

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await _stream.WriteAsync(bytes, cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }

            // WaitAsync throws TimeoutException if the reply takes too long.
            return await reply.Task.WaitAsync(_replyTimeout, cancellationToken);
        }
        finally
        {
            _pending.TryRemove(messageNumber, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _closing.Cancel();
        _client.Dispose();
        await _readLoop;
        _closing.Dispose();
        _writeLock.Dispose();
    }

    // Thread-safe counter. ushort wraps from 65535 back to 0, which is fine:
    // a number is only reused long after its request has finished.
    private ushort NextMessageNumber()
    {
        return (ushort)Interlocked.Increment(ref _lastMessageNumber);
    }

    private async Task ReadLoopAsync()
    {
        var reader = new FrameReader(_stream);

        try
        {
            while (true)
            {
                Frame? frame;
                try
                {
                    frame = await reader.ReadFrameAsync(_closing.Token);
                }
                catch (InvalidFrameException)
                {
                    continue; // A damaged frame was read completely; skip it.
                }

                if (frame is null)
                {
                    break; // The speaker closed the connection.
                }

                if (frame.Command == Command.Telemetry)
                {
                    RaiseTelemetry(frame);
                }
                else if (_pending.TryGetValue(frame.MessageNumber, out TaskCompletionSource<Frame>? reply))
                {
                    reply.TrySetResult(frame);
                }

                // Otherwise: a late reply to a request that already timed out. Ignore it.
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or OperationCanceledException or ObjectDisposedException)
        {
            // Connection dropped or we're closing it.
        }
        finally
        {
            _isConnected = false;

            // Nobody will answer the requests still waiting, so fail them now
            // instead of letting them sit until their timeout.
            foreach (TaskCompletionSource<Frame> reply in _pending.Values)
            {
                reply.TrySetException(new IOException("The connection to the speaker was closed."));
            }
        }
    }

    private void RaiseTelemetry(Frame frame)
    {
        try
        {
            TelemetryReceived?.Invoke(frame);
        }
        catch
        {
            // A broken event handler must not stop the read loop for everyone else.
        }
    }
}
