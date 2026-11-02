import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ApiError, SpeakerApi } from '../../core/speaker-api';

const MAX_NAME_LENGTH = 255;
const PRINTABLE_ASCII = /^[\x20-\x7E]+$/;

// Speaker settings. For now: renaming. The protocol sends names as ASCII text
// of at most 255 characters, so the form checks that before sending (the
// backend and the speaker check again).
@Component({
  selector: 'app-settings-card',
  templateUrl: './settings-card.html',
  styleUrl: './settings-card.scss',
})
export class SettingsCard {
  private readonly api = inject(SpeakerApi);

  readonly speakerId = input.required<string>();
  readonly name = input.required<string>(); // the current name

  protected readonly draft = signal('');
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);
  private edited = false;

  private readonly trimmed = computed(() => this.draft().trim());

  protected readonly hint = computed(() => {
    const name = this.trimmed();
    if (name.length > MAX_NAME_LENGTH) {
      return `At most ${MAX_NAME_LENGTH} characters.`;
    }
    if (name.length > 0 && !PRINTABLE_ASCII.test(name)) {
      return 'Use plain letters, numbers and symbols (no accents or emoji).';
    }
    return null;
  });

  protected readonly canSave = computed(
    () => this.trimmed().length > 0 && this.hint() === null && this.trimmed() !== this.name() && !this.saving(),
  );

  constructor() {
    // Follow the current name (e.g. renamed from another tab) unless the user is typing.
    effect(() => {
      const current = this.name();
      untracked(() => {
        if (!this.edited) {
          this.draft.set(current);
        }
      });
    });
  }

  protected inputId(): string {
    return `name-${this.speakerId()}`;
  }

  protected onInput(event: Event): void {
    this.edited = true;
    this.draft.set((event.target as HTMLInputElement).value);
    this.saved.set(false);
    this.error.set(null);
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    try {
      await this.api.setName(this.speakerId(), this.trimmed());
      this.edited = false;
      this.saved.set(true);
    } catch (error) {
      this.error.set(error instanceof ApiError ? error.message : 'Could not save the name.');
    } finally {
      this.saving.set(false);
    }
  }
}
