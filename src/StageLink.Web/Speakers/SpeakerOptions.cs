namespace StageLink.Web.Speakers;

// Settings for talking to speakers, read from the "Speakers" section of appsettings.json.
public class SpeakerOptions
{
    // How long to wait for a TCP connection before giving up.
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);

    // How long to wait for the reply to each command.
    public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromSeconds(2);

    // How often a watched speaker sends Telemetry (5 times a second).
    public TimeSpan TelemetryInterval { get; set; } = TimeSpan.FromMilliseconds(200);
}
