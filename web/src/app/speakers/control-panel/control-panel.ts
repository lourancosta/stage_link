import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiError, SpeakerApi } from '../../core/speaker-api';
import { SpeakerHub } from '../../core/speaker-hub';
import { SPEAKER_LIMITS, SpeakerStatus } from '../../core/speaker.models';

type Setting = 'gain' | 'mute' | 'tilt';

// Gain and tilt sliders plus a mute toggle for one speaker.
// Changes are optimistic: the screen updates at once, and jumps back with an
// error message if the backend or the speaker refuses.
@Component({
  selector: 'app-control-panel',
  templateUrl: './control-panel.html',
  styleUrl: './control-panel.scss',
  host: { '[class.compact]': "variant() === 'compact'" },
})
export class ControlPanel {
  private readonly api = inject(SpeakerApi);
  private readonly hub = inject(SpeakerHub);

  // Changes sent but not yet answered. While > 0, telemetry is ignored, because
  // a reading sent just before the speaker applied our change would undo it.
  private pendingChanges = 0;

  readonly speakerId = input.required<string>();

  // 'full' on the speaker page; 'compact' inside the cards on the speaker list.
  readonly variant = input<'full' | 'compact'>('full');

  protected readonly limits = SPEAKER_LIMITS;
  protected readonly status = signal<SpeakerStatus | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly errors = signal<Record<Setting, string | null>>({ gain: null, mute: null, tilt: null });

  constructor() {
    // Load whenever the speaker id changes (also when moving between speakers).
    effect(() => {
      const id = this.speakerId();
      untracked(() => void this.load(id));
    });

    // Follow changes made elsewhere (another tab, another user) via telemetry.
    effect(() => {
      const reading = this.hub.telemetryFor(this.speakerId());
      untracked(() => {
        const current = this.status();
        if (!reading || !current || this.pendingChanges > 0) {
          return;
        }

        if (current.gain !== reading.gain || current.muted !== reading.muted || current.tilt !== reading.tilt) {
          this.status.set({ ...current, gain: reading.gain, muted: reading.muted, tilt: reading.tilt });
        }
      });
    });
  }

  protected onGainChange(event: Event): void {
    const slider = event.target as HTMLInputElement;
    const gain = slider.valueAsNumber;
    void this.change('gain', { gain }, () => this.api.setGain(this.speakerId(), gain), (previous) => {
      slider.value = String(previous.gain);
    });
  }

  protected onTiltChange(event: Event): void {
    const slider = event.target as HTMLInputElement;
    const tilt = slider.valueAsNumber;
    void this.change('tilt', { tilt }, () => this.api.setTilt(this.speakerId(), tilt), (previous) => {
      slider.value = String(previous.tilt);
    });
  }

  protected toggleMute(): void {
    const muted = !this.status()?.muted;
    void this.change('mute', { muted }, () => this.api.setMute(this.speakerId(), muted));
  }

  // Element ids must be unique on the page, and the list shows many panels.
  protected elementId(name: string): string {
    return `${name}-${this.speakerId()}`;
  }

  protected formatGain(gain: number): string {
    return `${gain > 0 ? '+' : ''}${gain.toFixed(1)} dB`;
  }

  protected formatTilt(tilt: number): string {
    return `${tilt > 0 ? '+' : ''}${tilt.toFixed(1)}°`;
  }

  private async load(id: string): Promise<void> {
    this.status.set(null);
    this.loadError.set(null);
    try {
      this.status.set(await this.api.getStatus(id));
    } catch (error) {
      this.loadError.set(messageOf(error));
    }
  }

  // resetControl: the user moved the slider in the page directly. If the value
  // jumps back to what Angular last rendered, Angular sees "no change" and would
  // leave the slider where the user dropped it, so it's reset by hand.
  private async change(
    setting: Setting,
    update: Partial<SpeakerStatus>,
    send: () => Promise<void>,
    resetControl?: (previous: SpeakerStatus) => void,
  ): Promise<void> {
    const previous = this.status();
    if (!previous) {
      return;
    }

    this.status.set({ ...previous, ...update }); // optimistic
    this.setError(setting, null);
    this.pendingChanges++;

    try {
      await send();
    } catch (error) {
      this.status.set(previous); // jump back
      resetControl?.(previous);
      this.setError(setting, messageOf(error));
    } finally {
      this.pendingChanges--;
    }
  }

  private setError(setting: Setting, message: string | null): void {
    this.errors.update((errors) => ({ ...errors, [setting]: message }));
  }
}

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Something went wrong.';
}
