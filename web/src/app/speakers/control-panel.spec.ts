import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ApiError, SpeakerApi } from '../core/speaker-api';
import { SpeakerHub } from '../core/speaker-hub';
import { FakeSpeakerHub } from '../testing/fake-speaker-hub';
import { FakeSpeakerApi } from '../testing/fake-speaker-api';
import { ControlPanel } from './control-panel';

describe('ControlPanel', () => {
  let api: FakeSpeakerApi;
  let hub: FakeSpeakerHub;
  let fixture: ComponentFixture<ControlPanel>;

  beforeEach(async () => {
    api = new FakeSpeakerApi();
    hub = new FakeSpeakerHub();
    await TestBed.configureTestingModule({
      imports: [ControlPanel],
      providers: [
        { provide: SpeakerApi, useValue: api },
        { provide: SpeakerHub, useValue: hub },
      ],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    fixture = TestBed.createComponent(ControlPanel);
    fixture.componentRef.setInput('speakerId', 'SL-1');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function text(page: HTMLElement, testId: string): string {
    return page.querySelector(`[data-testid="${testId}"]`)?.textContent?.trim() ?? '';
  }

  // Simulates the user dragging a slider and letting go.
  async function moveSlider(page: HTMLElement, testId: string, value: number): Promise<void> {
    const slider = page.querySelector(`[data-testid="${testId}"]`) as HTMLInputElement;
    slider.value = String(value);
    slider.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  it('loads and shows the current settings', async () => {
    api.getStatus.mockResolvedValue({ name: 'Main L', gain: -6.5, muted: true, tilt: 7.5 });

    const page = await render();

    expect(api.getStatus).toHaveBeenCalledWith('SL-1');
    expect(text(page, 'gain-value')).toBe('-6.5 dB');
    expect(text(page, 'tilt-value')).toBe('+7.5°');
    expect(text(page, 'mute-button')).toBe('Muted');
    expect((page.querySelector('[data-testid="gain-slider"]') as HTMLInputElement).value).toBe('-6.5');
  });

  it('sends a new gain when the slider is released', async () => {
    const page = await render();

    await moveSlider(page, 'gain-slider', -3);

    expect(api.setGain).toHaveBeenCalledWith('SL-1', -3);
    expect(text(page, 'gain-value')).toBe('-3.0 dB');
  });

  it('sends a new tilt when the slider is released', async () => {
    const page = await render();

    await moveSlider(page, 'tilt-slider', -4.5);

    expect(api.setTilt).toHaveBeenCalledWith('SL-1', -4.5);
    expect(text(page, 'tilt-value')).toBe('-4.5°');
  });

  it('toggles mute', async () => {
    const page = await render();

    (page.querySelector('[data-testid="mute-button"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(api.setMute).toHaveBeenCalledWith('SL-1', true);
    expect(text(page, 'mute-button')).toBe('Muted');
  });

  it('shows the error and jumps back when the speaker rejects a value', async () => {
    api.setGain.mockRejectedValue(new ApiError(422, 'Speaker rejected SetGain: the value is out of range.'));
    const page = await render();

    await moveSlider(page, 'gain-slider', 12);

    expect(text(page, 'gain-error')).toContain('out of range');
    expect(text(page, 'gain-value')).toBe('-6.5 dB');
    expect((page.querySelector('[data-testid="gain-slider"]') as HTMLInputElement).value).toBe('-6.5');
  });

  it('clears the error after a successful change', async () => {
    api.setGain.mockRejectedValueOnce(new ApiError(503, 'Speaker unavailable'));
    const page = await render();
    await moveSlider(page, 'gain-slider', 0);

    await moveSlider(page, 'gain-slider', -1);

    expect(page.querySelector('[data-testid="gain-error"]')).toBeNull();
  });

  it('shows a message when the settings cannot be loaded', async () => {
    api.getStatus.mockRejectedValue(new ApiError(503, "Speaker 'SL-1' could not be reached."));

    const page = await render();

    expect(text(page, 'load-error')).toContain('could not be reached');
  });

  // Keeping in sync with live telemetry

  it('follows changes made elsewhere (from telemetry)', async () => {
    const page = await render();

    hub.pushTelemetry('SL-1', { temperature: 25, level: -60, limiterActive: false, gain: 3, muted: true, tilt: -2, faults: 'None' });
    await fixture.whenStable();

    expect(text(page, 'gain-value')).toBe('+3.0 dB');
    expect(text(page, 'tilt-value')).toBe('-2.0°');
    expect(text(page, 'mute-button')).toBe('Muted');
  });

  it('does not let telemetry undo a change that is still on its way', async () => {
    api.setGain.mockReturnValue(new Promise(() => {})); // never answers
    const page = await render();
    await moveSlider(page, 'gain-slider', -3);

    // Telemetry sent before the speaker applied the change still says -6.5.
    hub.pushTelemetry('SL-1', { temperature: 25, level: -60, limiterActive: false, gain: -6.5, muted: false, tilt: 0, faults: 'None' });
    await fixture.whenStable();

    expect(text(page, 'gain-value')).toBe('-3.0 dB');
  });
});
