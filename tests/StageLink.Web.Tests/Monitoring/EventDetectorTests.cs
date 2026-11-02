using StageLink.Protocol;
using StageLink.Web.Monitoring;
using StageLink.Web.Speakers;

namespace StageLink.Web.Tests.Monitoring;

public class EventDetectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);

    private static TelemetryReading Reading(
        bool limiter = false, Faults faults = Faults.None, double gain = 0, bool muted = false, double tilt = 0)
    {
        return new TelemetryReading(30, -20, limiter, gain, muted, tilt, faults);
    }

    private static List<(EventSeverity Severity, string Message)> Compare(TelemetryReading previous, TelemetryReading current)
    {
        return EventDetector.Compare(previous, current, Now).Select(e => (e.Severity, e.Message)).ToList();
    }

    [Fact]
    public void NothingChanged_NoEvents()
    {
        Assert.Empty(Compare(Reading(), Reading()));
    }

    [Fact]
    public void LimiterEngaged_IsAWarning()
    {
        Assert.Equal([(EventSeverity.Warning, "Limiter engaged")], Compare(Reading(limiter: false), Reading(limiter: true)));
    }

    [Fact]
    public void LimiterReleased_IsInfo()
    {
        Assert.Equal([(EventSeverity.Info, "Limiter released")], Compare(Reading(limiter: true), Reading(limiter: false)));
    }

    [Fact]
    public void FaultDetected_IsAnError_AndClearedIsInfo()
    {
        Assert.Equal([(EventSeverity.Error, "Overtemperature detected")],
            Compare(Reading(), Reading(faults: Faults.Overtemperature)));
        Assert.Equal([(EventSeverity.Info, "Overtemperature cleared")],
            Compare(Reading(faults: Faults.Overtemperature), Reading()));
    }

    [Fact]
    public void TwoFaultsAtOnce_GiveTwoEvents()
    {
        var events = Compare(Reading(), Reading(faults: Faults.AmplifierFault | Faults.PowerSupplyFault));

        Assert.Equal(
            [(EventSeverity.Error, "Amplifier fault detected"), (EventSeverity.Error, "Power supply fault detected")],
            events);
    }

    [Theory]
    [InlineData(-6.0, "Gain changed to -6.0 dB")]
    [InlineData(12.0, "Gain changed to +12.0 dB")]
    [InlineData(0.0, "Gain changed to 0.0 dB")]
    public void GainChanged_ShowsTheNewValue(double gain, string expected)
    {
        Assert.Equal([(EventSeverity.Info, expected)], Compare(Reading(gain: 3), Reading(gain: gain)));
    }

    [Fact]
    public void MuteAndUnmute()
    {
        Assert.Equal([(EventSeverity.Info, "Muted")], Compare(Reading(muted: false), Reading(muted: true)));
        Assert.Equal([(EventSeverity.Info, "Unmuted")], Compare(Reading(muted: true), Reading(muted: false)));
    }

    [Fact]
    public void TiltChanged_ShowsTheNewValue()
    {
        Assert.Equal([(EventSeverity.Info, "Tilt changed to +2.5°")], Compare(Reading(tilt: 0), Reading(tilt: 2.5)));
    }

    [Fact]
    public void SettingChanges_ComeBeforeWhatTheyCause()
    {
        // Raising the gain engages the limiter in the same reading: cause first, then effect.
        var events = Compare(Reading(gain: 0, limiter: false), Reading(gain: 12, limiter: true));

        Assert.Equal(
            [(EventSeverity.Info, "Gain changed to +12.0 dB"), (EventSeverity.Warning, "Limiter engaged")],
            events);
    }

    [Fact]
    public void Events_CarryTheGivenTime()
    {
        SpeakerEvent e = Assert.Single(EventDetector.Compare(Reading(muted: false), Reading(muted: true), Now));

        Assert.Equal(Now, e.Time);
    }
}
