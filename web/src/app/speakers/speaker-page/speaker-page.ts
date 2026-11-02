import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SpeakerHub } from '../../core/speaker-hub';
import { ControlPanel } from '../control-panel/control-panel';
import { EventLog } from '../event-log/event-log';
import { SettingsCard } from '../settings-card/settings-card';
import { TemperatureChart } from '../temperature-chart/temperature-chart';
import { TelemetryPanel } from '../telemetry-panel/telemetry-panel';

// One speaker's page (/speakers/:id): controls and live telemetry like the
// dashboard cards, plus what doesn't fit in a card: temperature history, the
// event log and settings.
@Component({
  selector: 'app-speaker-page',
  imports: [RouterLink, ControlPanel, TelemetryPanel, TemperatureChart, EventLog, SettingsCard],
  templateUrl: './speaker-page.html',
  styleUrl: './speaker-page.scss',
})
export class SpeakerPage {
  protected readonly hub = inject(SpeakerHub);

  // Filled from the :id route parameter (withComponentInputBinding).
  readonly id = input.required<string>();

  // Recomputed automatically when the id or the live speaker list changes.
  protected readonly speaker = computed(() => this.hub.findSpeaker(this.id()));
}
