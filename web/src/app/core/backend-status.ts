import { HttpClient } from '@angular/common/http';
import { Injectable, PendingTasks, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type BackendState = 'checking' | 'online' | 'offline';

// Knows whether the ASP.NET backend is reachable, via GET /api/health.
// providedIn: 'root' = one shared instance for the whole app (a singleton).
@Injectable({ providedIn: 'root' })
export class BackendStatus {
  private readonly http = inject(HttpClient);
  private readonly pendingTasks = inject(PendingTasks);

  // A signal: templates that read status() update automatically when it changes.
  private readonly state = signal<BackendState>('checking');
  readonly status = this.state.asReadonly();

  // PendingTasks tells Angular the app isn't "stable" until the check is fully
  // done, including updating the signal. Tests (whenStable) wait for it.
  async check(): Promise<void> {
    const done = this.pendingTasks.add();
    try {
      await firstValueFrom(this.http.get<{ status: string }>('/api/health'));
      this.state.set('online');
    } catch {
      this.state.set('offline');
    } finally {
      done();
    }
  }
}
