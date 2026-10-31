import { signal } from '@angular/core';
import { HubState } from '../core/speaker-hub';
import { DiscoveredSpeaker, TelemetryReading } from '../core/speaker.models';

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

  pushTelemetry(id: string, reading: TelemetryReading): void {
    this.telemetry.update((all) => ({ ...all, [id]: reading }));
  }

  findSpeaker(id: string): DiscoveredSpeaker | undefined {
    return this.speakers().find((speaker) => speaker.id === id);
  }
}
