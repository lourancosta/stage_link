using System.Net;
using StageLink.Simulator;

namespace StageLink.Web.Tests.Speakers;

// The real Phase 2 simulator on a free localhost port, stopped on Dispose.
public sealed class TestSimulator : IDisposable
{
    private readonly CancellationTokenSource _stop = new();

    public TestSimulator(string name = "Main L")
    {
        State = new SpeakerState(name);
        var server = new SpeakerServer(new Speaker(State), IPAddress.Loopback, 0);
        server.Start();
        _ = server.RunAsync(_stop.Token);
        Port = server.Port;
    }

    // The simulator's state, so tests can set it up and check the effects of commands.
    public SpeakerState State { get; }

    public int Port { get; }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
