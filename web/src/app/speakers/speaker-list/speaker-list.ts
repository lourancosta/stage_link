import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SpeakerHub } from '../../core/speaker-hub';
import { ControlPanel } from '../control-panel/control-panel';
import { TelemetryPanel } from '../telemetry-panel/telemetry-panel';

// Home page: every speaker on the network, each with its controls and live
// telemetry, so a whole system can be monitored without opening each speaker.
@Component({
  selector: 'app-speaker-list',
  imports: [RouterLink, ControlPanel, TelemetryPanel],
  templateUrl: './speaker-list.html',
  styleUrl: './speaker-list.scss',
})
export class SpeakerList {
  protected readonly hub = inject(SpeakerHub);
}
