using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StageLink.Web.Discovery;
using StageLink.Web.Tests.Speakers;

namespace StageLink.Web.Tests;

// Integration tests: HTTP in, real TCP to a real simulator, HTTP out.
public class SpeakerApiTests : IDisposable
{
    private const string SpeakerId = "SL-TEST";

    private readonly WebApplicationFactory<Program> _factory = TestApp.WithDiscoveryPort(0);
    private readonly HttpClient _client;
    private readonly List<TestSimulator> _simulators = [];

    public SpeakerApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _simulators.ForEach(simulator => simulator.Dispose());
    }

    // Starts a simulator and puts it in the registry as if it had announced itself.
    private TestSimulator AddSpeaker(string name = "Main L")
    {
        var simulator = new TestSimulator(name);
        _simulators.Add(simulator);
        Register(simulator.Port, name);
        return simulator;
    }

    private void Register(int tcpPort, string name = "Main L")
    {
        var registry = _factory.Services.GetRequiredService<SpeakerRegistry>();
        registry.Update(new DiscoveredSpeaker(SpeakerId, name, "StageLink Sim", "1.0.0", "127.0.0.1", (ushort)tcpPort, DateTimeOffset.UtcNow));
    }

    private sealed record StatusDto(string Name, double Gain, bool Muted, double Tilt);

    // Reading

    [Fact]
    public async Task GetStatus_ReturnsSpeakerSettings()
    {
        TestSimulator simulator = AddSpeaker("Main L");
        simulator.State.SetGain(-6.5);
        simulator.State.SetMute(true);
        simulator.State.SetTilt(7.5);

        StatusDto? status = await _client.GetFromJsonAsync<StatusDto>($"/api/speakers/{SpeakerId}/status");

        Assert.Equal(new StatusDto("Main L", -6.5, true, 7.5), status);
    }

    // Changing settings

    [Fact]
    public async Task PutGain_ChangesTheSpeaker()
    {
        TestSimulator simulator = AddSpeaker();

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/gain", new { Gain = -6.5 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(-6.5, simulator.State.Gain);
    }

    [Fact]
    public async Task PutMute_ChangesTheSpeaker()
    {
        TestSimulator simulator = AddSpeaker();

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/mute", new { Muted = true });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(simulator.State.Muted);
    }

    [Fact]
    public async Task PutTilt_ChangesTheSpeaker()
    {
        TestSimulator simulator = AddSpeaker();

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/tilt", new { Tilt = 7.5 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(7.5, simulator.State.Tilt);
    }

    [Fact]
    public async Task SeveralRequests_AllWork()
    {
        TestSimulator simulator = AddSpeaker();

        await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/gain", new { Gain = -1.0 });
        await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/gain", new { Gain = -2.0 });
        StatusDto? status = await _client.GetFromJsonAsync<StatusDto>($"/api/speakers/{SpeakerId}/status");

        Assert.Equal(-2.0, status?.Gain);
    }

    // Errors

    [Fact]
    public async Task PutGain_SpeakerRejectsValue_Returns422()
    {
        TestSimulator simulator = AddSpeaker();

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/gain", new { Gain = 20.0 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("out of range", problem?.Detail);
        Assert.Equal(0.0, simulator.State.Gain);
    }

    [Fact]
    public async Task PutGain_ValueTooLargeForWire_Returns422()
    {
        AddSpeaker();

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/speakers/{SpeakerId}/gain", new { Gain = 400.0 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task UnknownSpeaker_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/speakers/SL-NOPE/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }

    [Fact]
    public async Task SpeakerNotReachable_Returns503()
    {
        // Announced, but nothing listens on its TCP port.
        int closedPort;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            closedPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        Register(closedPort);

        HttpResponseMessage response = await _client.GetAsync($"/api/speakers/{SpeakerId}/status");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task SpeakerRestartsOnAnotherPort_Reconnects()
    {
        TestSimulator first = AddSpeaker("First");
        Assert.Equal("First", (await _client.GetFromJsonAsync<StatusDto>($"/api/speakers/{SpeakerId}/status"))?.Name);

        first.Dispose();                     // the speaker goes away...
        AddSpeaker("Second");                // ...and comes back on a new port with the same id

        StatusDto? status = await _client.GetFromJsonAsync<StatusDto>($"/api/speakers/{SpeakerId}/status");

        Assert.Equal("Second", status?.Name);
    }
}
