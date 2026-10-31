import { InjectionToken } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';

// The parts of a SignalR connection the app uses. Tests provide a fake with the
// same shape, so they don't need a running backend.
export type HubConnectionLike = Pick<
  HubConnection,
  'on' | 'start' | 'invoke' | 'onreconnecting' | 'onreconnected' | 'onclose'
>;

// How to create the connection. The real one talks to /hubs/speakers (through
// the dev proxy) and reconnects by itself after short drops.
export const HUB_CONNECTION_FACTORY = new InjectionToken<() => HubConnectionLike>('HUB_CONNECTION_FACTORY', {
  providedIn: 'root',
  factory: () => () => new HubConnectionBuilder().withUrl('/hubs/speakers').withAutomaticReconnect().build(),
});
