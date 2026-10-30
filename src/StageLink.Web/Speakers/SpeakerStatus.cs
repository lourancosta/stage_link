using StageLink.Protocol;

namespace StageLink.Web.Speakers;

// A speaker's settings, from its State reply.
public record SpeakerStatus(string Name, double Gain, bool Muted, double Tilt)
{
    // State data: name (length + ASCII), gain (2), mute (1), tilt (2)
    public static SpeakerStatus FromPayload(byte[] payload)
    {
        var reader = new ByteReader(payload);
        return new SpeakerStatus(
            Name: reader.ReadString(),
            Gain: reader.ReadFixedPoint(),
            Muted: reader.ReadByte() == 1,
            Tilt: reader.ReadFixedPoint());
    }
}
