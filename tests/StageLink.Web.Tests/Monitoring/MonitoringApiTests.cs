using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StageLink.Web.Discovery;
using StageLink.Web.Tests.Speakers;

namespace StageLink.Web.Tests.Monitoring;

// Integration tests: the monitor follows a real simulator; results are read over HTTP.
public class MonitoringApiTests : IDisposable
{
    private const string SpeakerId = "SL-TEST";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly List<TestSimulator> _simulators = [];

    public MonitoringApiTests()
    {
        // Faster than production so tests stay quick.
        _factory = TestApp.WithDiscoveryPort(0).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Monitoring:CheckInterval", "00:00:00.100");
            builder.UseSetting("Monitoring:HistorySampleInterval", "00:00:00.100");
            builder.UseSetting("Speakers:TelemetryInterval", "00:00:00.100");
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _simulators.ForEach(simulator => simulator.Dispose());
    }

    private TestSimulator AddSpeaker(string name = "Main L")
    {
        var simulator = new TestSimulator(name);
        _simulators.Add(simulator);
        _factory.Services.GetRequiredService<SpeakerRegistry>().Update(
            new DiscoveredSpeaker(SpeakerId, name, "StageLink Sim", "1.0.0", "127.0.0.1", (ushort)simulator.Port, DateTimeOffset.UtcNow));
        return simulator;
    }

    private sealed record SampleDto(DateTimeOffset Time, double Temperature, double Level);

    private sealed record EventDto(DateTimeOffset Time, string Severity, string Message);

    // Polls until the condition holds (the monitor works in the background).
    private async Task<T> WaitForAsync<T>(Func<Task<T>> read, Func<T, bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (true)
        {
            T value = await read();
            if (condition(value))
            {
                return value;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private async Task<List<EventDto>> EventsAsync()
    {
        return await _client.GetFromJsonAsync<List<EventDto>>($"/api/speakers/{SpeakerId}/events") ?? [];
    }

    private async Task<List<SampleDto>> HistoryAsync()
    {
        return await _client.GetFromJsonAsync<List<SampleDto>>($"/api/speakers/{SpeakerId}/history") ?? [];
    }

    private Task<List<EventDto>> WaitForEventAsync(string message)
    {
        return WaitForAsync(EventsAsync, events => events.Any(e => e.Message == message));
    }

    [Fact]
    public async Task History_FillsUpWhileTheSpeakerIsOnline()
    {
        AddSpeaker();

        List<SampleDto> history = await WaitForAsync(HistoryAsync, samples => samples.Count >= 2);

        Assert.All(history, sample => Assert.InRange(sample.Temperature, 20, 80));
        Assert.True(history[1].Time > history[0].Time);
    }

    [Fact]
    public async Task Events_StartWithConnected()
    {
        AddSpeaker();

        List<EventDto> events = await WaitForEventAsync("Connected");

        Assert.Equal("Info", events.First(e => e.Message == "Connected").Severity);
    }

    [Fact]
    public async Task Events_RecordSettingChanges()
    {
        AddSpeaker();
        // Changes are found by comparing readings, so wait for a first reading
        // (the first history sample) to compare against.
        await WaitForAsync(HistoryAsync, samples => samples.Count >= 1);

        await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/gain", new { Gain = -6.0 });

        List<EventDto> events = await WaitForEventAsync("Gain changed to -6.0 dB");
        Assert.Equal("Gain changed to -6.0 dB", events[0].Message); // newest first
    }

    [Fact]
    public async Task Events_RecordRenames()
    {
        AddSpeaker("Main L");
        await WaitForEventAsync("Connected");

        await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/name", new { Name = "Main Left" });

        await WaitForEventAsync("Renamed from \"Main L\" to \"Main Left\"");
    }

    [Fact]
    public async Task Rename_UpdatesTheSpeakerListRightAway()
    {
        AddSpeaker("Main L");

        await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/name", new { Name = "Main Left" });

        Assert.Equal("Main Left", _factory.Services.GetRequiredService<SpeakerRegistry>().Find(SpeakerId)?.Name);
    }

    [Fact]
    public async Task Events_RecordWhenTheSpeakerGoesOffline()
    {
        TestSimulator simulator = AddSpeaker();
        await WaitForEventAsync("Connected");

        simulator.Dispose();

        List<EventDto> events = await WaitForEventAsync("Went offline");
        Assert.Equal("Error", events.First(e => e.Message == "Went offline").Severity);
    }

    [Fact]
    public async Task UnknownSpeaker_Returns404()
    {
        HttpResponseMessage history = await _client.GetAsync("/api/speakers/SL-NOPE/history");
        HttpResponseMessage events = await _client.GetAsync("/api/speakers/SL-NOPE/events");

        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, events.StatusCode);
    }
}
