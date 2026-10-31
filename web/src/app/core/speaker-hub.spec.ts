import { TestBed } from '@angular/core/testing';
import { FakeHubConnection } from '../testing/fake-hub-connection';
import { HUB_CONNECTION_FACTORY } from './hub-connection';
import { SpeakerHub } from './speaker-hub';
import { DiscoveredSpeaker } from './speaker.models';

const mainL: DiscoveredSpeaker = {
  id: 'SL-51000',
  name: 'Main L',
  model: 'StageLink Sim',
  firmware: '1.0.0',
  address: '127.0.0.1',
  tcpPort: 51000,
  lastSeen: '2026-10-30T12:00:00Z',
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
});
