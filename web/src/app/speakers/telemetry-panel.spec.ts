import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpeakerHub } from '../core/speaker-hub';
import { TelemetryReading } from '../core/speaker.models';
import { FakeSpeakerHub } from '../testing/fake-speaker-hub';
import { TelemetryPanel } from './telemetry-panel';

function reading(overrides: Partial<TelemetryReading> = {}): TelemetryReading {
  return { temperature: 41.5, level: -12.25, limiterActive: false, gain: -6.5, muted: false, tilt: 0, faults: 'None', ...overrides };
}

describe('TelemetryPanel', () => {
  let hub: FakeSpeakerHub;
  let fixture: ComponentFixture<TelemetryPanel>;

  beforeEach(async () => {
    hub = new FakeSpeakerHub();
    await TestBed.configureTestingModule({
      imports: [TelemetryPanel],
      providers: [{ provide: SpeakerHub, useValue: hub }],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    fixture = TestBed.createComponent(TelemetryPanel);
    fixture.componentRef.setInput('speakerId', 'SL-1');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  async function push(value: TelemetryReading): Promise<void> {
    hub.pushTelemetry('SL-1', value);
    await fixture.whenStable();
  }

  function text(page: HTMLElement, testId: string): string {
    return page.querySelector(`[data-testid="${testId}"]`)?.textContent?.trim() ?? '';
  }

  it('starts watching the speaker', async () => {
    await render();

    expect(hub.watch).toHaveBeenCalledWith('SL-1');
  });

  it('waits for the first reading', async () => {
    const page = await render();

    expect(page.textContent).toContain('Waiting for live data');
  });

  it('shows temperature, level and limiter', async () => {
    const page = await render();

    await push(reading({ temperature: 41.5, level: -12.25, limiterActive: true }));

    expect(text(page, 'temperature')).toBe('41.5 °C');
    expect(text(page, 'level')).toBe('-12.3 dB');
    expect(text(page, 'limiter')).toBe('Active');
  });

  it('fills the level meter from -60 dB (empty) to 0 dB (full)', async () => {
    const page = await render();

    await push(reading({ level: -15 }));

    const bar = page.querySelector('[data-testid="level-bar"]') as HTMLElement;
    expect(bar.style.width).toBe('75%');
  });

  it('says "No faults" when everything is fine', async () => {
    const page = await render();

    await push(reading({ faults: 'None' }));

    expect(text(page, 'faults')).toBe('No faults');
  });

  it('lists active faults in plain words', async () => {
    const page = await render();

    await push(reading({ faults: 'Overtemperature, PowerSupplyFault' }));

    const faults = Array.from(page.querySelectorAll('[data-testid="fault"]')).map((f) => f.textContent?.trim());
    expect(faults).toEqual(['Overtemperature', 'Power supply fault']);
  });

  it('marks the temperature as hot above 60 °C', async () => {
    const page = await render();

    await push(reading({ temperature: 62 }));

    expect(page.querySelector('[data-testid="temperature"]')?.classList).toContain('hot');
  });

  it('stops watching when it goes away', async () => {
    await render();

    fixture.destroy();

    expect(hub.unwatch).toHaveBeenCalledWith('SL-1');
  });

  it('shows a message when live data is not available', async () => {
    hub.watch.mockRejectedValue(new Error("Speaker 'SL-1' could not be reached."));

    const page = await render();

    expect(text(page, 'telemetry-error')).toContain('could not be reached');
  });
});
