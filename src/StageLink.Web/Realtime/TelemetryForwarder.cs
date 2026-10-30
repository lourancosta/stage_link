using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StageLink.Web.Speakers;

namespace StageLink.Web.Realtime;

// Forwards each speaker's Telemetry to the browsers watching it.
// Subscribes to a speaker when its first watcher arrives and unsubscribes when the
// last one leaves, so speakers don't stream data nobody is looking at.
// Every few seconds it re-checks, so Telemetry resumes after a speaker reconnects.
public sealed class TelemetryForwarder(
    SpeakerManager manager,
    IHubContext<SpeakerHub, ISpeakerHubClient> hub,
    IOptions<SpeakerOptions> options,
    ILogger<TelemetryForwarder> logger) : BackgroundService
{
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(2);

    private readonly TimeSpan _telemetryInterval = options.Value.TelemetryInterval;

    // Which connections watch which speaker.
    private readonly Dictionary<string, HashSet<string>> _watchers = [];

    // The client currently subscribed for each speaker, and our handler on it.
    private readonly Dictionary<string, Subscription> _subscriptions = [];

    private readonly SemaphoreSlim _lock = new(1, 1);

    public static string GroupName(string speakerId)
    {
        return $"speaker:{speakerId}";
    }

    public async Task WatchAsync(string connectionId, string speakerId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await EnsureSubscribedAsync(speakerId, cancellationToken);

            if (!_watchers.TryGetValue(speakerId, out HashSet<string>? connections))
            {
                connections = [];
                _watchers[speakerId] = connections;
            }

            connections.Add(connectionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UnwatchAsync(string connectionId, string speakerId)
    {
        await _lock.WaitAsync();
        try
        {
            await RemoveWatcherAsync(connectionId, speakerId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (string speakerId in _watchers.Keys.ToList())
            {
                await RemoveWatcherAsync(connectionId, speakerId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RecheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RecheckAllAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    // Re-subscribes watched speakers whose connection was replaced.
    private async Task RecheckAllAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            foreach (string speakerId in _watchers.Keys)
            {
                try
                {
                    await EnsureSubscribedAsync(speakerId, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Speaker offline for now; try again next time.
                    logger.LogDebug("Could not resubscribe to {SpeakerId}: {Reason}", speakerId, ex.Message);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // Call with _lock held.
    private async Task EnsureSubscribedAsync(string speakerId, CancellationToken cancellationToken)
    {
        SpeakerClient client = await manager.GetClientAsync(speakerId, cancellationToken);

        if (_subscriptions.TryGetValue(speakerId, out Subscription? current))
        {
            if (ReferenceEquals(current.Client, client))
            {
                return; // Already subscribed on this connection.
            }

            current.Client.TelemetryReceived -= current.Handler; // old, replaced connection
        }

        // Runs on the speaker connection's read loop, so don't wait for the browser send.
        void Handler(TelemetryReading reading) => _ = hub.Clients.Group(GroupName(speakerId)).Telemetry(speakerId, reading);

        client.TelemetryReceived += Handler;
        try
        {
            await client.SubscribeAsync(_telemetryInterval, cancellationToken);
        }
        catch
        {
            client.TelemetryReceived -= Handler;
            _subscriptions.Remove(speakerId);
            throw;
        }

        _subscriptions[speakerId] = new Subscription(client, Handler);
    }

    // Call with _lock held.
    private async Task RemoveWatcherAsync(string connectionId, string speakerId)
    {
        if (!_watchers.TryGetValue(speakerId, out HashSet<string>? connections) || !connections.Remove(connectionId))
        {
            return;
        }

        if (connections.Count > 0)
        {
            return; // Others are still watching.
        }

        _watchers.Remove(speakerId);

        if (_subscriptions.Remove(speakerId, out Subscription? subscription))
        {
            subscription.Client.TelemetryReceived -= subscription.Handler;
            try
            {
                await subscription.Client.UnsubscribeAsync();
            }
            catch (Exception ex)
            {
                // Best effort: the speaker may already be gone.
                logger.LogDebug("Unsubscribe from {SpeakerId} failed: {Reason}", speakerId, ex.Message);
            }
        }
    }

    private sealed record Subscription(SpeakerClient Client, Action<TelemetryReading> Handler);
}
