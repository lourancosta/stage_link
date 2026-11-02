import { signal } from '@angular/core';
import { HubState } from '../core/speaker-hub';
import { DiscoveredSpeaker, SpeakerEvent, TelemetryReading } from '../core/speaker.models';

// Replaces SpeakerHub in component tests: tests set the signals directly.
export class FakeSpeakerHub {
  readonly speakers = signal<DiscoveredSpeaker[]>([]);
  readonly state = signal<HubState>('connected');
  readonly loaded = signal(true);
  readonly start = vi.fn(async () => {});
  readonly watch = vi.fn(async (_id: string) => {});
  readonly unwatch = vi.fn(async (_id: string) => {});
  readonly telemetry = signal<Record<string, TelemetryReading>>({});

  telemetryFor(id: string): TelemetryReading | undefined {
    return this.telemetry()[id];
  }

  readonly liveEvents = signal<Record<string, SpeakerEvent[]>>({});

  liveEventsFor(id: string): SpeakerEvent[] {
    return this.liveEvents()[id] ?? [];
  }

  pushEvent(id: string, event: SpeakerEvent): void {
    this.liveEvents.update((all) => ({ ...all, [id]: [...(all[id] ?? []), event] }));
  }

  pushTelemetry(id: string, reading: TelemetryReading): void {
    this.telemetry.update((all) => ({ ...all, [id]: reading }));
  }

  findSpeaker(id: string): DiscoveredSpeaker | undefined {
    return this.speakers().find((speaker) => speaker.id === id);
  }
}
