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
        <!--
          Two rows on a phone, one row from 640px up. Everything here used to sit in a
          single non-wrapping flex row: brand, three nav links, connection status,
          username, role and a button came to roughly 650px of content, which forced a
          horizontal scrollbar on a 375px screen.

          The nav is ordered last and given full width below the sm breakpoint, so it
          drops onto its own line; secondary text (status word, role) is hidden at the
          narrowest sizes, its meaning carried by the coloured dot and the tooltip.

          Note for future edits: this is a TypeScript template literal, so a backtick
          anywhere in here — including inside an HTML comment — ends the string and
          produces a wall of unrelated syntax errors.
        -->
        <header class="border-b border-ink-700 bg-ink-800">
          <div class="mx-auto flex max-w-7xl flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 sm:gap-x-6">
            <a
              routerLink="/dashboard"
              class="flex shrink-0 items-center gap-2 text-sm font-semibold text-white"
            >
              <span class="grid h-7 w-7 place-items-center rounded bg-sea-600 text-xs">FO</span>
              <span class="hidden xs:inline">CloudScale FleetOps</span>
              <span class="xs:hidden">FleetOps</span>
            </a>

            <nav
              class="order-last -mx-1 flex w-full items-center gap-1 overflow-x-auto text-sm
                     sm:order-none sm:mx-0 sm:w-auto sm:overflow-visible"
            >
              @for (link of navLinks; track link.path) {
                <a
                  [routerLink]="link.path"
                  routerLinkActive="bg-ink-700 text-white"
                  class="shrink-0 rounded px-3 py-2 text-slate-400 hover:text-slate-200 sm:py-1.5"
                >
                  {{ link.label }}
                </a>
              }
            </nav>

            <div class="ml-auto flex items-center gap-2 text-xs sm:gap-3">
              <span class="flex items-center gap-1.5" [title]="'Live feed: ' + stream.status()">
                <span class="h-2 w-2 shrink-0 rounded-full" [class]="streamDot()"></span>
                <span class="hidden text-slate-400 sm:inline">{{ stream.status() }}</span>
              </span>

              <span class="truncate text-slate-400">
                {{ auth.username() }}
                <span class="hidden md:inline">
                  <span class="text-slate-600">·</span>
                  {{ auth.role() }}
                </span>
              </span>

              <button
                type="button"
                class="btn-ghost shrink-0 px-3 py-2 text-xs sm:px-2 sm:py-1"
                (click)="auth.logout()"
              >
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
