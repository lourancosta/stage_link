import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiError, SpeakerApi } from '../../core/speaker-api';
import { SpeakerHub } from '../../core/speaker-hub';
import { TelemetryReading, TemperatureSample } from '../../core/speaker.models';
import { buildChart } from './chart-geometry';

const WINDOW_SECONDS = 600; // 10 minutes
const SAMPLE_INTERVAL_MS = 1000;

// The last 10 minutes of temperature: history from the backend, then extended
// live with the readings that arrive over SignalR (one point per second).
@Component({
  selector: 'app-temperature-chart',
  templateUrl: './temperature-chart.html',
  styleUrl: './temperature-chart.scss',
})
export class TemperatureChart {
  private readonly api = inject(SpeakerApi);
  private readonly hub = inject(SpeakerHub);

  readonly speakerId = input.required<string>();

  protected readonly width = 600;
  protected readonly height = 160;

  protected readonly samples = signal<TemperatureSample[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly chart = computed(() =>
    buildChart(this.samples(), { width: this.width, height: this.height, window: WINDOW_SECONDS }),
  );
  protected readonly current = computed(() => this.samples().at(-1)?.temperature);

  constructor() {
    effect(() => {
      const id = this.speakerId();
      untracked(() => void this.load(id));
    });

    effect(() => {
      const reading = this.hub.telemetryFor(this.speakerId());
      if (reading) {
        untracked(() => this.append(reading));
      }
    });
  }

  private async load(id: string): Promise<void> {
    this.error.set(null);
    try {
      const history = await this.api.getHistory(id);
      // Keep live points that are newer than the history (they arrived while loading).
      const lastHistoryTime = history.length > 0 ? Date.parse(history[history.length - 1].time) : 0;
      this.samples.update((live) => [...history, ...live.filter((s) => Date.parse(s.time) > lastHistoryTime)]);
    } catch (error) {
      this.error.set(error instanceof ApiError ? error.message : 'Could not load the history.');
    }
  }

  // Telemetry arrives 5 times a second; the chart keeps one point per second.
  private append(reading: TelemetryReading): void {
    const now = Date.now();
    const last = this.samples().at(-1);
    if (last && now - Date.parse(last.time) < SAMPLE_INTERVAL_MS) {
      return;
    }

    const sample = { time: new Date(now).toISOString(), temperature: reading.temperature, level: reading.level };
    const oldest = now - WINDOW_SECONDS * 1000;
    this.samples.update((samples) => [...samples, sample].filter((s) => Date.parse(s.time) >= oldest));
  }
}
