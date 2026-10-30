using System.Net;
using System.Net.Sockets;
using StageLink.Protocol;
using StageLink.Web.Speakers;

namespace StageLink.Web.Tests.Speakers;

public class SpeakerConnectionTests : IDisposable
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(2);

    // Safety net so a broken test fails instead of hanging.
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
    private readonly TestSimulator _simulator = new();

    public void Dispose()
    {
        _simulator.Dispose();
        _timeout.Dispose();
    }

    private int StartSimulator()
    {
        return _simulator.Port;
    }

    private Task<SpeakerConnection> ConnectAsync(int port, TimeSpan? replyTimeout = null)
    {
        return SpeakerConnection.ConnectAsync("127.0.0.1", port, replyTimeout ?? ReplyTimeout, _timeout.Token);
    }

    // With the real simulator

    [Fact]
    public async Task SendAsync_Ping_ReturnsPong()
    {
        await using SpeakerConnection connection = await ConnectAsync(StartSimulator());

        Frame reply = await connection.SendAsync(Command.Ping, [], _timeout.Token);

        Assert.Equal(Command.Pong, reply.Command);
    }

    [Fact]
    public async Task SendAsync_ManyAtOnce_EachGetsItsOwnReply()
    {
        await using SpeakerConnection connection = await ConnectAsync(StartSimulator());

        Frame[] replies = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => connection.SendAsync(Command.Ping, [], _timeout.Token)));

        Assert.All(replies, reply => Assert.Equal(Command.Pong, reply.Command));
        Assert.Equal(20, replies.Select(reply => reply.MessageNumber).Distinct().Count());
    }

    [Fact]
    public async Task Telemetry_IsDeliveredToTheEvent_NotToRequests()
    {
        await using SpeakerConnection connection = await ConnectAsync(StartSimulator());
        var twoReceived = new TaskCompletionSource();
        int count = 0;
        connection.TelemetryReceived += _ =>
        {
            if (Interlocked.Increment(ref count) == 2)
            {
                twoReceived.TrySetResult();
            }
        };

        var interval = new ByteWriter();
        interval.WriteUInt16(100);
        Frame ack = await connection.SendAsync(Command.Subscribe, interval.ToArray(), _timeout.Token);
        Frame pong = await connection.SendAsync(Command.Ping, [], _timeout.Token);
        await twoReceived.Task.WaitAsync(_timeout.Token);

        Assert.Equal(Command.Ack, ack.Command);
        Assert.Equal(Command.Pong, pong.Command);
    }

    [Fact]
    public async Task ConnectAsync_NoSpeakerListening_Throws()
    {
        int closedPort;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        await Assert.ThrowsAnyAsync<SocketException>(() => ConnectAsync(closedPort));
    }

    // With the fake speaker

    [Fact]
    public async Task RepliesOutOfOrder_AreMatchedByMessageNumber()
    {
        using var fake = new FakeSpeaker();
        Task accept = fake.AcceptAsync(_timeout.Token);
        await using SpeakerConnection connection = await ConnectAsync(fake.Port);
        await accept;

        Task<Frame> first = connection.SendAsync(Command.Ping, [], _timeout.Token);
        Task<Frame> second = connection.SendAsync(Command.GetState, [], _timeout.Token);
        Frame request1 = await fake.ReceiveAsync(_timeout.Token);
        Frame request2 = await fake.ReceiveAsync(_timeout.Token);

        // Answer the second request first.
        await fake.SendAsync(new Frame(Command.State, request2.MessageNumber, [0x00]), _timeout.Token);
        await fake.SendAsync(new Frame(Command.Pong, request1.MessageNumber, []), _timeout.Token);

        Assert.Equal(Command.Pong, (await first).Command);
        Assert.Equal(Command.State, (await second).Command);
    }

    [Fact]
    public async Task NoReply_ThrowsTimeoutException()
    {
        using var fake = new FakeSpeaker();
        Task accept = fake.AcceptAsync(_timeout.Token);
        await using SpeakerConnection connection = await ConnectAsync(fake.Port, TimeSpan.FromMilliseconds(200));
        await accept;

        // The fake reads the request but never answers.
        await Assert.ThrowsAsync<TimeoutException>(() => connection.SendAsync(Command.Ping, [], _timeout.Token));
    }

    [Fact]
    public async Task SpeakerHangsUp_PendingRequestFails()
    {
        using var fake = new FakeSpeaker();
        Task accept = fake.AcceptAsync(_timeout.Token);
        await using SpeakerConnection connection = await ConnectAsync(fake.Port);
        await accept;

        Task<Frame> pending = connection.SendAsync(Command.Ping, [], _timeout.Token);
        await fake.ReceiveAsync(_timeout.Token);
        fake.HangUp();

        await Assert.ThrowsAsync<IOException>(() => pending);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task SendAsync_AfterSpeakerHungUp_FailsImmediately()
    {
        using var fake = new FakeSpeaker();
        Task accept = fake.AcceptAsync(_timeout.Token);
        await using SpeakerConnection connection = await ConnectAsync(fake.Port);
        await accept;

        fake.HangUp();
        while (connection.IsConnected)
        {
            await Task.Delay(10, _timeout.Token);
        }

        await Assert.ThrowsAsync<IOException>(() => connection.SendAsync(Command.Ping, [], _timeout.Token));
    }
}
