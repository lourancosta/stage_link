namespace StageLink.Simulator;

// Fixed facts about a speaker, sent in every Announce.
// A record: immutable, compared by value, with a one-line declaration.
public record SpeakerIdentity(string Id, string Model, string Firmware)
{
    public static SpeakerIdentity Default { get; } = new("SL-0001", "StageLink Sim", "1.0.0");
}
