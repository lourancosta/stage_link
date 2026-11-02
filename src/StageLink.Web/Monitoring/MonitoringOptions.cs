namespace StageLink.Web.Monitoring;

// Settings for the speaker monitor, read from the "Monitoring" section of appsettings.json.
public class MonitoringOptions
{
    // How often every discovered speaker is checked (connected, online, renamed).
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(1);

    // How much temperature history to keep per speaker.
    public TimeSpan HistoryLength { get; set; } = TimeSpan.FromMinutes(10);

    // Telemetry arrives 5 times a second; history keeps one sample per interval.
    public TimeSpan HistorySampleInterval { get; set; } = TimeSpan.FromSeconds(1);

    // The event log keeps this many recent events per speaker.
    public int MaxEventsPerSpeaker { get; set; } = 200;
}
