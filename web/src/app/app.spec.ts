import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  async function render(healthAnswer: 'ok' | 'error'): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges(); // runs ngOnInit, which checks the backend

    const request = http.expectOne('/api/health');
    if (healthAnswer === 'ok') {
      request.flush({ status: 'ok' });
    } else {
      request.flush('down', { status: 503, statusText: 'Service Unavailable' });
    }

    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the StageLink title', async () => {
    const page = await render('ok');

    expect(page.querySelector('h1')?.textContent).toContain('StageLink');
  });

  it('shows "Backend online" when the backend answers', async () => {
    const page = await render('ok');

    expect(page.querySelector('[data-testid="backend-status"]')?.textContent).toContain('Backend online');
  });

  it('shows "Backend offline" when the backend fails', async () => {
    const page = await render('error');

    expect(page.querySelector('[data-testid="backend-status"]')?.textContent).toContain('Backend offline');
  });
});
