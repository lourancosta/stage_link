import { SpeakerEvent, SpeakerStatus, TemperatureSample } from '../core/speaker.models';

// Replaces SpeakerApi in component tests. Each method is a spy that tests can
// make succeed or fail.
export class FakeSpeakerApi {
  readonly getStatus = vi.fn(async (_id: string): Promise<SpeakerStatus> => ({
    name: 'Main L',
    gain: -6.5,
    muted: false,
    tilt: 0,
  }));
  readonly setGain = vi.fn(async (_id: string, _gain: number): Promise<void> => {});
  readonly setMute = vi.fn(async (_id: string, _muted: boolean): Promise<void> => {});
  readonly setTilt = vi.fn(async (_id: string, _tilt: number): Promise<void> => {});
  readonly setName = vi.fn(async (_id: string, _name: string): Promise<void> => {});
  readonly getHistory = vi.fn(async (_id: string): Promise<TemperatureSample[]> => []);
  readonly getEvents = vi.fn(async (_id: string): Promise<SpeakerEvent[]> => []);
}
