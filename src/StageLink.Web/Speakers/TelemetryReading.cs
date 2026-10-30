using StageLink.Protocol;

namespace StageLink.Web.Speakers;

// One Telemetry update: live measurements plus the current settings.
public record TelemetryReading(
    double Temperature,
    double Level,
    bool LimiterActive,
    double Gain,
    bool Muted,
    double Tilt,
    Faults Faults)
{
    // Telemetry data: temperature (2), level (2), limiter (1), gain (2), mute (1), tilt (2), faults (1)
    public static TelemetryReading FromPayload(byte[] payload)
    {
        var reader = new ByteReader(payload);
        return new TelemetryReading(
            Temperature: reader.ReadFixedPoint(),
            Level: reader.ReadFixedPoint(),
            LimiterActive: reader.ReadByte() == 1,
            Gain: reader.ReadFixedPoint(),
            Muted: reader.ReadByte() == 1,
            Tilt: reader.ReadFixedPoint(),
            Faults: (Faults)reader.ReadByte());
    }
}
