namespace StageLink.Web.Speakers;

// No speaker with this id has announced itself recently.
public class SpeakerNotFoundException(string id)
    : Exception($"No speaker with id '{id}' has been discovered.")
{
    public string Id { get; } = id;
}
