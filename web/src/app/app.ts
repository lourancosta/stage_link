import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BackendStatus } from './core/backend-status';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  protected readonly backend = inject(BackendStatus);

  ngOnInit(): void {
    void this.backend.check();
  }
}
