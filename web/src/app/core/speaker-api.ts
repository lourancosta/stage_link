import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, PendingTasks, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { SpeakerStatus } from './speaker.models';

// A failed API call, with the backend's own message (from ProblemDetails).
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
}

// The backend's HTTP API for one speaker's settings.
@Injectable({ providedIn: 'root' })
export class SpeakerApi {
  private readonly http = inject(HttpClient);
  private readonly pendingTasks = inject(PendingTasks);

  getStatus(id: string): Promise<SpeakerStatus> {
    return this.send(this.http.get<SpeakerStatus>(this.url(id, 'status')));
  }

  setGain(id: string, gain: number): Promise<void> {
    return this.send(this.http.put<void>(this.url(id, 'gain'), { gain }));
  }

  setMute(id: string, muted: boolean): Promise<void> {
    return this.send(this.http.put<void>(this.url(id, 'mute'), { muted }));
  }

  setTilt(id: string, tilt: number): Promise<void> {
    return this.send(this.http.put<void>(this.url(id, 'tilt'), { tilt }));
  }

  // encodeURIComponent: an id with spaces or slashes can't break the URL.
  private url(id: string, path: string): string {
    return `/api/speakers/${encodeURIComponent(id)}/${path}`;
  }

  // Runs a request as a promise, converts errors to ApiError, and tells Angular
  // it's busy until done (so tests and the app wait for the result).
  private async send<T>(request: Observable<T>): Promise<T> {
    const done = this.pendingTasks.add();
    try {
      return await firstValueFrom(request);
    } catch (error) {
      throw toApiError(error);
    } finally {
      done();
    }
  }
}

function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return new ApiError(0, 'Unexpected error.');
  }

  if (error.status === 0) {
    return new ApiError(0, 'Could not reach the backend.');
  }

  const problem = error.error as ProblemDetails | null;
  const message = problem?.detail ?? problem?.title ?? `Request failed (${error.status}).`;
  return new ApiError(error.status, message);
}
