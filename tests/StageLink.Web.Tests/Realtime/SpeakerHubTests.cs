using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StageLink.Web.Discovery;
using StageLink.Web.Tests.Speakers;

namespace StageLink.Web.Tests.Realtime;

// Integration tests: a real SignalR client talking to the in-memory backend,
// which talks to a real simulator over TCP.
public class SpeakerHubTests : IAsyncLifetime
{
    private const string SpeakerId = "SL-TEST";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly List<TestSimulator> _simulators = [];
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));
    private HubConnection _hub = null!;

    public SpeakerHubTests()
    {
        // Faster than production so tests stay quick.
        _factory = TestApp.WithDiscoveryPort(0).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Discovery:ListCheckInterval", "00:00:00.100");
            builder.UseSetting("Speakers:TelemetryInterval", "00:00:00.100");
        });
    }

    public Task InitializeAsync()
    {
        _hub = CreateHubConnection();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _hub.DisposeAsync();
        await _factory.DisposeAsync();
        _simulators.ForEach(simulator => simulator.Dispose());
        _timeout.Dispose();
    }

    // Connects through the in-memory test server (no real port).
    private HubConnection CreateHubConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/speakers"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    private void Register(int tcpPort, string name = "Main L")
    {
        _factory.Services.GetRequiredService<SpeakerRegistry>().Update(
            new DiscoveredSpeaker(SpeakerId, name, "StageLink Sim", "1.0.0", "127.0.0.1", (ushort)tcpPort, DateTimeOffset.UtcNow));
    }

    private TestSimulator AddSpeaker()
    {
        var simulator = new TestSimulator();
        _simulators.Add(simulator);
        Register(simulator.Port);
        return simulator;
    }

    private sealed record SpeakerDto(string Id, string Name);

    private sealed record TelemetryDto(double Temperature, double Level, bool LimiterActive, double Gain, bool Muted, double Tilt, string Faults);

    // Speaker list

    [Fact]
    public async Task OnConnect_SendsCurrentSpeakerList()
    {
        Register(51000, "Main L");
        var received = new TaskCompletionSource<List<SpeakerDto>>();
        _hub.On<List<SpeakerDto>>("SpeakersChanged", list => received.TrySetResult(list));

        await _hub.StartAsync(_timeout.Token);
        List<SpeakerDto> speakers = await received.Task.WaitAsync(_timeout.Token);

        Assert.Equal("Main L", Assert.Single(speakers).Name);
    }

    [Fact]
    public async Task NewSpeaker_IsPushedToConnectedClients()
    {
        var withSpeaker = new TaskCompletionSource<List<SpeakerDto>>();
        _hub.On<List<SpeakerDto>>("SpeakersChanged", list =>
        {
            if (list.Count > 0)
            {
                withSpeaker.TrySetResult(list);
            }
        });
        await _hub.StartAsync(_timeout.Token);

        Register(51000, "Main R"); // appears after the browser connected

        List<SpeakerDto> speakers = await withSpeaker.Task.WaitAsync(_timeout.Token);
        Assert.Equal(SpeakerId, Assert.Single(speakers).Id);
    }

    // Telemetry

    [Fact]
    public async Task WatchSpeaker_ReceivesLiveTelemetry()
    {
        TestSimulator simulator = AddSpeaker();
        simulator.State.SetGain(-6.5);
        var received = new TaskCompletionSource<(string Id, TelemetryDto Reading)>();
        _hub.On<string, TelemetryDto>("Telemetry", (id, reading) => received.TrySetResult((id, reading)));
        await _hub.StartAsync(_timeout.Token);

        await _hub.InvokeAsync("WatchSpeaker", SpeakerId, _timeout.Token);
        (string id, TelemetryDto reading) = await received.Task.WaitAsync(_timeout.Token);

        Assert.Equal(SpeakerId, id);
        Assert.Equal(-6.5, reading.Gain);
        Assert.Equal("None", reading.Faults); // enums are sent as names
    }

    [Fact]
    public async Task WatchUnknownSpeaker_ReturnsClearError()
    {
        await _hub.StartAsync(_timeout.Token);

        HubException ex = await Assert.ThrowsAsync<HubException>(
            () => _hub.InvokeAsync("WatchSpeaker", "SL-NOPE", _timeout.Token));

        Assert.Contains("SL-NOPE", ex.Message);
    }

    [Fact]
    public async Task UnwatchSpeaker_StopsTelemetry()
    {
        AddSpeaker();
        int count = 0;
        var first = new TaskCompletionSource();
        _hub.On<string, TelemetryDto>("Telemetry", (_, _) =>
        {
            Interlocked.Increment(ref count);
            first.TrySetResult();
        });
        await _hub.StartAsync(_timeout.Token);
        await _hub.InvokeAsync("WatchSpeaker", SpeakerId, _timeout.Token);
        await first.Task.WaitAsync(_timeout.Token);

        await _hub.InvokeAsync("UnwatchSpeaker", SpeakerId, _timeout.Token);
        await Task.Delay(150, _timeout.Token); // let anything already in flight arrive
        int afterUnwatch = Volatile.Read(ref count);
        await Task.Delay(500, _timeout.Token); // 5 Telemetry intervals

        Assert.Equal(afterUnwatch, Volatile.Read(ref count));
    }
}
