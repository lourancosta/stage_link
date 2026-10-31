import { TestBed } from '@angular/core/testing';
import { FakeHubConnection } from '../testing/fake-hub-connection';
import { HUB_CONNECTION_FACTORY } from './hub-connection';
import { SpeakerHub } from './speaker-hub';
import { DiscoveredSpeaker, TelemetryReading } from './speaker.models';

const mainL: DiscoveredSpeaker = {
  id: 'SL-51000',
  name: 'Main L',
  model: 'StageLink Sim',
  firmware: '1.0.0',
  address: '127.0.0.1',
  tcpPort: 51000,
  lastSeen: '2026-10-30T12:00:00Z',
};

const reading: TelemetryReading = {
  temperature: 41.5,
  level: -12.25,
  limiterActive: true,
  gain: -6.5,
  muted: false,
  tilt: 0,
  faults: 'None',
};

describe('SpeakerHub', () => {
  let connection: FakeHubConnection;
  let hub: SpeakerHub;

  beforeEach(() => {
    connection = new FakeHubConnection();
    TestBed.configureTestingModule({
      providers: [{ provide: HUB_CONNECTION_FACTORY, useValue: () => connection }],
    });
    hub = TestBed.inject(SpeakerHub);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts with no speakers and disconnected', () => {
    expect(hub.speakers()).toEqual([]);
    expect(hub.state()).toBe('disconnected');
  });

  it('is connected after start', async () => {
    await hub.start();

    expect(hub.state()).toBe('connected');
  });

  it('only connects once, even if start is called twice', async () => {
    await hub.start();
    await hub.start();

    expect(connection.startCalls).toBe(1);
  });

  it('updates the list when the server sends SpeakersChanged', async () => {
    await hub.start();

    connection.emit('SpeakersChanged', [mainL]);

    expect(hub.speakers()).toEqual([mainL]);
  });

  it('is not loaded until the first list arrives', async () => {
    await hub.start();
    expect(hub.loaded()).toBe(false);

    connection.emit('SpeakersChanged', []);

    expect(hub.loaded()).toBe(true);
  });

  it('finds a speaker by id', async () => {
    await hub.start();
    connection.emit('SpeakersChanged', [mainL]);

    expect(hub.findSpeaker('SL-51000')?.name).toBe('Main L');
    expect(hub.findSpeaker('SL-NOPE')).toBeUndefined();
  });

  it('follows reconnecting and reconnected', async () => {
    await hub.start();

    connection.simulateReconnecting();
    expect(hub.state()).toBe('reconnecting');

    connection.simulateReconnected();
    expect(hub.state()).toBe('connected');
  });

  it('retries 5 seconds after the first connection fails', async () => {
    vi.useFakeTimers();
    connection.failNextStart = true;

    await hub.start();
    expect(hub.state()).toBe('disconnected');

    await vi.advanceTimersByTimeAsync(5000);

    expect(connection.startCalls).toBe(2);
    expect(hub.state()).toBe('connected');
  });

  it('retries after the connection is closed for good', async () => {
    vi.useFakeTimers();
    await hub.start();

    connection.simulateClose();
    expect(hub.state()).toBe('disconnected');

    await vi.advanceTimersByTimeAsync(5000);
    expect(hub.state()).toBe('connected');
  });

  // Telemetry

  it('asks the server to watch a speaker', async () => {
    await hub.start();

    await hub.watch('SL-51000');

    expect(connection.invoked).toContainEqual({ method: 'WatchSpeaker', args: ['SL-51000'] });
  });

  it('watches as soon as it connects if asked before', async () => {
    await hub.watch('SL-51000'); // not connected yet
    expect(connection.invoked).toEqual([]);

    await hub.start();

    expect(connection.invoked).toContainEqual({ method: 'WatchSpeaker', args: ['SL-51000'] });
  });

  it('keeps the latest reading per speaker', async () => {
    await hub.start();

    connection.emit('Telemetry', 'SL-51000', reading);

    expect(hub.telemetryFor('SL-51000')).toEqual(reading);
    expect(hub.telemetryFor('SL-51010')).toBeUndefined();
  });

  it('unwatching tells the server and forgets the reading', async () => {
    await hub.start();
    await hub.watch('SL-51000');
    connection.emit('Telemetry', 'SL-51000', reading);

    await hub.unwatch('SL-51000');

    expect(connection.invoked).toContainEqual({ method: 'UnwatchSpeaker', args: ['SL-51000'] });
    expect(hub.telemetryFor('SL-51000')).toBeUndefined();
  });

  it('watches again after reconnecting (the server forgot)', async () => {
    await hub.start();
    await hub.watch('SL-51000');
    connection.invoked.length = 0;

    connection.simulateReconnecting();
    connection.simulateReconnected();
    await Promise.resolve();

    expect(connection.invoked).toContainEqual({ method: 'WatchSpeaker', args: ['SL-51000'] });
  });

  it('reports a failed watch to the caller', async () => {
    await hub.start();
    connection.invokeError = new Error("No speaker with id 'SL-NOPE' has been discovered.");

    await expect(hub.watch('SL-NOPE')).rejects.toThrow('SL-NOPE');
  });
});
