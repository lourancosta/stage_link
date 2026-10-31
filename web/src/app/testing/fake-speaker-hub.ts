import { signal } from '@angular/core';
import { HubState } from '../core/speaker-hub';
import { DiscoveredSpeaker } from '../core/speaker.models';

// Replaces SpeakerHub in component tests: tests set the signals directly.
export class FakeSpeakerHub {
  readonly speakers = signal<DiscoveredSpeaker[]>([]);
  readonly state = signal<HubState>('connected');
  readonly loaded = signal(true);
  readonly start = vi.fn(async () => {});

  findSpeaker(id: string): DiscoveredSpeaker | undefined {
    return this.speakers().find((speaker) => speaker.id === id);
  }
}
