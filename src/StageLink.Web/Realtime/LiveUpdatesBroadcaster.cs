using Microsoft.AspNetCore.SignalR;
using StageLink.Web.Monitoring;
using StageLink.Web.Speakers;

namespace StageLink.Web.Realtime;

// Relays the monitor's readings and events to the browsers watching each speaker
// (one SignalR group per speaker).
public sealed class LiveUpdatesBroadcaster(SpeakerMonitor monitor, IHubContext<SpeakerHub, ISpeakerHubClient> hub)
    : IHostedService
{
    public static string GroupName(string speakerId)
    {
        return $"speaker:{speakerId}";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        monitor.TelemetryReceived += OnTelemetry;
        monitor.EventRecorded += OnEvent;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        monitor.TelemetryReceived -= OnTelemetry;
        monitor.EventRecorded -= OnEvent;
        return Task.CompletedTask;
    }

    // Called from speaker read loops: send without waiting for the browsers.
    private void OnTelemetry(string speakerId, TelemetryReading reading)
    {
        _ = hub.Clients.Group(GroupName(speakerId)).Telemetry(speakerId, reading);
    }

    private void OnEvent(string speakerId, SpeakerEvent speakerEvent)
    {
        _ = hub.Clients.Group(GroupName(speakerId)).EventRecorded(speakerId, speakerEvent);
    }
}
