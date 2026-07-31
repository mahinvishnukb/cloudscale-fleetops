import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { environment } from '../../environments/environment';
import { AuthResponse } from './models';

interface StoredSession {
  token: string;
  expiresAtUtc: string;
  username: string;
  role: string;
}

const SESSION_KEY = 'fleetops.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<StoredSession | null>(this.restore());

  readonly username = computed(() => this.session()?.username ?? null);
  readonly role = computed(() => this.session()?.role ?? null);
  readonly isAuthenticated = computed(() => {
    const current = this.session();
    if (!current) {
      return false;
    }
    return new Date(current.expiresAtUtc).getTime() > Date.now();
  });

  /** Fleet managers and administrators may mutate; analysts are read-only. */
  readonly canManageFleet = computed(() => {
    const role = this.role();
    return role === 'Administrator' || role === 'FleetManager';
  });

  login(username: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/api/auth/login`, { username, password })
      .pipe(tap((response) => this.persist(response)));
  }

  logout(): void {
    this.session.set(null);
    localStorage.removeItem(SESSION_KEY);
    void this.router.navigate(['/login']);
  }

  token(): string | null {
    return this.isAuthenticated() ? (this.session()?.token ?? null) : null;
  }

  private persist(response: AuthResponse): void {
    const stored: StoredSession = {
      token: response.token,
      expiresAtUtc: response.expiresAtUtc,
      username: response.username,
      role: response.role,
    };

    this.session.set(stored);
    localStorage.setItem(SESSION_KEY, JSON.stringify(stored));
  }

  private restore(): StoredSession | null {
    const raw = localStorage.getItem(SESSION_KEY);
    if (!raw) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as StoredSession;
      // Drop an expired session on load rather than letting the first request 401.
      return new Date(parsed.expiresAtUtc).getTime() > Date.now() ? parsed : null;
    } catch {
      localStorage.removeItem(SESSION_KEY);
      return null;
    }
  }
}
