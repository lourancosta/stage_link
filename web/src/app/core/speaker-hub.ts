import { Injectable, inject, signal } from '@angular/core';
import { HUB_CONNECTION_FACTORY } from './hub-connection';
import { DiscoveredSpeaker, TelemetryReading } from './speaker.models';

export type HubState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

const RETRY_DELAY_MS = 5000;

// The app's live connection to the backend (SignalR, /hubs/speakers).
// Keeps the discovered speakers in a signal that pages read directly.
@Injectable({ providedIn: 'root' })
export class SpeakerHub {
  private readonly connection = inject(HUB_CONNECTION_FACTORY)();
  private started = false;

  private readonly speakersState = signal<DiscoveredSpeaker[]>([]);
  readonly speakers = this.speakersState.asReadonly();

  // False until the first list arrives, so pages can say "loading" instead of
  // wrongly saying "no speakers" or "not found".
  private readonly loadedState = signal(false);
  readonly loaded = this.loadedState.asReadonly();

  private readonly hubState = signal<HubState>('disconnected');
  readonly state = this.hubState.asReadonly();

  // Latest Telemetry reading per watched speaker id.
  private readonly telemetryState = signal<Record<string, TelemetryReading>>({});

  // Speakers this browser wants Telemetry for. Remembered so they can be
  // watched again after a reconnect: the server forgets (new connection id).
  private readonly watched = new Set<string>();

  constructor() {
    this.connection.on('SpeakersChanged', (speakers: DiscoveredSpeaker[]) => {
      this.speakersState.set(speakers);
      this.loadedState.set(true);
    });

    this.connection.on('Telemetry', (speakerId: string, reading: TelemetryReading) =>
      this.telemetryState.update((all) => ({ ...all, [speakerId]: reading })),
    );

    // Short drops: SignalR's automatic reconnect handles them.
    this.connection.onreconnecting(() => this.hubState.set('reconnecting'));
    this.connection.onreconnected(() => {
      this.hubState.set('connected');
      void this.watchAgain();
    });

    // Automatic reconnect gave up (e.g. backend stopped for a while): keep trying.
    this.connection.onclose(() => {
      this.hubState.set('disconnected');
      this.scheduleRetry();
    });
  }

  async start(): Promise<void> {
    if (this.started) {
      return;
    }

    this.started = true;
    await this.connect();
  }

  findSpeaker(id: string): DiscoveredSpeaker | undefined {
    return this.speakers().find((speaker) => speaker.id === id);
  }

  telemetryFor(id: string): TelemetryReading | undefined {
    return this.telemetryState()[id];
  }

  // Start receiving Telemetry for a speaker. If not connected yet, it happens
  // as soon as the connection is up. Rejects if the server refuses (e.g. unknown id).
  async watch(id: string): Promise<void> {
    this.watched.add(id);
    if (this.hubState() === 'connected') {
      await this.connection.invoke('WatchSpeaker', id);
    }
  }

  async unwatch(id: string): Promise<void> {
    this.watched.delete(id);
    this.telemetryState.update(({ [id]: _removed, ...others }) => others);

    if (this.hubState() === 'connected') {
      try {
        await this.connection.invoke('UnwatchSpeaker', id);
      } catch {
        // Best effort: the server also cleans up when the connection closes.
      }
    }
  }

  private async connect(): Promise<void> {
    this.hubState.set('connecting');
    try {
      await this.connection.start();
      this.hubState.set('connected');
      await this.watchAgain();
    } catch {
      // Backend not running yet. Try again in a few seconds.
      this.hubState.set('disconnected');
      this.scheduleRetry();
    }
  }

  private async watchAgain(): Promise<void> {
    for (const id of this.watched) {
      try {
        await this.connection.invoke('WatchSpeaker', id);
      } catch {
        // That speaker may be gone; its page shows the problem when it retries.
      }
    }
  }

  private scheduleRetry(): void {
    setTimeout(() => void this.connect(), RETRY_DELAY_MS);
  }
}
