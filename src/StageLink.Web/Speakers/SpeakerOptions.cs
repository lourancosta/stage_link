namespace StageLink.Web.Speakers;

// Settings for talking to speakers, read from the "Speakers" section of appsettings.json.
public class SpeakerOptions
{
    // How long to wait for a TCP connection before giving up.
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);

    // How long to wait for the reply to each command.
    public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromSeconds(2);
}
