using StageLink.Protocol;
using StageLink.Simulator;

namespace StageLink.Simulator.Tests;

public class MeasurementSimulatorTests
{
    private readonly SpeakerState _state = new("Main L");

    // No random variation, so results are exact.
    private MeasurementSimulator NoNoise()
    {
        return new MeasurementSimulator(_state, () => 0.0);
    }

    // Level

    [Fact]
    public void Tick_Muted_LevelIsSilenceAndLimiterOff()
    {
        _state.SetGain(12.0);
        _state.SetMute(true);

        NoNoise().Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(-60.0, _state.Level);
        Assert.False(_state.LimiterActive);
    }

    [Fact]
    public void Tick_LevelIsGainMinus10dB()
    {
        _state.SetGain(0.0);

        NoNoise().Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(-10.0, _state.Level);
        Assert.False(_state.LimiterActive);
    }

    [Fact]
    public void Tick_AddsRandomVariationToLevel()
    {
        // Noise at its maximum (+1) adds 1.5 dB
        var simulator = new MeasurementSimulator(_state, () => 1.0);

        simulator.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(-8.5, _state.Level);
    }

    [Fact]
    public void Tick_HighGain_LimiterHoldsLevelAtMinus1dB()
    {
        _state.SetGain(12.0); // would be +2 dB without the limiter

        NoNoise().Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(-1.0, _state.Level);
        Assert.True(_state.LimiterActive);
    }

    // Temperature

    [Fact]
    public void Tick_TemperatureRisesGraduallyTowardTarget()
    {
        // Level -10 dB → target 25 + 50 × 0.8 = 65 °C.
        // After one time constant (30 s) it covers 63.2% of the gap: 25 + 40 × 0.632 = 50.28
        _state.SetGain(0.0);

        NoNoise().Tick(TimeSpan.FromSeconds(30));

        Assert.Equal(50.28, _state.Temperature, 2);
    }

    [Fact]
    public void Tick_TemperatureCoolsWhenMuted()
    {
        // Muted → target 25 °C. From 60: 60 - 35 × 0.632 = 37.88
        _state.UpdateMeasurements(60.0, -60.0, false, Faults.None);
        _state.SetMute(true);

        NoNoise().Tick(TimeSpan.FromSeconds(30));

        Assert.Equal(37.88, _state.Temperature, 2);
    }

    // Overtemperature fault (on above 70 °C, off below 65 °C)

    [Fact]
    public void Tick_Above70Degrees_SetsOvertemperature()
    {
        _state.UpdateMeasurements(71.0, -60.0, false, Faults.None);

        NoNoise().Tick(TimeSpan.Zero);

        Assert.True(_state.Faults.HasFlag(Faults.Overtemperature));
    }

    [Fact]
    public void Tick_Between65And70_KeepsOvertemperatureOn()
    {
        _state.UpdateMeasurements(68.0, -60.0, false, Faults.Overtemperature);

        NoNoise().Tick(TimeSpan.Zero);

        Assert.True(_state.Faults.HasFlag(Faults.Overtemperature));
    }

    [Fact]
    public void Tick_Below65Degrees_ClearsOvertemperature()
    {
        _state.UpdateMeasurements(64.0, -60.0, false, Faults.Overtemperature);

        NoNoise().Tick(TimeSpan.Zero);

        Assert.False(_state.Faults.HasFlag(Faults.Overtemperature));
    }

    [Fact]
    public void Tick_KeepsOtherFaults()
    {
        _state.UpdateMeasurements(71.0, -60.0, false, Faults.AmplifierFault);

        NoNoise().Tick(TimeSpan.Zero);

        Assert.Equal(Faults.AmplifierFault | Faults.Overtemperature, _state.Faults);
    }
}
