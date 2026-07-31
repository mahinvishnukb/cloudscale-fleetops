import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth.service';
import { TelemetryStreamService } from '../../core/telemetry-stream.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto mt-12 max-w-sm">
      <div class="mb-6 text-center">
        <div class="mx-auto mb-3 grid h-12 w-12 place-items-center rounded-lg bg-sea-600 font-semibold text-white">
          FO
        </div>
        <h1 class="text-xl font-semibold text-white">CloudScale FleetOps</h1>
        <p class="mt-1 text-sm text-slate-400">Sign in to the fleet operations console</p>
      </div>

      <form class="card space-y-4" [formGroup]="form" (ngSubmit)="submit()">
        <div>
          <label class="label" for="username">Username</label>
          <input id="username" type="text" class="input" formControlName="username" autocomplete="username" />
        </div>

        <div>
          <label class="label" for="password">Password</label>
          <input id="password" type="password" class="input" formControlName="password" autocomplete="current-password" />
        </div>

        @if (error()) {
          <p class="rounded border border-rose-800 bg-rose-950 px-3 py-2 text-sm text-rose-300">
            {{ error() }}
          </p>
        }

        <button type="submit" class="btn-primary w-full" [disabled]="form.invalid || busy()">
          {{ busy() ? 'Signing in…' : 'Sign in' }}
        </button>

        <p class="text-center text-xs text-slate-500">
          Demo accounts: <code class="text-slate-400">admin</code>,
          <code class="text-slate-400">manager</code>,
          <code class="text-slate-400">analyst</code>
          — password from <code>DEMO_PASSWORD</code> in your .env
        </p>
      </form>

      <p class="mt-4 text-center text-xs text-slate-600">
        The API sleeps when idle on the free tier; the first sign-in can take up to a minute.
      </p>
    </div>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly stream = inject(TelemetryStreamService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    username: ['admin', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const { username, password } = this.form.getRawValue();

    this.auth.login(username, password).subscribe({
      next: () => {
        void this.stream.start();
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (err: { status?: number }) => {
        this.busy.set(false);
        this.error.set(
          err.status === 0
            ? 'Cannot reach the API. If it is deployed on a free tier it may still be waking up — try again in a moment.'
            : 'Invalid username or password.',
        );
      },
    });
  }
}
