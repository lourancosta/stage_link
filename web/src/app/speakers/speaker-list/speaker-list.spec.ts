import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SpeakerHub } from '../../core/speaker-hub';
import { DiscoveredSpeaker } from '../../core/speaker.models';
import { FakeSpeakerHub } from '../../testing/fake-speaker-hub';
import { SpeakerList } from './speaker-list';

function speaker(id: string, name: string, tcpPort = 51000): DiscoveredSpeaker {
  return { id, name, model: 'StageLink Sim', firmware: '1.0.0', address: '127.0.0.1', tcpPort, lastSeen: '' };
}

describe('SpeakerList', () => {
  let hub: FakeSpeakerHub;

  beforeEach(async () => {
    hub = new FakeSpeakerHub();
    await TestBed.configureTestingModule({
      imports: [SpeakerList],
      providers: [provideRouter([]), { provide: SpeakerHub, useValue: hub }],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SpeakerList);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows a card for each speaker', async () => {
    hub.speakers.set([speaker('SL-51000', 'Main L'), speaker('SL-51010', 'Main R', 51010)]);

    const page = await render();
    const cards = page.querySelectorAll('[data-testid="speaker-card"]');

    expect(cards.length).toBe(2);
    expect(cards[0].textContent).toContain('Main L');
    expect(cards[0].textContent).toContain('StageLink Sim');
    expect(cards[1].textContent).toContain('127.0.0.1:51010');
  });

  it('links each card to the speaker page', async () => {
    hub.speakers.set([speaker('SL-51000', 'Main L')]);

    const page = await render();
    const link = page.querySelector('[data-testid="speaker-card"]') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/speakers/SL-51000');
  });

  it('explains how to start a simulator when no speakers are found', async () => {
    const page = await render();

    expect(page.querySelector('[data-testid="empty"]')?.textContent).toContain('No speakers found');
  });

  it('says it is still looking before the first list arrives', async () => {
    hub.loaded.set(false);

    const page = await render();

    expect(page.textContent).toContain('Looking for speakers');
    expect(page.querySelector('[data-testid="empty"]')).toBeNull();
  });

  it('warns when the live connection is lost', async () => {
    hub.state.set('reconnecting');

    const page = await render();

    expect(page.querySelector('[data-testid="connection-warning"]')?.textContent).toContain('Reconnecting');
  });

  it('shows no warning while connected', async () => {
    const page = await render();

    expect(page.querySelector('[data-testid="connection-warning"]')).toBeNull();
  });
});
