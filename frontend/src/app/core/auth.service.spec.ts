import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from './auth.service';
import { AuthResponse } from './models';

describe('AuthService', () => {
  const hourFromNow = () => new Date(Date.now() + 3_600_000).toISOString();
  const post = vi.fn();
  const navigate = vi.fn();

  function makeService(): AuthService {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: HttpClient, useValue: { post } },
        { provide: Router, useValue: { navigate, createUrlTree: vi.fn() } },
      ],
    });
    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    localStorage.clear();
    post.mockReset();
    navigate.mockReset();
    TestBed.resetTestingModule();
  });

  afterEach(() => localStorage.clear());

  it('starts unauthenticated', () => {
    expect(makeService().isAuthenticated()).toBe(false);
  });

  it('stores the session after a successful login', () => {
    const response: AuthResponse = {
      token: 'jwt-token',
      expiresAtUtc: hourFromNow(),
      username: 'admin',
      role: 'Administrator',
    };
    post.mockReturnValue(of(response));

    const auth = makeService();
    auth.login('admin', 'password').subscribe();

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.username()).toBe('admin');
    expect(auth.token()).toBe('jwt-token');
  });

  it('treats an expired token as signed out', () => {
    post.mockReturnValue(
      of({
        token: 'stale',
        expiresAtUtc: new Date(Date.now() - 1_000).toISOString(),
        username: 'admin',
        role: 'Administrator',
      }),
    );

    const auth = makeService();
    auth.login('admin', 'password').subscribe();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token()).toBeNull();
  });

  it('grants management rights to managers and admins but not analysts', () => {
    const roleGrants = (role: string) => {
      TestBed.resetTestingModule();
      localStorage.clear();
      post.mockReturnValue(of({ token: 't', expiresAtUtc: hourFromNow(), username: 'u', role }));
      const auth = makeService();
      auth.login('u', 'p').subscribe();
      return auth.canManageFleet();
    };

    expect(roleGrants('Administrator')).toBe(true);
    expect(roleGrants('FleetManager')).toBe(true);
    expect(roleGrants('Analyst')).toBe(false);
  });

  it('clears the stored session on logout', () => {
    post.mockReturnValue(
      of({ token: 't', expiresAtUtc: hourFromNow(), username: 'admin', role: 'Administrator' }),
    );

    const auth = makeService();
    auth.login('admin', 'password').subscribe();
    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('fleetops.session')).toBeNull();
  });

  it('discards a corrupt stored session instead of throwing', () => {
    localStorage.setItem('fleetops.session', '{not valid json');
    expect(makeService().isAuthenticated()).toBe(false);
  });
});
