import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpeakerApi } from '../../core/speaker-api';
import { SpeakerHub } from '../../core/speaker-hub';
import { TelemetryReading } from '../../core/speaker.models';
import { FakeSpeakerApi } from '../../testing/fake-speaker-api';
import { FakeSpeakerHub } from '../../testing/fake-speaker-hub';
import { TemperatureChart } from './temperature-chart';

function reading(temperature: number): TelemetryReading {
  return { temperature, level: -20, limiterActive: false, gain: 0, muted: false, tilt: 0, faults: 'None' };
}

describe('TemperatureChart', () => {
  let api: FakeSpeakerApi;
  let hub: FakeSpeakerHub;
  let fixture: ComponentFixture<TemperatureChart>;

  beforeEach(async () => {
    api = new FakeSpeakerApi();
    hub = new FakeSpeakerHub();
    await TestBed.configureTestingModule({
      imports: [TemperatureChart],
      providers: [
        { provide: SpeakerApi, useValue: api },
        { provide: SpeakerHub, useValue: hub },
      ],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    fixture = TestBed.createComponent(TemperatureChart);
    fixture.componentRef.setInput('speakerId', 'SL-1');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function linePoints(page: HTMLElement): string[] {
    return (page.querySelector('[data-testid="temperature-line"]')?.getAttribute('points') ?? '').split(' ').filter(Boolean);
  }

  it('loads the history for the speaker', async () => {
    await render();

    expect(api.getHistory).toHaveBeenCalledWith('SL-1');
  });

  it('says it is collecting data when there is no history yet', async () => {
    const page = await render();

    expect(page.textContent).toContain('Collecting data');
  });

  it('draws one point per sample and shows the latest temperature', async () => {
    api.getHistory.mockResolvedValue([
      { time: '2026-10-31T20:00:00Z', temperature: 30, level: -20 },
      { time: '2026-10-31T20:00:01Z', temperature: 31.5, level: -20 },
    ]);

    const page = await render();

    expect(linePoints(page)).toHaveLength(2);
    expect(page.querySelector('[data-testid="current-temperature"]')?.textContent?.trim()).toBe('31.5 °C');
  });

  it('adds live readings to the line', async () => {
    api.getHistory.mockResolvedValue([{ time: new Date(Date.now() - 5000).toISOString(), temperature: 30, level: -20 }]);
    const page = await render();

    hub.pushTelemetry('SL-1', reading(33));
    await fixture.whenStable();

    expect(linePoints(page)).toHaveLength(2);
    expect(page.querySelector('[data-testid="current-temperature"]')?.textContent?.trim()).toBe('33.0 °C');
  });

  it('shows the overtemperature limit', async () => {
    api.getHistory.mockResolvedValue([{ time: '2026-10-31T20:00:00Z', temperature: 30, level: -20 }]);

    const page = await render();

    expect(page.querySelector('[data-testid="threshold-label"]')?.textContent).toContain('70 °C');
  });
});
