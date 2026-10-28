using System.Text;

namespace StageLink.Simulator;

public class SpeakerState
{
    public const double MinGain = -60.0;
    public const double MaxGain = 12.0;
    public const double MinTilt = -15.0;
    public const double MaxTilt = 15.0;

    public string Name { get; }
    public double Gain { get; private set; }
    public bool Muted { get; private set; }
    public double Tilt { get; private set; }

    // The name is sent in the State reply as 1 length byte + ASCII characters,
    // so it must fit those rules.
    public SpeakerState(string name)
    {
        if (name.Length == 0 || name.Length > byte.MaxValue)
        {
            throw new ArgumentException("Name must be 1 to 255 characters long.", nameof(name));
        }

        if (!Ascii.IsValid(name))
        {
            throw new ArgumentException("Name must contain only ASCII characters.", nameof(name));
        }

        Name = name;
    }

    public void SetGain(double gain)
    {
        if (gain < MinGain || gain > MaxGain)
        {
            throw new ArgumentOutOfRangeException(nameof(gain), gain, $"Gain must be between {MinGain} and {MaxGain} dB.");
        }

        Gain = gain;
    }

    public void SetMute(bool muted)
    {
        Muted = muted;
    }

    public void SetTilt(double tilt)
    {
        if (tilt < MinTilt || tilt > MaxTilt)
        {
            throw new ArgumentOutOfRangeException(nameof(tilt), tilt, $"Tilt must be between {MinTilt} and {MaxTilt} degrees.");
        }

        Tilt = tilt;
    }
}