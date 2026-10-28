using StageLink.Simulator;

namespace StageLink.Simulator.Tests;

public class SpeakerStateTests
{
    [Fact]
    public void NewSpeaker_StartsWithDefaults()
    {
        var state = new SpeakerState("Main L");

        Assert.Equal("Main L", state.Name);
        Assert.Equal(0.0, state.Gain);
        Assert.False(state.Muted);
        Assert.Equal(0.0, state.Tilt);
    }

    [Fact]
    public void SetGain_UpdatesGain()
    {
        var state = new SpeakerState("Main L");

        state.SetGain(-6.5);

        Assert.Equal(-6.5, state.Gain);
    }

    [Theory]
    [InlineData(-60.0)]
    [InlineData(12.0)]
    public void SetGain_AcceptsLimits(double gain)
    {
        var state = new SpeakerState("Main L");

        state.SetGain(gain);

        Assert.Equal(gain, state.Gain);
    }

    [Theory]
    [InlineData(-60.01)]
    [InlineData(12.01)]
    public void SetGain_ThrowsWhenOutOfRange(double gain)
    {
        var state = new SpeakerState("Main L");

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetGain(gain));
    }

    [Fact]
    public void SetMute_UpdatesMuted()
    {
        var state = new SpeakerState("Main L");

        state.SetMute(true);

        Assert.True(state.Muted);
    }

    [Fact]
    public void SetTilt_UpdatesTilt()
    {
        var state = new SpeakerState("Main L");

        state.SetTilt(7.5);

        Assert.Equal(7.5, state.Tilt);
    }

    [Theory]
    [InlineData(-15.01)]
    [InlineData(15.01)]
    public void SetTilt_ThrowsWhenOutOfRange(double tilt)
    {
        var state = new SpeakerState("Main L");

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetTilt(tilt));
    }
}