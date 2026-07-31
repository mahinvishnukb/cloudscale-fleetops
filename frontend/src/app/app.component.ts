import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth.service';
import { TelemetryStreamService } from './core/telemetry-stream.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex min-h-full flex-col">
      @if (auth.isAuthenticated()) {
        <header class="border-b border-ink-700 bg-ink-800">
          <div class="mx-auto flex max-w-7xl items-center gap-6 px-4 py-3">
            <a routerLink="/dashboard" class="flex items-center gap-2 text-sm font-semibold text-white">
              <span class="grid h-7 w-7 place-items-center rounded bg-sea-600 text-xs">FO</span>
              CloudScale FleetOps
            </a>

            <nav class="flex items-center gap-1 text-sm">
              @for (link of navLinks; track link.path) {
                <a
                  [routerLink]="link.path"
                  routerLinkActive="bg-ink-700 text-white"
                  class="rounded px-3 py-1.5 text-slate-400 hover:text-slate-200"
                >
                  {{ link.label }}
                </a>
              }
            </nav>

            <div class="ml-auto flex items-center gap-3 text-xs">
              <span class="flex items-center gap-1.5" [title]="'Live feed: ' + stream.status()">
                <span class="h-2 w-2 rounded-full" [class]="streamDot()"></span>
                <span class="text-slate-400">{{ stream.status() }}</span>
              </span>

              <span class="text-slate-400">
                {{ auth.username() }}
                <span class="text-slate-600">·</span>
                {{ auth.role() }}
              </span>

              <button type="button" class="btn-ghost px-2 py-1 text-xs" (click)="auth.logout()">
                Sign out
              </button>
            </div>
          </div>
        </header>
      }

      <main class="mx-auto w-full max-w-7xl flex-1 px-4 py-6">
        <router-outlet />
      </main>

      <footer class="border-t border-ink-700 px-4 py-3 text-center text-xs text-slate-600">
        CloudScale FleetOps — Angular 22 · .NET 8 · AWS
      </footer>
    </div>
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
  protected readonly stream = inject(TelemetryStreamService);

  protected readonly navLinks = [
    { path: '/dashboard', label: 'Overview' },
    { path: '/fleet', label: 'Fleet' },
    { path: '/manifests', label: 'Manifests' },
  ];

  protected readonly streamDot = computed(() => {
    switch (this.stream.status()) {
      case 'connected':
        return 'bg-emerald-400';
      case 'connecting':
      case 'reconnecting':
        return 'bg-amber-400 animate-pulse';
      default:
        return 'bg-slate-600';
    }
  });
}
