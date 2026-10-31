import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SpeakerHub } from '../../core/speaker-hub';

// Home page: every speaker currently announcing itself, updated live.
@Component({
  selector: 'app-speaker-list',
  imports: [RouterLink],
  templateUrl: './speaker-list.html',
  styleUrl: './speaker-list.scss',
})
export class SpeakerList {
  protected readonly hub = inject(SpeakerHub);
}
