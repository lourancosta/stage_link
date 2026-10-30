namespace StageLink.Web.Speakers;

// The speaker was discovered, but the backend couldn't connect to it.
public class SpeakerUnavailableException(string id, Exception innerException)
    : Exception($"Speaker '{id}' could not be reached: {innerException.Message}", innerException)
{
    public string Id { get; } = id;
}
