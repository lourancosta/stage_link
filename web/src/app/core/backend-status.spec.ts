import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BackendStatus } from './backend-status';

describe('BackendStatus', () => {
  let service: BackendStatus;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      // provideHttpClientTesting intercepts every request so tests can answer it.
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(BackendStatus);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify(); // fails the test if a request was made but never answered
  });

  it('starts as "checking"', () => {
    expect(service.status()).toBe('checking');
  });

  it('is "online" when /api/health answers ok', async () => {
    const done = service.check();
    http.expectOne('/api/health').flush({ status: 'ok' });
    await done;

    expect(service.status()).toBe('online');
  });

  it('is "offline" when /api/health fails', async () => {
    const done = service.check();
    http.expectOne('/api/health').flush('Bad gateway', { status: 502, statusText: 'Bad Gateway' });
    await done;

    expect(service.status()).toBe('offline');
  });
});
