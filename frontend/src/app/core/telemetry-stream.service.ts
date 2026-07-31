import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { Anomaly, TelemetryReading } from './models';

export type StreamStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/**
 * Live telemetry over SignalR.
 *
 * Latest values are exposed as signals so templates re-render without manual
 * change detection, and the raw stream never needs an async pipe.
 */
@Injectable({ providedIn: 'root' })
export class TelemetryStreamService {
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  private connection: HubConnection | null = null;

  readonly status = signal<StreamStatus>('disconnected');
  readonly latestByVessel = signal<Record<string, TelemetryReading>>({});
  readonly liveAnomalies = signal<Anomaly[]>([]);

  constructor() {
    this.destroyRef.onDestroy(() => void this.stop());
  }

  async start(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    const token = this.auth.token();
    if (!token) {
      return;
    }

    this.status.set('connecting');

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/telemetry`, {
        // Browsers cannot set headers on the WebSocket handshake, so the token
        // travels as a query-string parameter the API knows to read.
        accessTokenFactory: () => this.auth.token() ?? '',
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    this.connection.on('FleetTelemetryReceived', (reading: TelemetryReading) => {
      this.latestByVessel.update((current) => ({ ...current, [reading.vesselId]: reading }));
    });

    this.connection.on('AnomalyRaised', (anomaly: Anomaly) => {
      // Keep the newest 25; the alerts panel is a live tail, not a history view.
      this.liveAnomalies.update((current) => [anomaly, ...current].slice(0, 25));
    });

    this.connection.onreconnecting(() => this.status.set('reconnecting'));
    this.connection.onreconnected(() => this.status.set('connected'));
    this.connection.onclose(() => this.status.set('disconnected'));

    try {
      await this.connection.start();
      this.status.set('connected');
    } catch (error) {
      // The dashboard still works on polled data; the live feed is an enhancement.
      console.warn('Telemetry stream unavailable; falling back to polling.', error);
      this.status.set('disconnected');
    }
  }

  async subscribeToVessel(vesselId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('SubscribeToVessel', vesselId);
    }
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
    this.status.set('disconnected');
  }
}
