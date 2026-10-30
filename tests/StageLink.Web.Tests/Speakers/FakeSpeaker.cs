using System.Net;
using System.Net.Sockets;
using StageLink.Protocol;

namespace StageLink.Web.Tests.Speakers;

// A scriptable TCP "speaker" for behavior the real simulator never shows:
// answering out of order, never answering, hanging up mid-request.
public sealed class FakeSpeaker : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private TcpClient? _client;

    public FakeSpeaker()
    {
        _listener.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public NetworkStream Stream => _client?.GetStream() ?? throw new InvalidOperationException("No client yet.");

    public async Task AcceptAsync(CancellationToken cancellationToken)
    {
        _client = await _listener.AcceptTcpClientAsync(cancellationToken);
    }

    public async Task<Frame> ReceiveAsync(CancellationToken cancellationToken)
    {
        Frame? frame = await new FrameReader(Stream).ReadFrameAsync(cancellationToken);
        return frame ?? throw new EndOfStreamException("Client disconnected.");
    }

    public async Task SendAsync(Frame frame, CancellationToken cancellationToken)
    {
        await Stream.WriteAsync(frame.Encode(), cancellationToken);
    }

    public void HangUp()
    {
        _client?.Close();
    }

    public void Dispose()
    {
        _client?.Dispose();
        _listener.Stop();
        _listener.Dispose();
    }
}
