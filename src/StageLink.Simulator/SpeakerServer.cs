using System.Net;
using System.Net.Sockets;
using StageLink.Protocol;

namespace StageLink.Simulator;

// Accepts TCP connections and answers every frame with the speaker's reply.
// Each connected app is served independently, so several can be connected at once.
public class SpeakerServer
{
    private readonly Speaker _speaker;
    private readonly TcpListener _listener;
    private readonly TextWriter _log;

    // Several connections can send commands at the same moment, but SpeakerState
    // is not thread-safe, so only one request is handled at a time.
    private readonly Lock _speakerLock = new();

    public SpeakerServer(Speaker speaker, IPAddress address, int port, TextWriter? log = null)
    {
        _speaker = speaker;
        _listener = new TcpListener(address, port);
        _log = log ?? TextWriter.Null;
    }

    // The port actually in use (useful when port 0 asked the OS to pick one).
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
    }

    // Accepts connections until the token is cancelled. Call Start() first.
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);

                // Don't await: serve this client in the background and go straight
                // back to accepting the next one.
                _ = ServeClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            EndPoint? remote = client.Client.RemoteEndPoint;
            _log.WriteLine($"Connected: {remote}");

            NetworkStream stream = client.GetStream();
            var reader = new FrameReader(stream);
            var session = new Session();

            try
            {
                while (true)
                {
                    Frame? request;
                    try
                    {
                        request = await reader.ReadFrameAsync(cancellationToken);
                    }
                    catch (InvalidFrameException ex)
                    {
                        // A damaged frame was read completely, so the stream is still in
                        // step: skip it and keep going.
                        _log.WriteLine($"Skipped invalid frame from {remote}: {ex.Message}");
                        continue;
                    }

                    if (request is null)
                    {
                        break; // The app closed the connection.
                    }

                    Frame reply;
                    lock (_speakerLock)
                    {
                        reply = _speaker.Handle(request, session);
                    }

                    _log.WriteLine($"{request.Command} #{request.MessageNumber} -> {reply.Command}");
                    await stream.WriteAsync(reply.Encode(), cancellationToken);
                }
            }
            catch (Exception ex) when (ex is IOException or EndOfStreamException or OperationCanceledException)
            {
                // Connection dropped, or the server is shutting down.
            }

            _log.WriteLine($"Disconnected: {remote}");
        }
    }
}
