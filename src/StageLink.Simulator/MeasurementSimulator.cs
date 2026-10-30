using StageLink.Protocol;

namespace StageLink.Simulator;

// Makes the speaker's measurements behave like real hardware:
// level follows gain, the limiter catches loud signals, temperature rises and
// falls slowly, and an overtemperature fault appears when it gets too hot.
public class MeasurementSimulator
{
    public const double AmbientTemperature = 25.0;
    public const double OvertemperatureOn = 70.0;
    public const double OvertemperatureOff = 65.0; // lower than On (hysteresis), so the fault doesn't flicker

    private const double SilenceLevel = -60.0;
    private const double ProgramHeadroom = 10.0; // music sits about 10 dB below the gain setting
    private const double LimiterThreshold = -1.0;
    private const double NoiseRange = 1.5;       // random variation of the level, ± dB
    private const double HeatPerDb = 0.8;        // °C above ambient for each dB above silence
    private static readonly TimeSpan ThermalTimeConstant = TimeSpan.FromSeconds(30);

    private readonly SpeakerState _state;
    private readonly Func<double> _noise;

    // noise returns a value from -1 to 1. Tests pass a fixed value to get exact results.
    public MeasurementSimulator(SpeakerState state, Func<double>? noise = null)
    {
        _state = state;
        _noise = noise ?? (() => (Random.Shared.NextDouble() * 2) - 1);
    }

    // Advances the simulation by the given amount of time.
    public void Tick(TimeSpan elapsed)
    {
        double level;
        bool limiterActive;

        if (_state.Muted)
        {
            level = SilenceLevel;
            limiterActive = false;
        }
        else
        {
            double unlimited = _state.Gain - ProgramHeadroom + (_noise() * NoiseRange);
            limiterActive = unlimited > LimiterThreshold;
            level = Math.Clamp(unlimited, SilenceLevel, LimiterThreshold);
        }

        // Move part of the way toward the target temperature. After one time
        // constant it has covered about 63% of the gap (exponential approach).
        double targetTemperature = AmbientTemperature + ((level - SilenceLevel) * HeatPerDb);
        double fraction = 1 - Math.Exp(-(elapsed / ThermalTimeConstant));
        double temperature = _state.Temperature + ((targetTemperature - _state.Temperature) * fraction);

        Faults faults = _state.Faults;
        if (temperature > OvertemperatureOn)
        {
            faults |= Faults.Overtemperature;  // turn the bit on
        }
        else if (temperature < OvertemperatureOff)
        {
            faults &= ~Faults.Overtemperature; // turn the bit off, keep the others
        }

        _state.UpdateMeasurements(temperature, level, limiterActive, faults);
    }
}
