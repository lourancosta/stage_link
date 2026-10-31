import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SpeakerHub } from '../core/speaker-hub';
import { ControlPanel } from './control-panel';

// One speaker's page (/speakers/:id): details and controls.
@Component({
  selector: 'app-speaker-page',
  imports: [RouterLink, ControlPanel],
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
