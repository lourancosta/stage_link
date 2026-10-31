import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiError, SpeakerApi } from './speaker-api';

describe('SpeakerApi', () => {
  let api: SpeakerApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(SpeakerApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the status', async () => {
    const result = api.getStatus('SL-1');
    http.expectOne({ method: 'GET', url: '/api/speakers/SL-1/status' })
      .flush({ name: 'Main L', gain: -6.5, muted: true, tilt: 7.5 });

    expect(await result).toEqual({ name: 'Main L', gain: -6.5, muted: true, tilt: 7.5 });
  });

  it('sends the gain', async () => {
    const result = api.setGain('SL-1', -6.5);
    const request = http.expectOne({ method: 'PUT', url: '/api/speakers/SL-1/gain' });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await result;

    expect(request.request.body).toEqual({ gain: -6.5 });
  });

  it('sends mute and tilt', async () => {
    const mute = api.setMute('SL-1', true);
    const muteRequest = http.expectOne({ method: 'PUT', url: '/api/speakers/SL-1/mute' });
    muteRequest.flush(null, { status: 204, statusText: 'No Content' });
    await mute;

    const tilt = api.setTilt('SL-1', 7.5);
    const tiltRequest = http.expectOne({ method: 'PUT', url: '/api/speakers/SL-1/tilt' });
    tiltRequest.flush(null, { status: 204, statusText: 'No Content' });
    await tilt;

    expect(muteRequest.request.body).toEqual({ muted: true });
    expect(tiltRequest.request.body).toEqual({ tilt: 7.5 });
  });

  it('encodes the id in the URL', async () => {
    const result = api.getStatus('SL 1/2');
    http.expectOne('/api/speakers/SL%201%2F2/status').flush({ name: 'x', gain: 0, muted: false, tilt: 0 });
    await result;
  });

  it('turns a ProblemDetails error into an ApiError with its message', async () => {
    const result = api.setGain('SL-1', 20);
    http.expectOne('/api/speakers/SL-1/gain').flush(
      { status: 422, title: 'Value out of range', detail: 'Speaker rejected SetGain: the value is out of range.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );

    const error = await result.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(422);
    expect((error as ApiError).message).toBe('Speaker rejected SetGain: the value is out of range.');
  });

  it('explains when the backend cannot be reached', async () => {
    const result = api.getStatus('SL-1');
    http.expectOne('/api/speakers/SL-1/status').error(new ProgressEvent('error'));

    const error = (await result.catch((e: unknown) => e)) as ApiError;
    expect(error.status).toBe(0);
    expect(error.message).toBe('Could not reach the backend.');
  });
});
