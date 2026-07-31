import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { FleetService } from '../../core/fleet.service';
import { AuthService } from '../../core/auth.service';
import { CargoManifest, VesselSummary } from '../../core/models';
import { manifestStatusClasses, relativeTime } from '../../core/format';

@Component({
  selector: 'app-manifests',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <h1 class="text-lg font-semibold text-white">Cargo manifests</h1>

      @if (auth.canManageFleet()) {
        <section class="card">
          <h2 class="mb-3 text-sm font-semibold text-white">Upload a manifest</h2>

          <div class="grid gap-3 sm:grid-cols-4">
            <div class="sm:col-span-1">
              <label class="label" for="vessel">Vessel</label>
              <select
                id="vessel"
                class="input"
                [ngModel]="vesselId()"
                (ngModelChange)="vesselId.set($event)"
              >
                <option value="">Select…</option>
                @for (vessel of vessels(); track vessel.id) {
                  <option [value]="vessel.id">{{ vessel.name }}</option>
                }
              </select>
            </div>

            <div class="sm:col-span-1">
              <label class="label" for="voyage">Voyage number</label>
              <input
                id="voyage"
                class="input"
                placeholder="V-2026-014"
                [ngModel]="voyageNumber()"
                (ngModelChange)="voyageNumber.set($event)"
              />
            </div>

            <div class="sm:col-span-2">
              <label class="label" for="file">CSV file</label>
              <input id="file" type="file" accept=".csv,text/csv" class="input" (change)="onFileSelected($event)" />
            </div>
          </div>

          <div class="mt-3 flex items-center gap-3">
            <button type="button" class="btn-primary" [disabled]="!canUpload() || uploading()" (click)="upload()">
              {{ uploading() ? 'Uploading…' : 'Upload and ingest' }}
            </button>

            <p class="text-xs text-slate-500">
              Large files should be dropped straight into the S3 bucket under
              <code class="text-slate-400">incoming/&#123;IMO&#125;/&#123;VOYAGE&#125;.csv</code>
              — the Lambda picks those up without tying up an API worker.
            </p>
          </div>

          @if (message()) {
            <p class="mt-3 rounded border px-3 py-2 text-sm" [class]="messageClass()">{{ message() }}</p>
          }
        </section>
      }

      <section class="card overflow-x-auto p-0">
        <table class="w-full">
          <thead class="border-b border-ink-600">
            <tr>
              <th class="th">Voyage</th>
              <th class="th">Status</th>
              <th class="th">Containers</th>
              <th class="th">Total weight</th>
              <th class="th">Hazardous</th>
              <th class="th">Errors</th>
              <th class="th">Received</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-ink-700">
            @for (manifest of manifests(); track manifest.id) {
              <tr class="align-top hover:bg-ink-700/40">
                <td class="td font-medium">{{ manifest.voyageNumber }}</td>
                <td class="td">
                  <span class="rounded px-2 py-0.5 text-xs" [class]="manifestStatusClasses(manifest.status)">
                    {{ manifest.status }}
                  </span>
                </td>
                <td class="td font-mono">{{ manifest.lineItemCount }}</td>
                <td class="td font-mono">{{ (manifest.totalGrossWeightKg / 1000).toFixed(1) }} t</td>
                <td class="td font-mono">{{ manifest.hazardousCount || '—' }}</td>
                <td class="td">
                  @if (manifest.validationErrors.length === 0) {
                    <span class="text-slate-600">none</span>
                  } @else {
                    <details>
                      <summary class="cursor-pointer text-amber-400">
                        {{ manifest.validationErrors.length }}
                      </summary>
                      <ul class="mt-1 max-w-md space-y-0.5 whitespace-normal text-xs text-slate-400">
                        @for (issue of manifest.validationErrors; track issue) {
                          <li>{{ issue }}</li>
                        }
                      </ul>
                    </details>
                  }
                </td>
                <td class="td text-slate-400">{{ relativeTime(manifest.receivedAtUtc) }}</td>
              </tr>
            } @empty {
              <tr>
                <td class="td py-10 text-center text-slate-500" colspan="7">No manifests ingested yet.</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    </div>
  `,
})
export class ManifestsComponent implements OnInit {
  private readonly fleet = inject(FleetService);
  protected readonly auth = inject(AuthService);

  protected readonly manifestStatusClasses = manifestStatusClasses;
  protected readonly relativeTime = relativeTime;

  protected readonly manifests = signal<CargoManifest[]>([]);
  protected readonly vessels = signal<VesselSummary[]>([]);
  protected readonly uploading = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly failed = signal(false);

  protected readonly vesselId = signal('');
  protected readonly voyageNumber = signal('');
  private readonly file = signal<File | null>(null);

  ngOnInit(): void {
    this.load();
    this.fleet.searchVessels({ pageSize: 100 }).subscribe({
      next: (page) => this.vessels.set(page.items),
      error: () => undefined,
    });
  }

  /** Computed so the submit button's disabled state tracks the signals under zoneless CD. */
  protected readonly canUpload = computed(
    () => this.file() !== null && this.vesselId() !== '' && this.voyageNumber().trim().length > 0,
  );

  protected messageClass(): string {
    return this.failed()
      ? 'border-rose-800 bg-rose-950 text-rose-300'
      : 'border-emerald-800 bg-emerald-950 text-emerald-300';
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
  }

  protected upload(): void {
    const file = this.file();
    if (file === null || !this.canUpload()) {
      return;
    }

    this.uploading.set(true);
    this.message.set(null);

    this.fleet.uploadManifest(file, this.voyageNumber().trim(), this.vesselId()).subscribe({
      next: (manifest) => {
        this.uploading.set(false);
        this.failed.set(false);
        this.message.set(
          `${manifest.voyageNumber}: ${manifest.status} — ${manifest.lineItemCount} container(s), ` +
            `${manifest.validationErrors.length} error(s).`,
        );
        this.load();
      },
      error: () => {
        this.uploading.set(false);
        this.failed.set(true);
        this.message.set('Upload failed. Check the file format and that the API can reach S3.');
      },
    });
  }

  private load(): void {
    this.fleet.getManifests(undefined, 50).subscribe({
      next: (manifests) => this.manifests.set(manifests),
      error: () => undefined,
    });
  }
}
