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
}
