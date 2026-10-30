using System.Net;
using System.Net.Sockets;
using StageLink.Protocol;
using StageLink.Simulator;

namespace StageLink.Simulator.Tests;

// Integration tests: a real server on localhost, talked to over real TCP.
public class SpeakerServerTests : IDisposable
{
    private readonly SpeakerState _state = new("Main L");
    private readonly SpeakerServer _server;
    private readonly CancellationTokenSource _shutdown = new();

    // Safety net so a broken test fails instead of hanging forever.
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(5));

    public SpeakerServerTests()
    {
        // Port 0 = let the operating system pick any free port.
        _server = new SpeakerServer(new Speaker(_state), IPAddress.Loopback, 0);
        _server.Start();
        _ = _server.RunAsync(_shutdown.Token);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        _timeout.Dispose();
    }

    private async Task<TcpClient> ConnectAsync()
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _server.Port, _timeout.Token);
        return client;
    }

    [Fact]
    public async Task Ping_RepliesPong()
    {
        using TcpClient client = await ConnectAsync();
        NetworkStream stream = client.GetStream();

        await stream.WriteAsync(new Frame(Command.Ping, 300, []).Encode(), _timeout.Token);
        Frame? reply = await new FrameReader(stream).ReadFrameAsync(_timeout.Token);

        Assert.NotNull(reply);
        Assert.Equal(Command.Pong, reply.Command);
        Assert.Equal(300, reply.MessageNumber);
    }

    [Fact]
    public async Task SetGain_UpdatesSpeakerState()
    {
        using TcpClient client = await ConnectAsync();
        NetworkStream stream = client.GetStream();

        var payload = new ByteWriter();
        payload.WriteFixedPoint(-6.5);
        await stream.WriteAsync(new Frame(Command.SetGain, 1, payload.ToArray()).Encode(), _timeout.Token);
        Frame? reply = await new FrameReader(stream).ReadFrameAsync(_timeout.Token);

        Assert.Equal(Command.Ack, reply?.Command);
        Assert.Equal(-6.5, _state.Gain);
    }

    [Fact]
    public async Task TwoFramesInOneWrite_GetTwoReplies()
    {
        using TcpClient client = await ConnectAsync();
        NetworkStream stream = client.GetStream();
        var reader = new FrameReader(stream);

        byte[] both = [.. new Frame(Command.Ping, 1, []).Encode(), .. new Frame(Command.Ping, 2, []).Encode()];
        await stream.WriteAsync(both, _timeout.Token);

        Frame? first = await reader.ReadFrameAsync(_timeout.Token);
        Frame? second = await reader.ReadFrameAsync(_timeout.Token);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, first.MessageNumber);
        Assert.Equal(2, second.MessageNumber);
    }

    [Fact]
    public async Task InvalidFrame_IsSkipped_AndConnectionKeepsWorking()
    {
        using TcpClient client = await ConnectAsync();
        NetworkStream stream = client.GetStream();

        byte[] corrupted = new Frame(Command.Ping, 1, []).Encode();
        corrupted[^1] ^= 0xFF; // break the CRC
        byte[] valid = new Frame(Command.Ping, 2, []).Encode();
        byte[] both = [.. corrupted, .. valid];
        await stream.WriteAsync(both, _timeout.Token);

        Frame? reply = await new FrameReader(stream).ReadFrameAsync(_timeout.Token);

        Assert.NotNull(reply);
        Assert.Equal(2, reply.MessageNumber);
    }

    [Fact]
    public async Task TwoClients_AreServedAtTheSameTime()
    {
        using TcpClient first = await ConnectAsync();
        using TcpClient second = await ConnectAsync();

        // The second client is answered while the first is still connected.
        await second.GetStream().WriteAsync(new Frame(Command.Ping, 9, []).Encode(), _timeout.Token);
        Frame? reply = await new FrameReader(second.GetStream()).ReadFrameAsync(_timeout.Token);

        Assert.NotNull(reply);
        Assert.Equal(9, reply.MessageNumber);
    }

    // Telemetry

    private static byte[] Interval(ushort milliseconds)
    {
        var writer = new ByteWriter();
        writer.WriteUInt16(milliseconds);
        return writer.ToArray();
    }

    [Fact]
    public async Task Subscribe_SendsTelemetryRepeatedly()
    {
        using TcpClient client = await ConnectAsync();
        NetworkStream stream = client.GetStream();
        var reader = new FrameReader(stream);

        await stream.WriteAsync(new Frame(Command.Subscribe, 77, Interval(100)).Encode(), _timeout.Token);

        Frame? ack = await reader.ReadFrameAsync(_timeout.Token);
        Frame? first = await reader.ReadFrameAsync(_timeout.Token);
        Frame? second = await reader.ReadFrameAsync(_timeout.Token);

        Assert.Equal(Command.Ack, ack?.Command);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(Command.Telemetry, first.Command);
        Assert.Equal(Command.Telemetry, second.Command);
        Assert.Equal(77, first.MessageNumber); // reuses the Subscribe message number
    }

    [Fact]
    public async Task Unsubscribe_StopsTelemetry()
    {
        using TcpClient client = await ConnectAsync();
        NetworkStream stream = client.GetStream();
        var reader = new FrameReader(stream);

        await stream.WriteAsync(new Frame(Command.Subscribe, 1, Interval(100)).Encode(), _timeout.Token);
        await reader.ReadFrameAsync(_timeout.Token); // Ack
        await reader.ReadFrameAsync(_timeout.Token); // first Telemetry

        await stream.WriteAsync(new Frame(Command.Unsubscribe, 2, []).Encode(), _timeout.Token);

        // Telemetry already on its way may arrive before the Ack; skip it.
        Frame? frame;
        do
        {
            frame = await reader.ReadFrameAsync(_timeout.Token);
        }
        while (frame is not null && frame.Command == Command.Telemetry);

        Assert.Equal(Command.Ack, frame?.Command);
        Assert.Equal(2, frame?.MessageNumber ?? 0);

        // After the Ack, nothing more should arrive (4 intervals of silence).
        using var quiet = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadFrameAsync(quiet.Token));
    }

    [Fact]
    public async Task RepliesAndTelemetry_NeverCorruptEachOther()
    {
        using TcpClient client = await ConnectAsync();
        NetworkStream stream = client.GetStream();
        var reader = new FrameReader(stream);
        const int pingCount = 30;

        await stream.WriteAsync(new Frame(Command.Subscribe, 1000, Interval(100)).Encode(), _timeout.Token);

        // Pings spread over ~600 ms, so Telemetry is sent in between them.
        for (ushort i = 1; i <= pingCount; i++)
        {
            await stream.WriteAsync(new Frame(Command.Ping, i, []).Encode(), _timeout.Token);
            await Task.Delay(20, _timeout.Token);
        }

        // Every frame must decode (a corrupted one would throw), and every Pong
        // must arrive, in order.
        var pongs = new List<ushort>();
        int telemetryCount = 0;
        while (pongs.Count < pingCount)
        {
            Frame? frame = await reader.ReadFrameAsync(_timeout.Token);
            Assert.NotNull(frame);

            if (frame.Command == Command.Pong)
            {
                pongs.Add(frame.MessageNumber);
            }
            else if (frame.Command == Command.Telemetry)
            {
                telemetryCount++;
            }
        }

        Assert.Equal(Enumerable.Range(1, pingCount).Select(n => (ushort)n), pongs);
        Assert.True(telemetryCount > 0, "expected Telemetry frames between the Pongs");
    }
}
