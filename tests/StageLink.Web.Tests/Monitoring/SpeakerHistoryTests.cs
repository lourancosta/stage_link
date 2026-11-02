using Microsoft.Extensions.Options;
using StageLink.Protocol;
using StageLink.Web.Monitoring;
using StageLink.Web.Speakers;

namespace StageLink.Web.Tests.Monitoring;

public class SpeakerHistoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 20, 0, 0, TimeSpan.Zero);

    private readonly SpeakerHistory _history = new(Options.Create(new MonitoringOptions
    {
        HistoryLength = TimeSpan.FromMinutes(10),
        HistorySampleInterval = TimeSpan.FromSeconds(1),
    }));

    private static TelemetryReading Reading(double temperature, double level = -20)
    {
        return new TelemetryReading(temperature, level, false, 0, false, 0, Faults.None);
    }

    [Fact]
    public void Get_UnknownSpeaker_IsEmpty()
    {
        Assert.Empty(_history.Get("SL-NOPE"));
    }

    [Fact]
    public void Add_StoresTimeTemperatureAndLevel()
    {
        _history.Add("SL-1", T0, Reading(30.5, -12));

        Assert.Equal([new TemperatureSample(T0, 30.5, -12)], _history.Get("SL-1"));
    }

    [Fact]
    public void Add_KeepsAtMostOneSamplePerInterval()
    {
        // Telemetry arrives 5 times a second; history keeps one per second.
        _history.Add("SL-1", T0, Reading(30));
        _history.Add("SL-1", T0.AddMilliseconds(200), Reading(31));
        _history.Add("SL-1", T0.AddMilliseconds(1000), Reading(32));

        Assert.Equal([30.0, 32.0], _history.Get("SL-1").Select(s => s.Temperature));
    }

    [Fact]
    public void Add_DropsSamplesOlderThanTheHistoryLength()
    {
        _history.Add("SL-1", T0, Reading(30));
        _history.Add("SL-1", T0.AddMinutes(5), Reading(31));
        _history.Add("SL-1", T0.AddMinutes(10).AddSeconds(1), Reading(32));

        Assert.Equal([31.0, 32.0], _history.Get("SL-1").Select(s => s.Temperature));
    }

    [Fact]
    public void Speakers_HaveSeparateHistories()
    {
        _history.Add("SL-1", T0, Reading(30));
        _history.Add("SL-2", T0, Reading(40));

        Assert.Equal(30.0, Assert.Single(_history.Get("SL-1")).Temperature);
        Assert.Equal(40.0, Assert.Single(_history.Get("SL-2")).Temperature);
    }
}
