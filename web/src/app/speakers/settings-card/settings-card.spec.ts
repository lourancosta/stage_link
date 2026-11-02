import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ApiError, SpeakerApi } from '../../core/speaker-api';
import { FakeSpeakerApi } from '../../testing/fake-speaker-api';
import { SettingsCard } from './settings-card';

describe('SettingsCard', () => {
  let api: FakeSpeakerApi;
  let fixture: ComponentFixture<SettingsCard>;

  beforeEach(async () => {
    api = new FakeSpeakerApi();
    await TestBed.configureTestingModule({
      imports: [SettingsCard],
      providers: [{ provide: SpeakerApi, useValue: api }],
    }).compileComponents();
  });

  async function render(name = 'Main L'): Promise<HTMLElement> {
    fixture = TestBed.createComponent(SettingsCard);
    fixture.componentRef.setInput('speakerId', 'SL-1');
    fixture.componentRef.setInput('name', name);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function input(page: HTMLElement): HTMLInputElement {
    return page.querySelector('[data-testid="name-input"]') as HTMLInputElement;
  }

  function saveButton(page: HTMLElement): HTMLButtonElement {
    return page.querySelector('[data-testid="save-name"]') as HTMLButtonElement;
  }

  async function type(page: HTMLElement, value: string): Promise<void> {
    input(page).value = value;
    input(page).dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  it('shows the current name', async () => {
    const page = await render('Main L');

    expect(input(page).value).toBe('Main L');
  });

  it('can only save after the name was changed', async () => {
    const page = await render();
    expect(saveButton(page).disabled).toBe(true);

    await type(page, 'Main Left');

    expect(saveButton(page).disabled).toBe(false);
  });

  it('saves the new name (without surrounding spaces) and confirms', async () => {
    const page = await render();
    await type(page, '  Main Left  ');

    saveButton(page).click();
    await fixture.whenStable();

    expect(api.setName).toHaveBeenCalledWith('SL-1', 'Main Left');
    expect(page.querySelector('[data-testid="name-saved"]')?.textContent).toContain('Saved');
  });

  it('does not allow empty names or characters the protocol cannot send', async () => {
    const page = await render();

    await type(page, '   ');
    expect(saveButton(page).disabled).toBe(true);

    await type(page, 'Café');
    expect(saveButton(page).disabled).toBe(true);
    expect(page.querySelector('[data-testid="name-hint"]')?.textContent).toContain('letters');
  });

  it('shows the error when saving fails', async () => {
    api.setName.mockRejectedValue(new ApiError(503, "Speaker 'SL-1' could not be reached."));
    const page = await render();
    await type(page, 'Main Left');

    saveButton(page).click();
    await fixture.whenStable();

    expect(page.querySelector('[data-testid="name-error"]')?.textContent).toContain('could not be reached');
  });

  it('uses an id unique to the speaker', async () => {
    const page = await render();

    expect(input(page).id).toBe('name-SL-1');
    expect(page.querySelector('label[for="name-SL-1"]')).not.toBeNull();
  });
});
