import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { SpeakerHub } from '../../core/speaker-hub';

const SILENCE_DB = -60;
const HOT_CELSIUS = 60;

const FAULT_LABELS: Record<string, string> = {
  Overtemperature: 'Overtemperature',
  AmplifierFault: 'Amplifier fault',
  PowerSupplyFault: 'Power supply fault',
};

// Live readings for one speaker, pushed over SignalR about 5 times a second.
// Watches the speaker while visible and stops when the page is left.
@Component({
  selector: 'app-telemetry-panel',
  templateUrl: './telemetry-panel.html',
  styleUrl: './telemetry-panel.scss',
  host: { '[class.compact]': "variant() === 'compact'" },
})
export class TelemetryPanel {
  private readonly hub = inject(SpeakerHub);

  readonly speakerId = input.required<string>();

  // 'full' on the speaker page; 'compact' inside the cards on the speaker list.
  readonly variant = input<'full' | 'compact'>('full');

  protected readonly error = signal<string | null>(null);
  protected readonly reading = computed(() => this.hub.telemetryFor(this.speakerId()));

  // Bar width: -60 dB (silence) is empty, 0 dB is full.
  protected readonly levelPercent = computed(() => {
    const level = this.reading()?.level ?? SILENCE_DB;
    const percent = ((level - SILENCE_DB) / -SILENCE_DB) * 100;
    return Math.min(100, Math.max(0, percent));
  });

  protected readonly faults = computed(() => {
    const faults = this.reading()?.faults ?? 'None';
    return faults === 'None' ? [] : faults.split(',').map((name) => FAULT_LABELS[name.trim()] ?? name.trim());
  });

  protected readonly hot = computed(() => (this.reading()?.temperature ?? 0) > HOT_CELSIUS);
  protected readonly overheated = computed(() => this.faults().includes('Overtemperature'));

  constructor() {
    // Watch the current speaker; the cleanup runs when the id changes or the
    // panel is destroyed (leaving the page), so the speaker stops streaming.
    effect((onCleanup) => {
      const id = this.speakerId();
      untracked(() => {
        this.error.set(null);
        this.hub.watch(id).catch((e: unknown) => this.error.set(e instanceof Error ? e.message : 'Live data unavailable.'));
      });
      onCleanup(() => void this.hub.unwatch(id));
    });
  }
}
