using StageLink.Protocol;
using StageLink.Web.Speakers;

namespace StageLink.Web.Tests.Speakers;

public class SpeakerClientTests : IDisposable
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
    private readonly TestSimulator _simulator = new("Main L");

    public void Dispose()
    {
        _simulator.Dispose();
        _timeout.Dispose();
    }

    private Task<SpeakerClient> ConnectAsync(int? port = null)
    {
        return SpeakerClient.ConnectAsync("127.0.0.1", port ?? _simulator.Port, ReplyTimeout, _timeout.Token);
    }

    [Fact]
    public async Task PingAsync_Succeeds()
    {
        await using SpeakerClient client = await ConnectAsync();

        await client.PingAsync(_timeout.Token);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentSettings()
    {
        _simulator.State.SetGain(-6.5);
        _simulator.State.SetMute(true);
        _simulator.State.SetTilt(7.5);
        await using SpeakerClient client = await ConnectAsync();

        SpeakerStatus status = await client.GetStatusAsync(_timeout.Token);

        Assert.Equal(new SpeakerStatus("Main L", -6.5, true, 7.5), status);
    }

    [Fact]
    public async Task SetGainAsync_ChangesTheSpeaker()
    {
        await using SpeakerClient client = await ConnectAsync();

        await client.SetGainAsync(-3.25, _timeout.Token);

        Assert.Equal(-3.25, _simulator.State.Gain);
    }

    [Fact]
    public async Task SetMuteAsync_ChangesTheSpeaker()
    {
        await using SpeakerClient client = await ConnectAsync();

        await client.SetMuteAsync(true, _timeout.Token);

        Assert.True(_simulator.State.Muted);
    }

    [Fact]
    public async Task SetTiltAsync_ChangesTheSpeaker()
    {
        await using SpeakerClient client = await ConnectAsync();

        await client.SetTiltAsync(-4.5, _timeout.Token);

        Assert.Equal(-4.5, _simulator.State.Tilt);
    }

    [Fact]
    public async Task SetGainAsync_SpeakerRejects_ThrowsWithNackCode()
    {
        await using SpeakerClient client = await ConnectAsync();

        // 20 dB fits on the wire but is above the speaker's +12 dB maximum.
        SpeakerCommandException ex = await Assert.ThrowsAsync<SpeakerCommandException>(
            () => client.SetGainAsync(20.0, _timeout.Token));

        Assert.Equal(Command.SetGain, ex.Command);
        Assert.Equal(NackCode.ValueOutOfRange, ex.Code);
        Assert.Contains("out of range", ex.Message);
        Assert.Equal(0.0, _simulator.State.Gain);
    }

    [Fact]
    public async Task SetGainAsync_ValueTooLargeForWire_ThrowsBeforeSending()
    {
        await using SpeakerClient client = await ConnectAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SetGainAsync(400.0, _timeout.Token));
        Assert.Equal(0.0, _simulator.State.Gain);
    }

    [Fact]
    public async Task SubscribeAsync_RaisesParsedTelemetry()
    {
        _simulator.State.SetGain(-6.5);
        await using SpeakerClient client = await ConnectAsync();
        var received = new TaskCompletionSource<TelemetryReading>();
        client.TelemetryReceived += reading => received.TrySetResult(reading);

        await client.SubscribeAsync(TimeSpan.FromMilliseconds(100), _timeout.Token);
        TelemetryReading reading = await received.Task.WaitAsync(_timeout.Token);

        Assert.Equal(-6.5, reading.Gain);
        Assert.Equal(25.0, reading.Temperature);
    }

    [Fact]
    public async Task UnsubscribeAsync_Succeeds()
    {
        await using SpeakerClient client = await ConnectAsync();
        await client.SubscribeAsync(TimeSpan.FromMilliseconds(100), _timeout.Token);

        await client.UnsubscribeAsync(_timeout.Token);
    }

    [Fact]
    public async Task UnexpectedReply_ThrowsInvalidDataException()
    {
        using var fake = new FakeSpeaker();
        Task accept = fake.AcceptAsync(_timeout.Token);
        await using SpeakerClient client = await ConnectAsync(fake.Port);
        await accept;

        Task setGain = client.SetGainAsync(1.0, _timeout.Token);
        Frame request = await fake.ReceiveAsync(_timeout.Token);
        await fake.SendAsync(new Frame(Command.Pong, request.MessageNumber, []), _timeout.Token); // Pong instead of Ack

        await Assert.ThrowsAsync<InvalidDataException>(() => setGain);
    }
}
