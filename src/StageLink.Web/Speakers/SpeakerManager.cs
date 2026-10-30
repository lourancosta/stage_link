using System.Net.Sockets;
using Microsoft.Extensions.Options;
using StageLink.Web.Discovery;

namespace StageLink.Web.Speakers;

// Keeps one open SpeakerClient per speaker, shared by every HTTP request.
// Connects on first use, using the address from discovery, and reconnects if the
// connection dropped or the speaker came back on a different address or port.
public sealed class SpeakerManager : IAsyncDisposable
{
    private readonly SpeakerRegistry _registry;
    private readonly SpeakerOptions _options;
    private readonly Dictionary<string, Connected> _clients = [];

    // Async lock: connecting involves await, and two requests for a new speaker
    // arriving together must not open two connections.
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SpeakerManager(SpeakerRegistry registry, IOptions<SpeakerOptions> options)
    {
        _registry = registry;
        _options = options.Value;
    }

    // Throws SpeakerNotFoundException if the id isn't currently discovered,
    // and SpeakerUnavailableException if it can't be reached.
    public async Task<SpeakerClient> GetClientAsync(string id, CancellationToken cancellationToken = default)
    {
        DiscoveredSpeaker speaker = _registry.Find(id) ?? throw new SpeakerNotFoundException(id);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_clients.TryGetValue(id, out Connected? existing))
            {
                bool sameEndpoint = existing.Address == speaker.Address && existing.Port == speaker.TcpPort;
                if (sameEndpoint && existing.Client.IsConnected)
                {
                    return existing.Client;
                }

                // Stale: the connection dropped or the speaker moved. Replace it.
                _clients.Remove(id);
                await existing.Client.DisposeAsync();
            }

            SpeakerClient client = await ConnectAsync(speaker, cancellationToken);
            _clients[id] = new Connected(client, speaker.Address, speaker.TcpPort);
            return client;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (Connected connected in _clients.Values)
        {
            await connected.Client.DisposeAsync();
        }

        _clients.Clear();
        _lock.Dispose();
    }

    private async Task<SpeakerClient> ConnectAsync(DiscoveredSpeaker speaker, CancellationToken cancellationToken)
    {
        // Give up after ConnectTimeout, but still stop early if the HTTP request is cancelled.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ConnectTimeout);

        try
        {
            return await SpeakerClient.ConnectAsync(speaker.Address, speaker.TcpPort, _options.ReplyTimeout, timeout.Token);
        }
        catch (SocketException ex)
        {
            throw new SpeakerUnavailableException(speaker.Id, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeakerUnavailableException(speaker.Id, new TimeoutException("Connecting timed out.", ex));
        }
    }

    private sealed record Connected(SpeakerClient Client, string Address, ushort Port);
}
