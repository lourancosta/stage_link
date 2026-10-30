using StageLink.Protocol;
using StageLink.Web.Speakers;

namespace StageLink.Web.Tests.Speakers;

public class SpeakerStatusTests
{
    [Fact]
    public void SpeakerStatus_FromPayload_ReadsAllFields()
    {
        byte[] payload =
        [
            0x06, 0x4D, 0x61, 0x69, 0x6E, 0x20, 0x4C, // name: "Main L"
            0xFD, 0x76,                               // gain: -6.5 dB
            0x01,                                     // mute: on
            0x02, 0xEE,                               // tilt: 7.5 degrees
        ];

        SpeakerStatus status = SpeakerStatus.FromPayload(payload);

        Assert.Equal(new SpeakerStatus("Main L", -6.5, true, 7.5), status);
    }

    [Fact]
    public void TelemetryReading_FromPayload_ReadsAllFields()
    {
        byte[] payload =
        [
            0x10, 0x36, // temperature: 41.5 °C
            0xFB, 0x37, // level: -12.25 dB
            0x01,       // limiter: active
            0xFD, 0x76, // gain: -6.5 dB
            0x01,       // mute: on
            0x02, 0xEE, // tilt: 7.5 degrees
            0x05,       // faults: overtemperature + power supply
        ];

        TelemetryReading reading = TelemetryReading.FromPayload(payload);

        Assert.Equal(
            new TelemetryReading(41.5, -12.25, true, -6.5, true, 7.5, Faults.Overtemperature | Faults.PowerSupplyFault),
            reading);
    }
}
