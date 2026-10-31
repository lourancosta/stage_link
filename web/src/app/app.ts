import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BackendStatus } from './core/backend-status';
import { SpeakerHub } from './core/speaker-hub';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  protected readonly backend = inject(BackendStatus);
  private readonly hub = inject(SpeakerHub);

  ngOnInit(): void {
    void this.backend.check();
    void this.hub.start();
  }
}
