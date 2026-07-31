import { ChangeDetectionStrategy, Component, Input, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { FleetService } from '../../core/fleet.service';
import { AuthService } from '../../core/auth.service';
import { TelemetryStreamService } from '../../core/telemetry-stream.service';
import { TelemetryReading, Vessel, VesselStatus } from '../../core/models';
import { relativeTime, statusClasses, statusLabel } from '../../core/format';
import { LineChartComponent, Series } from '../../shared/line-chart.component';
import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'app-vessel',
  imports: [RouterLink, LineChartComponent, StatCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <div>
        <a routerLink="/fleet" class="text-xs text-slate-500 hover:text-slate-300">← Back to fleet</a>

        @if (vessel(); as v) {
          <div class="mt-2 flex flex-wrap items-center gap-3">
            <h1 class="text-lg font-semibold text-white">{{ v.name }}</h1>
            <span class="font-mono text-xs text-slate-500">IMO {{ v.imoNumber }}</span>
            <span class="rounded px-2 py-0.5 text-xs" [class]="statusClasses(v.status)">
              {{ statusLabel(v.status) }}
            </span>

            @if (auth.canManageFleet() && v.status !== 'Decommissioned') {
              <select
                class="input ml-auto max-w-[12rem] text-xs"
                [value]="v.status"
                (change)="changeStatus($event)"
              >
                @for (option of statusOptions; track option) {
                  <option [value]="option">{{ statusLabel(option) }}</option>
                }
              </select>
            }
          </div>
        }
      </div>

      @if (error()) {
        <p class="rounded border border-rose-800 bg-rose-950 px-3 py-2 text-sm text-rose-300">{{ error() }}</p>
      }

      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <app-stat-card label="Speed" [value]="latest() ? latest()!.speedOverGroundKn.toFixed(1) + ' kn' : '—'" />
        <app-stat-card
          label="Engine temp"
          [value]="latest() ? latest()!.engineTempC.toFixed(1) + ' °C' : '—'"
          [valueClass]="isHot() ? 'text-rose-400' : 'text-white'"
        />
        <app-stat-card label="Engine RPM" [value]="latest()?.engineRpm ?? '—'" />
        <app-stat-card
          label="Fuel burn"
          [value]="fuelBurn()"
          hint="Litres per nautical mile"
        />
      </div>

      <section class="card">
        <div class="mb-3 flex items-center justify-between">
          <h2 class="text-sm font-semibold text-white">Speed and engine temperature</h2>
          <div class="flex gap-1">
            @for (window of windows; track window) {
              <button
                type="button"
                class="rounded px-2 py-1 text-xs"
                [class]="hours() === window ? 'bg-sea-600 text-white' : 'text-slate-400 hover:text-slate-200'"
                (click)="setWindow(window)"
              >
                {{ window }}h
              </button>
            }
          </div>
        </div>

        @if (readings().length === 0) {
          <p class="py-12 text-center text-sm text-slate-500">
            No telemetry in this window. Enable the simulator or post readings to the API.
          </p>
        } @else {
          <app-line-chart
            [series]="chartSeries()"
            [labels]="chartLabels()"
            secondaryAxisLabel="°C"
          />
          <p class="mt-2 text-xs text-slate-500">
            {{ readings().length }} reading(s) · last {{ relativeTime(latest()?.recordedAtUtc ?? null) }}
          </p>
        }
      </section>

      <section class="card">
        <h2 class="mb-3 text-sm font-semibold text-white">Last known position</h2>
        @if (latest(); as reading) {
          <dl class="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
            <div>
              <dt class="text-xs text-slate-500">Latitude</dt>
              <dd class="font-mono text-slate-200">{{ reading.latitude.toFixed(4) }}</dd>
            </div>
            <div>
              <dt class="text-xs text-slate-500">Longitude</dt>
              <dd class="font-mono text-slate-200">{{ reading.longitude.toFixed(4) }}</dd>
            </div>
            <div>
              <dt class="text-xs text-slate-500">Fuel flow</dt>
              <dd class="font-mono text-slate-200">{{ reading.fuelFlowLitresPerHour.toFixed(0) }} L/h</dd>
            </div>
            <div>
              <dt class="text-xs text-slate-500">Recorded</dt>
              <dd class="text-slate-200">{{ relativeTime(reading.recordedAtUtc) }}</dd>
            </div>
          </dl>
        } @else {
          <p class="text-sm text-slate-500">No position reported.</p>
        }
      </section>
    </div>
  `,
})
export class VesselComponent implements OnInit {
  /** Bound from the :id route parameter via withComponentInputBinding(). */
  @Input({ required: true }) id = '';

  private readonly fleet = inject(FleetService);
  private readonly stream = inject(TelemetryStreamService);
  protected readonly auth = inject(AuthService);

  protected readonly statusClasses = statusClasses;
  protected readonly statusLabel = statusLabel;
  protected readonly relativeTime = relativeTime;

  protected readonly windows = [1, 6, 24];
  protected readonly statusOptions: VesselStatus[] = [
    'UnderWay', 'InPort', 'AtAnchor', 'Maintenance',
  ];

  protected readonly vessel = signal<Vessel | null>(null);
  protected readonly readings = signal<TelemetryReading[]>([]);
  protected readonly hours = signal(6);
  protected readonly error = signal<string | null>(null);

  /** The socket's latest value wins over the last polled reading. */
  protected readonly latest = computed<TelemetryReading | null>(() => {
    const live = this.stream.latestByVessel()[this.id];
    if (live) {
      return live;
    }
    const history = this.readings();
    return history.length > 0 ? history[history.length - 1] : null;
  });

  protected readonly isHot = computed(() => (this.latest()?.engineTempC ?? 0) >= 85);

  protected readonly fuelBurn = computed(() => {
    const burn = this.latest()?.fuelPerNauticalMile;
    return burn === null || burn === undefined ? '— (stationary)' : `${burn.toFixed(1)} L/nm`;
  });

  protected readonly chartLabels = computed(() =>
    this.readings().map((r) => new Date(r.recordedAtUtc).toLocaleTimeString()),
  );

  protected readonly chartSeries = computed<Series[]>(() => {
    const points = this.readings();

    return [
      {
        label: 'Speed (kn)',
        colour: '#38bdf8',
        values: points.map((r) => r.speedOverGroundKn),
        yAxis: 'y',
      },
      {
        label: 'Engine temp (°C)',
        colour: '#fb923c',
        values: points.map((r) => r.engineTempC),
        yAxis: 'y1',
      },
    ];
  });

  ngOnInit(): void {
    void this.stream.start().then(() => this.stream.subscribeToVessel(this.id));
    this.loadVessel();
    this.loadTelemetry();
  }

  protected setWindow(hours: number): void {
    this.hours.set(hours);
    this.loadTelemetry();
  }

  protected changeStatus(event: Event): void {
    const status = (event.target as HTMLSelectElement).value as VesselStatus;

    this.fleet.changeStatus(this.id, status).subscribe({
      next: (updated) => this.vessel.set(updated),
      error: () => this.error.set('Could not change the vessel status.'),
    });
  }

  private loadVessel(): void {
    this.fleet.getVessel(this.id).subscribe({
      next: (vessel) => this.vessel.set(vessel),
      error: () => this.error.set('Could not load that vessel.'),
    });
  }

  private loadTelemetry(): void {
    this.fleet.getTelemetry(this.id, this.hours()).subscribe({
      next: (readings) => this.readings.set(readings),
      error: () => this.error.set('Could not load telemetry.'),
    });
  }
}
