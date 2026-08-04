import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { FleetService } from '../../core/fleet.service';
import { TelemetryStreamService } from '../../core/telemetry-stream.service';
import { AuthService } from '../../core/auth.service';
import { Anomaly, FleetHealth, VesselSummary } from '../../core/models';
import { relativeTime, severityClasses, statusClasses, statusLabel } from '../../core/format';
import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, StatCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <div class="flex items-baseline justify-between">
        <h1 class="text-lg font-semibold text-white">Fleet overview</h1>
        <button type="button" class="btn-ghost text-xs" (click)="refresh()" [disabled]="loading()">
          {{ loading() ? 'Refreshing…' : 'Refresh' }}
        </button>
      </div>

      @if (error()) {
        <p class="rounded border border-rose-800 bg-rose-950 px-3 py-2 text-sm text-rose-300">
          {{ error() }}
        </p>
      }

      <!--
        Two columns even on the narrowest phone. Stacking four cards vertically pushed
        the fleet table roughly 400px down the page, so the first thing you saw on a
        phone was a column of numbers and no context.
      -->
      <div class="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
        <app-stat-card label="Vessels tracked" [value]="health()?.totalVessels ?? '—'" />
        <app-stat-card
          label="Under way"
          [value]="health()?.underWay ?? '—'"
          valueClass="text-emerald-400"
          [hint]="(health()?.inPort ?? 0) + ' in port'"
        />
        <app-stat-card
          label="Open anomalies"
          [value]="health()?.openAnomalies ?? '—'"
          [valueClass]="(health()?.openAnomalies ?? 0) > 0 ? 'text-amber-400' : 'text-white'"
          [hint]="(health()?.criticalAnomalies ?? 0) + ' critical'"
        />
        <app-stat-card
          label="Avg speed (1h)"
          [value]="(health()?.averageSpeedKn ?? 0) + ' kn'"
          [hint]="'Avg engine ' + (health()?.averageEngineTempC ?? 0) + ' °C'"
        />
      </div>

      <div class="grid gap-6 lg:grid-cols-3">
        <section class="card lg:col-span-2">
          <h2 class="mb-3 text-sm font-semibold text-white">Fleet status</h2>

          @if (vessels().length === 0 && !loading()) {
            <p class="py-8 text-center text-sm text-slate-500">No vessels registered yet.</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full">
                <thead class="border-b border-ink-600">
                  <tr>
                    <th class="th">Vessel</th>
                    <th class="th">Status</th>
                    <th class="th">Speed</th>
                    <th class="th">Engine</th>
                    <th class="th">Last report</th>
                    <th class="th">Alerts</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-ink-700">
                  @for (vessel of vessels(); track vessel.id) {
                    <tr class="hover:bg-ink-700/40">
                      <td class="td">
                        <a [routerLink]="['/vessels', vessel.id]" class="font-medium text-sea-400 hover:underline">
                          {{ vessel.name }}
                        </a>
                        <span class="ml-2 font-mono text-xs text-slate-500">{{ vessel.imoNumber }}</span>
                      </td>
                      <td class="td">
                        <span class="rounded px-2 py-0.5 text-xs" [class]="statusClasses(vessel.status)">
                          {{ statusLabel(vessel.status) }}
                        </span>
                      </td>
                      <td class="td font-mono">{{ liveSpeed(vessel) }}</td>
                      <td class="td font-mono" [class.text-rose-400]="isHot(vessel)">
                        {{ liveTemp(vessel) }}
                      </td>
                      <td class="td text-slate-400">{{ relativeTime(vessel.lastReportedAtUtc) }}</td>
                      <td class="td">
                        @if (vessel.openAnomalyCount > 0) {
                          <span class="rounded bg-amber-900 px-2 py-0.5 text-xs text-amber-300">
                            {{ vessel.openAnomalyCount }}
                          </span>
                        } @else {
                          <span class="text-slate-600">—</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>

        <section class="card">
          <div class="mb-3 flex items-center justify-between">
            <h2 class="text-sm font-semibold text-white">Open alerts</h2>
            <span class="text-xs text-slate-500">{{ anomalies().length }}</span>
          </div>

          @if (anomalies().length === 0) {
            <p class="py-8 text-center text-sm text-slate-500">Nothing needs attention.</p>
          } @else {
            <ul class="space-y-2">
              @for (anomaly of anomalies(); track anomaly.id) {
                <li class="rounded border border-ink-600 p-2.5">
                  <div class="flex items-start justify-between gap-2">
                    <span class="rounded px-1.5 py-0.5 text-[10px] uppercase" [class]="severityClasses(anomaly.severity)">
                      {{ anomaly.severity }}
                    </span>
                    <span class="text-[11px] text-slate-500">{{ relativeTime(anomaly.detectedAtUtc) }}</span>
                  </div>
                  <p class="mt-1.5 text-xs font-medium text-slate-200">{{ anomaly.vesselName }}</p>
                  <p class="mt-0.5 text-xs leading-relaxed text-slate-400">{{ anomaly.detail }}</p>

                  @if (auth.canManageFleet()) {
                    <button
                      type="button"
                      class="btn-ghost mt-2 w-full px-2 py-2 text-[11px] sm:py-1"
                      (click)="acknowledge(anomaly)"
                    >
                      Acknowledge
                    </button>
                  }
                </li>
              }
            </ul>
          }
        </section>
      </div>
    </div>
  `,
})
export class DashboardComponent implements OnInit {
  private readonly fleet = inject(FleetService);
  protected readonly stream = inject(TelemetryStreamService);
  protected readonly auth = inject(AuthService);

  protected readonly statusClasses = statusClasses;
  protected readonly statusLabel = statusLabel;
  protected readonly severityClasses = severityClasses;
  protected readonly relativeTime = relativeTime;

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly health = signal<FleetHealth | null>(null);
  protected readonly vessels = signal<VesselSummary[]>([]);
  private readonly polledAnomalies = signal<Anomaly[]>([]);

  /**
   * Live pushes are merged over the polled snapshot and de-duplicated, so the panel
   * updates instantly but still recovers if the socket was down during a refresh.
   */
  protected readonly anomalies = computed(() => {
    const merged = [...this.stream.liveAnomalies(), ...this.polledAnomalies()];
    const seen = new Set<string>();
    return merged.filter((a) => (seen.has(a.id) ? false : seen.add(a.id))).slice(0, 20);
  });

  ngOnInit(): void {
    void this.stream.start();
    this.refresh();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.error.set(null);

    this.fleet.getFleetHealth().subscribe({
      next: (health) => this.health.set(health),
      error: () => this.error.set('Could not load fleet health.'),
    });

    this.fleet.searchVessels({ pageSize: 50 }).subscribe({
      next: (page) => {
        this.vessels.set(page.items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load the vessel list.');
        this.loading.set(false);
      },
    });

    this.fleet.getOpenAnomalies(20).subscribe({
      next: (anomalies) => this.polledAnomalies.set(anomalies),
      error: () => undefined,
    });
  }

  protected acknowledge(anomaly: Anomaly): void {
    this.fleet.acknowledgeAnomaly(anomaly.id).subscribe({
      next: () => {
        this.polledAnomalies.update((current) => current.filter((a) => a.id !== anomaly.id));
        this.stream.liveAnomalies.update((current) => current.filter((a) => a.id !== anomaly.id));
      },
      error: () => this.error.set('Could not acknowledge that alert.'),
    });
  }

  /** Prefers the live socket value, falling back to whatever the last poll returned. */
  protected liveSpeed(vessel: VesselSummary): string {
    const live = this.stream.latestByVessel()[vessel.id];
    const speed = live?.speedOverGroundKn ?? vessel.lastSpeedKn;
    return speed === null || speed === undefined ? '—' : `${speed.toFixed(1)} kn`;
  }

  protected liveTemp(vessel: VesselSummary): string {
    const temp = this.currentTemp(vessel);
    return temp === null ? '—' : `${temp.toFixed(1)} °C`;
  }

  protected isHot(vessel: VesselSummary): boolean {
    const temp = this.currentTemp(vessel);
    return temp !== null && temp >= 85;
  }

  private currentTemp(vessel: VesselSummary): number | null {
    const live = this.stream.latestByVessel()[vessel.id];
    return live?.engineTempC ?? vessel.lastEngineTempC ?? null;
  }
}
