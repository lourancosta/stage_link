using Microsoft.Extensions.Options;
using StageLink.Web.Discovery;
using StageLink.Web.Speakers;

namespace StageLink.Web.Monitoring;

// Follows every discovered speaker for as long as the backend runs:
// - keeps one Telemetry subscription per speaker (reconnecting when needed),
// - records temperature history and events (limiter, faults, settings, renames,
//   going offline and coming back),
// - re-publishes readings and events for the browsers (TelemetryReceived, EventRecorded).
// Being the only subscriber per speaker means nothing else competes for it.
public sealed class SpeakerMonitor(
    SpeakerRegistry registry,
    SpeakerManager manager,
    SpeakerHistory history,
    SpeakerEventLog eventLog,
    TimeProvider time,
    IOptions<MonitoringOptions> monitoringOptions,
    IOptions<SpeakerOptions> speakerOptions,
    ILogger<SpeakerMonitor> logger) : BackgroundService
{
    private readonly TimeSpan _checkInterval = monitoringOptions.Value.CheckInterval;
    private readonly TimeSpan _telemetryInterval = speakerOptions.Value.TelemetryInterval;

    private readonly Dictionary<string, Monitored> _speakers = [];
    private readonly Lock _lock = new();

    // Raised for every reading and every new event (used to push them to browsers).
    public event Action<string, TelemetryReading>? TelemetryReceived;
    public event Action<string, SpeakerEvent>? EventRecorded;

    public bool IsOnline(string speakerId)
    {
        lock (_lock)
        {
            return _speakers.TryGetValue(speakerId, out Monitored? speaker) && speaker.Online;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_checkInterval);
        try
        {
            do
            {
                await CheckAllAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task CheckAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DiscoveredSpeaker> discovered = registry.GetAll();

        foreach (DiscoveredSpeaker speaker in discovered)
        {
            await CheckAsync(speaker, cancellationToken);
        }

        // Speakers that stopped announcing: offline, and no longer followed.
        List<string> gone;
        lock (_lock)
        {
            gone = _speakers.Keys.Where(id => discovered.All(s => s.Id != id)).ToList();
        }

        foreach (string id in gone)
        {
            Monitored speaker = Get(id);
            Detach(speaker);
            if (speaker.Online)
            {
                speaker.Online = false;
                Record(id, EventSeverity.Error, "Went offline");
            }

            lock (_lock)
            {
                _speakers.Remove(id);
            }
        }
    }

    private async Task CheckAsync(DiscoveredSpeaker discovered, CancellationToken cancellationToken)
    {
        string id = discovered.Id;
        Monitored speaker = Get(id);

        if (speaker.Name is not null && speaker.Name != discovered.Name)
        {
            Record(id, EventSeverity.Info, $"Renamed from \"{speaker.Name}\" to \"{discovered.Name}\"");
        }

        speaker.Name = discovered.Name;

        try
        {
            SpeakerClient client = await manager.GetClientAsync(id, cancellationToken);
            if (!ReferenceEquals(client, speaker.Client))
            {
                // New connection (first time, or the manager reconnected): subscribe on it.
                Detach(speaker);
                void Handler(TelemetryReading reading) => OnTelemetry(id, reading);
                client.TelemetryReceived += Handler;
                speaker.Client = client;
                speaker.Handler = Handler;
                await client.SubscribeAsync(_telemetryInterval, cancellationToken);
            }

            if (!speaker.Online)
            {
                speaker.Online = true;
                Record(id, EventSeverity.Info, speaker.WasOnline ? "Back online" : "Connected");
                speaker.WasOnline = true;
            }
        }
        catch (Exception ex) when (ex is SpeakerUnavailableException or SpeakerNotFoundException or TimeoutException
                                       or IOException or SpeakerCommandException)
        {
            Detach(speaker);
            if (speaker.Online || !speaker.ReportedUnreachable)
            {
                speaker.Online = false;
                speaker.ReportedUnreachable = true;
                Record(id, EventSeverity.Error, speaker.WasOnline ? "Went offline" : "Not reachable");
            }

            logger.LogDebug("Speaker {SpeakerId} not reachable: {Reason}", id, ex.Message);
        }
    }

    // Runs on the speaker connection's read loop, about 5 times a second.
    private void OnTelemetry(string id, TelemetryReading reading)
    {
        DateTimeOffset now = time.GetUtcNow();
        TelemetryReading? previous;
        lock (_lock)
        {
            if (!_speakers.TryGetValue(id, out Monitored? speaker))
            {
                return;
            }

            previous = speaker.LastReading;
            speaker.LastReading = reading;
        }

        history.Add(id, now, reading);

        if (previous is not null)
        {
            foreach (SpeakerEvent speakerEvent in EventDetector.Compare(previous, reading, now))
            {
                Record(id, speakerEvent);
            }
        }

        TelemetryReceived?.Invoke(id, reading);
    }

    private void Record(string id, EventSeverity severity, string message)
    {
        Record(id, new SpeakerEvent(time.GetUtcNow(), severity, message));
    }

    private void Record(string id, SpeakerEvent speakerEvent)
    {
        eventLog.Add(id, speakerEvent);
        EventRecorded?.Invoke(id, speakerEvent);
    }

    private Monitored Get(string id)
    {
        lock (_lock)
        {
            if (!_speakers.TryGetValue(id, out Monitored? speaker))
            {
                speaker = new Monitored();
                _speakers[id] = speaker;
            }

            return speaker;
        }
    }

    private static void Detach(Monitored speaker)
    {
        if (speaker.Client is not null && speaker.Handler is not null)
        {
            speaker.Client.TelemetryReceived -= speaker.Handler;
        }

        speaker.Client = null;
        speaker.Handler = null;
    }

    private sealed class Monitored
    {
        public SpeakerClient? Client { get; set; }
        public Action<TelemetryReading>? Handler { get; set; }
        public TelemetryReading? LastReading { get; set; }
        public string? Name { get; set; }
        public bool Online { get; set; }
        public bool WasOnline { get; set; }
        public bool ReportedUnreachable { get; set; }
    }
}
