import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { manifestStatusClasses, relativeTime, severityClasses, statusLabel } from './format';

describe('statusLabel', () => {
  it('renders machine enum values as human text', () => {
    expect(statusLabel('UnderWay')).toBe('Under way');
    expect(statusLabel('InPort')).toBe('In port');
  });
});

describe('severityClasses', () => {
  it('gives each severity a distinct treatment', () => {
    const classes = [
      severityClasses('Info'),
      severityClasses('Warning'),
      severityClasses('Critical'),
    ];
    expect(new Set(classes).size).toBe(3);
  });

  it('marks critical in red', () => {
    expect(severityClasses('Critical')).toContain('rose');
  });
});

describe('manifestStatusClasses', () => {
  it('distinguishes a clean acceptance from one with warnings', () => {
    expect(manifestStatusClasses('Accepted')).not.toBe(
      manifestStatusClasses('AcceptedWithWarnings'),
    );
  });
});

describe('relativeTime', () => {
  const now = new Date('2026-07-30T12:00:00Z');
  const ago = (ms: number) => new Date(now.getTime() - ms).toISOString();

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('handles a null timestamp', () => {
    expect(relativeTime(null)).toBe('never');
  });

  it('collapses the last few seconds to "just now"', () => {
    expect(relativeTime(ago(2_000))).toBe('just now');
  });

  it('reports seconds, minutes, hours and days', () => {
    expect(relativeTime(ago(30_000))).toBe('30s ago');
    expect(relativeTime(ago(5 * 60_000))).toBe('5m ago');
    expect(relativeTime(ago(3 * 3_600_000))).toBe('3h ago');
    expect(relativeTime(ago(2 * 86_400_000))).toBe('2d ago');
  });
});
