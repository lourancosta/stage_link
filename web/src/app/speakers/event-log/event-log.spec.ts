import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ApiError, SpeakerApi } from '../../core/speaker-api';
import { SpeakerHub } from '../../core/speaker-hub';
import { FakeSpeakerApi } from '../../testing/fake-speaker-api';
import { FakeSpeakerHub } from '../../testing/fake-speaker-hub';
import { EventLog } from './event-log';

describe('EventLog', () => {
  let api: FakeSpeakerApi;
  let hub: FakeSpeakerHub;
  let fixture: ComponentFixture<EventLog>;

  beforeEach(async () => {
    api = new FakeSpeakerApi();
    hub = new FakeSpeakerHub();
    await TestBed.configureTestingModule({
      imports: [EventLog],
      providers: [
        { provide: SpeakerApi, useValue: api },
        { provide: SpeakerHub, useValue: hub },
      ],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    fixture = TestBed.createComponent(EventLog);
    fixture.componentRef.setInput('speakerId', 'SL-1');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function messages(page: HTMLElement): string[] {
    return Array.from(page.querySelectorAll('[data-testid="event-message"]')).map((e) => e.textContent?.trim() ?? '');
  }

  it('shows the recorded events, newest first', async () => {
    api.getEvents.mockResolvedValue([
      { time: '2026-10-31T20:00:05Z', severity: 'Warning', message: 'Limiter engaged' },
      { time: '2026-10-31T20:00:00Z', severity: 'Info', message: 'Connected' },
    ]);

    const page = await render();

    expect(api.getEvents).toHaveBeenCalledWith('SL-1');
    expect(messages(page)).toEqual(['Limiter engaged', 'Connected']);
  });

  it('marks each event with its severity', async () => {
    api.getEvents.mockResolvedValue([{ time: '2026-10-31T20:00:05Z', severity: 'Error', message: 'Went offline' }]);

    const page = await render();

    expect(page.querySelector('[data-testid="event"]')?.classList).toContain('error');
  });

  it('adds new live events at the top', async () => {
    api.getEvents.mockResolvedValue([{ time: '2026-10-31T20:00:00Z', severity: 'Info', message: 'Connected' }]);
    const page = await render();

    hub.pushEvent('SL-1', { time: '2026-10-31T20:00:10Z', severity: 'Info', message: 'Muted' });
    await fixture.whenStable();

    expect(messages(page)).toEqual(['Muted', 'Connected']);
  });

  it('does not repeat a live event that was already loaded', async () => {
    const connected = { time: '2026-10-31T20:00:00Z', severity: 'Info' as const, message: 'Connected' };
    api.getEvents.mockResolvedValue([connected]);
    hub.pushEvent('SL-1', connected);

    const page = await render();

    expect(messages(page)).toEqual(['Connected']);
  });

  it('says so when nothing has happened yet', async () => {
    const page = await render();

    expect(page.textContent).toContain('No events yet');
  });

  it('shows a message when the log cannot be loaded', async () => {
    api.getEvents.mockRejectedValue(new ApiError(0, 'Could not reach the backend.'));

    const page = await render();

    expect(page.querySelector('[data-testid="events-error"]')?.textContent).toContain('Could not reach the backend');
  });
});
