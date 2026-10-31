import { HubConnectionLike } from '../core/hub-connection';

// A stand-in for a SignalR connection that tests drive by hand:
// emit('SpeakersChanged', [...]) pretends the server sent a message.
export class FakeHubConnection implements HubConnectionLike {
  startCalls = 0;
  failNextStart = false;
  invokeError: Error | null = null;
  readonly invoked: { method: string; args: unknown[] }[] = [];

  private readonly handlers = new Map<string, ((...args: any[]) => void)[]>();
  private reconnectingCallback?: () => void;
  private reconnectedCallback?: () => void;
  private closeCallback?: () => void;

  on(method: string, handler: (...args: any[]) => void): void {
    this.handlers.set(method, [...(this.handlers.get(method) ?? []), handler]);
  }

  async start(): Promise<void> {
    this.startCalls++;
    if (this.failNextStart) {
      this.failNextStart = false;
      throw new Error('Backend not reachable');
    }
  }

  async invoke(method: string, ...args: unknown[]): Promise<any> {
    this.invoked.push({ method, args });
    if (this.invokeError) {
      throw this.invokeError;
    }
  }

  onreconnecting(callback: () => void): void {
    this.reconnectingCallback = callback;
  }

  onreconnected(callback: () => void): void {
    this.reconnectedCallback = callback;
  }

  onclose(callback: () => void): void {
    this.closeCallback = callback;
  }

  // Test helpers

  emit(method: string, ...args: unknown[]): void {
    this.handlers.get(method)?.forEach((handler) => handler(...args));
  }

  simulateReconnecting(): void {
    this.reconnectingCallback?.();
  }

  simulateReconnected(): void {
    this.reconnectedCallback?.();
  }

  simulateClose(): void {
    this.closeCallback?.();
  }
}
