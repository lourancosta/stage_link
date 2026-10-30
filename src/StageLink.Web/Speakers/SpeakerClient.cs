using StageLink.Protocol;

namespace StageLink.Web.Speakers;

// Typed commands for one speaker, so callers never deal with bytes or frames.
// A Nack becomes a SpeakerCommandException; a reply of the wrong kind becomes
// an InvalidDataException.
public sealed class SpeakerClient : IAsyncDisposable
{
    private readonly SpeakerConnection _connection;

    public SpeakerClient(SpeakerConnection connection)
    {
        _connection = connection;
        _connection.TelemetryReceived += OnTelemetryReceived;
    }

    // Raised for every Telemetry update, already parsed.
    public event Action<TelemetryReading>? TelemetryReceived;

    public bool IsConnected => _connection.IsConnected;

    public static async Task<SpeakerClient> ConnectAsync(
        string host,
        int port,
        TimeSpan replyTimeout,
        CancellationToken cancellationToken = default)
    {
        SpeakerConnection connection = await SpeakerConnection.ConnectAsync(host, port, replyTimeout, cancellationToken);
        return new SpeakerClient(connection);
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(Command.Ping, [], Command.Pong, cancellationToken);
    }

    public async Task<SpeakerStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        Frame reply = await SendAsync(Command.GetState, [], Command.State, cancellationToken);
        return SpeakerStatus.FromPayload(reply.Payload);
    }

    public async Task SetGainAsync(double gain, CancellationToken cancellationToken = default)
    {
        await SendAsync(Command.SetGain, FixedPointPayload(gain), Command.Ack, cancellationToken);
    }

    public async Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default)
    {
        await SendAsync(Command.SetMute, [(byte)(muted ? 1 : 0)], Command.Ack, cancellationToken);
    }

    public async Task SetTiltAsync(double tilt, CancellationToken cancellationToken = default)
    {
        await SendAsync(Command.SetTilt, FixedPointPayload(tilt), Command.Ack, cancellationToken);
    }

    public async Task SubscribeAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        double milliseconds = Math.Round(interval.TotalMilliseconds);
        if (milliseconds < 0 || milliseconds > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must fit in 2 bytes of milliseconds.");
        }

        var payload = new ByteWriter();
        payload.WriteUInt16((ushort)milliseconds);
        await SendAsync(Command.Subscribe, payload.ToArray(), Command.Ack, cancellationToken);
    }

    public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(Command.Unsubscribe, [], Command.Ack, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _connection.TelemetryReceived -= OnTelemetryReceived;
        await _connection.DisposeAsync();
    }

    // Sends a command and checks the reply is the expected kind.
    private async Task<Frame> SendAsync(Command command, byte[] payload, Command expectedReply, CancellationToken cancellationToken)
    {
        Frame reply = await _connection.SendAsync(command, payload, cancellationToken);

        if (reply.Command == Command.Nack)
        {
            byte[] data = reply.Payload;
            if (data.Length != 1)
            {
                throw new InvalidDataException($"Nack for {command} has {data.Length} data bytes; expected 1.");
            }

            throw new SpeakerCommandException(command, (NackCode)data[0]);
        }

        if (reply.Command != expectedReply)
        {
            throw new InvalidDataException($"Expected {expectedReply} in reply to {command}, got {reply.Command}.");
        }

        return reply;
    }

    // Throws ArgumentOutOfRangeException (via FixedPoint) before anything is sent
    // if the value can't be represented on the wire.
    private static byte[] FixedPointPayload(double value)
    {
        var writer = new ByteWriter();
        writer.WriteFixedPoint(value);
        return writer.ToArray();
    }

    private void OnTelemetryReceived(Frame frame)
    {
        TelemetryReading reading;
        try
        {
            reading = TelemetryReading.FromPayload(frame.Payload);
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
        {
            return; // Malformed Telemetry; skip it.
        }

        TelemetryReceived?.Invoke(reading);
    }
}
