import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { FleetService } from '../../core/fleet.service';
import { AuthService } from '../../core/auth.service';
import { PagedResult, VesselStatus, VesselSummary } from '../../core/models';
import { relativeTime, statusClasses, statusLabel } from '../../core/format';

type SortKey = 'name' | 'status' | 'type' | 'grossTonnage' | 'homePort' | 'openAnomalyCount';

@Component({
  selector: 'app-fleet',
  imports: [RouterLink, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      <div class="flex flex-wrap items-center gap-3">
        <h1 class="text-lg font-semibold text-white">Fleet</h1>

        <input
          type="search"
          class="input max-w-xs"
          placeholder="Search name, IMO or port…"
          [ngModel]="search()"
          (ngModelChange)="onSearchChange($event)"
        />

        <select class="input max-w-[10rem]" [ngModel]="status()" (ngModelChange)="onStatusChange($event)">
          <option value="">All statuses</option>
          @for (option of statusOptions; track option) {
            <option [value]="option">{{ statusLabel(option) }}</option>
          }
        </select>

        <span class="ml-auto text-xs text-slate-500">
          {{ result()?.totalCount ?? 0 }} vessel(s)
        </span>
      </div>

      <div class="card overflow-x-auto p-0">
        <table class="w-full">
          <thead class="border-b border-ink-600">
            <tr>
              @for (column of columns; track column.label) {
                <th class="th cursor-pointer select-none hover:text-slate-200" (click)="sortBy(column.key)">
                  {{ column.label }}
                  @if (sortKey() === column.key) {
                    <span class="ml-1 text-sea-400">{{ sortAsc() ? '▲' : '▼' }}</span>
                  }
                </th>
              }
              <th class="th">Last report</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-ink-700">
            @for (vessel of sorted(); track vessel.id) {
              <tr class="hover:bg-ink-700/40">
                <td class="td">
                  <a [routerLink]="['/vessels', vessel.id]" class="font-medium text-sea-400 hover:underline">
                    {{ vessel.name }}
                  </a>
                  <div class="font-mono text-xs text-slate-500">IMO {{ vessel.imoNumber }}</div>
                </td>
                <td class="td">
                  <span class="rounded px-2 py-0.5 text-xs" [class]="statusClasses(vessel.status)">
                    {{ statusLabel(vessel.status) }}
                  </span>
                </td>
                <td class="td text-slate-400">{{ vessel.type }}</td>
                <td class="td font-mono">{{ vessel.grossTonnage.toLocaleString() }}</td>
                <td class="td text-slate-400">{{ vessel.homePort }}</td>
                <td class="td">
                  @if (vessel.openAnomalyCount > 0) {
                    <span class="rounded bg-amber-900 px-2 py-0.5 text-xs text-amber-300">
                      {{ vessel.openAnomalyCount }}
                    </span>
                  } @else {
                    <span class="text-slate-600">—</span>
                  }
                </td>
                <td class="td text-slate-400">{{ relativeTime(vessel.lastReportedAtUtc) }}</td>
              </tr>
            } @empty {
              <tr>
                <td class="td py-10 text-center text-slate-500" colspan="7">
                  No vessels match those filters.
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      @if ((result()?.totalPages ?? 0) > 1) {
        <div class="flex items-center justify-center gap-2 text-sm">
          <button class="btn-ghost" [disabled]="page() === 1" (click)="goTo(page() - 1)">Previous</button>
          <span class="text-slate-400">Page {{ page() }} of {{ result()?.totalPages }}</span>
          <button class="btn-ghost" [disabled]="!result()?.hasNextPage" (click)="goTo(page() + 1)">Next</button>
        </div>
      }
    </div>
  `,
})
export class FleetComponent implements OnInit {
  private readonly fleet = inject(FleetService);
  protected readonly auth = inject(AuthService);

  protected readonly statusClasses = statusClasses;
  protected readonly statusLabel = statusLabel;
  protected readonly relativeTime = relativeTime;

  protected readonly statusOptions: VesselStatus[] = [
    'UnderWay', 'InPort', 'AtAnchor', 'Maintenance', 'Decommissioned',
  ];

  protected readonly columns: { key: SortKey; label: string }[] = [
    { key: 'name', label: 'Vessel' },
    { key: 'status', label: 'Status' },
    { key: 'type', label: 'Type' },
    { key: 'grossTonnage', label: 'GT' },
    { key: 'homePort', label: 'Home port' },
    { key: 'openAnomalyCount', label: 'Alerts' },
  ];

  protected readonly search = signal('');
  protected readonly status = signal<VesselStatus | ''>('');
  protected readonly page = signal(1);
  protected readonly result = signal<PagedResult<VesselSummary> | null>(null);
  protected readonly sortKey = signal<SortKey>('name');
  protected readonly sortAsc = signal(true);

  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.load();
  }

  /** Client-side sort: the page is already in memory, so a round trip would be wasteful. */
  protected sorted(): VesselSummary[] {
    const items = [...(this.result()?.items ?? [])];
    const key = this.sortKey();
    const direction = this.sortAsc() ? 1 : -1;

    return items.sort((a, b) => {
      const left = a[key];
      const right = b[key];

      if (typeof left === 'number' && typeof right === 'number') {
        return (left - right) * direction;
      }

      return String(left).localeCompare(String(right)) * direction;
    });
  }

  protected sortBy(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortAsc.update((asc) => !asc);
    } else {
      this.sortKey.set(key);
      this.sortAsc.set(true);
    }
  }

  protected onSearchChange(value: string): void {
    this.search.set(value);
    // Debounce so typing does not fire a request per keystroke.
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.page.set(1);
      this.load();
    }, 300);
  }

  protected onStatusChange(value: VesselStatus | ''): void {
    this.status.set(value);
    this.page.set(1);
    this.load();
  }

  protected goTo(page: number): void {
    this.page.set(page);
    this.load();
  }

  private load(): void {
    this.fleet
      .searchVessels({
        search: this.search() || undefined,
        status: this.status() || null,
        page: this.page(),
        pageSize: 20,
      })
      .subscribe({
        next: (result) => this.result.set(result),
        error: () => this.result.set(null),
      });
  }
}
