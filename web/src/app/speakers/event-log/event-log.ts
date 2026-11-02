import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiError, SpeakerApi } from '../../core/speaker-api';
import { SpeakerHub } from '../../core/speaker-hub';
import { SpeakerEvent } from '../../core/speaker.models';

// What happened to the speaker, newest first: the backend's recorded log, plus
// new events pushed live over SignalR while the page is open.
@Component({
  selector: 'app-event-log',
  imports: [DatePipe],
  templateUrl: './event-log.html',
  styleUrl: './event-log.scss',
})
export class EventLog {
  private readonly api = inject(SpeakerApi);
  private readonly hub = inject(SpeakerHub);

  readonly speakerId = input.required<string>();

  private readonly recorded = signal<SpeakerEvent[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly events = computed(() => {
    const recorded = this.recorded();
    const known = new Set(recorded.map(key));
    // Live events arrive oldest first; skip any the recorded log already has.
    const live = this.hub
      .liveEventsFor(this.speakerId())
      .filter((e) => !known.has(key(e)))
      .reverse();
    return [...live, ...recorded];
  });

  constructor() {
    effect(() => {
      const id = this.speakerId();
      untracked(() => void this.load(id));
    });
  }

  private async load(id: string): Promise<void> {
    this.loaded.set(false);
    this.error.set(null);
    try {
      this.recorded.set(await this.api.getEvents(id));
    } catch (error) {
      this.error.set(error instanceof ApiError ? error.message : 'Could not load the event log.');
    } finally {
      this.loaded.set(true);
    }
  }
}

function key(event: SpeakerEvent): string {
  return `${event.time}|${event.message}`;
}
