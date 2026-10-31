import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SpeakerApi } from '../core/speaker-api';
import { SpeakerHub } from '../core/speaker-hub';
import { FakeSpeakerApi } from '../testing/fake-speaker-api';
import { FakeSpeakerHub } from '../testing/fake-speaker-hub';
import { SpeakerPage } from './speaker-page';

describe('SpeakerPage', () => {
  let hub: FakeSpeakerHub;

  beforeEach(async () => {
    hub = new FakeSpeakerHub();
    await TestBed.configureTestingModule({
      imports: [SpeakerPage],
      providers: [
        provideRouter([]),
        { provide: SpeakerHub, useValue: hub },
        { provide: SpeakerApi, useValue: new FakeSpeakerApi() },
      ],
    }).compileComponents();
  });

  async function render(id: string): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SpeakerPage);
    fixture.componentRef.setInput('id', id); // what the router does with :id
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the speaker name', async () => {
    hub.speakers.set([
      { id: 'SL-51000', name: 'Main L', model: 'StageLink Sim', firmware: '1.0.0', address: '127.0.0.1', tcpPort: 51000, lastSeen: '' },
    ]);

    const page = await render('SL-51000');

    expect(page.querySelector('h2')?.textContent).toContain('Main L');
  });

  it('shows the control panel for a known speaker', async () => {
    hub.speakers.set([
      { id: 'SL-51000', name: 'Main L', model: 'StageLink Sim', firmware: '1.0.0', address: '127.0.0.1', tcpPort: 51000, lastSeen: '' },
    ]);

    const page = await render('SL-51000');

    expect(page.querySelector('app-control-panel')).not.toBeNull();
  });

  it('says so when the speaker is not (or no longer) on the network', async () => {
    const page = await render('SL-NOPE');

    expect(page.textContent).toContain('not found');
  });

  it('shows "Loading" instead of "not found" before the first list arrives', async () => {
    hub.loaded.set(false);

    const page = await render('SL-51000');

    expect(page.textContent).toContain('Loading');
    expect(page.textContent).not.toContain('not found');
  });

  it('links back to the list', async () => {
    const page = await render('SL-NOPE');

    expect(page.querySelector('a[href="/"]')).not.toBeNull();
  });
});
