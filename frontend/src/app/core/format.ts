import { AnomalySeverity, ManifestStatus, VesselStatus } from './models';

export function statusLabel(status: VesselStatus): string {
  const labels: Record<VesselStatus, string> = {
    Unknown: 'Unknown',
    InPort: 'In port',
    UnderWay: 'Under way',
    AtAnchor: 'At anchor',
    Maintenance: 'Maintenance',
    Decommissioned: 'Decommissioned',
  };
  return labels[status];
}

export function statusClasses(status: VesselStatus): string {
  const classes: Record<VesselStatus, string> = {
    Unknown: 'bg-slate-700 text-slate-300',
    InPort: 'bg-sky-900 text-sky-300',
    UnderWay: 'bg-emerald-900 text-emerald-300',
    AtAnchor: 'bg-indigo-900 text-indigo-300',
    Maintenance: 'bg-amber-900 text-amber-300',
    Decommissioned: 'bg-slate-800 text-slate-500',
  };
  return classes[status];
}

export function severityClasses(severity: AnomalySeverity): string {
  const classes: Record<AnomalySeverity, string> = {
    Info: 'bg-slate-700 text-slate-300',
    Warning: 'bg-amber-900 text-amber-300',
    Critical: 'bg-rose-900 text-rose-300',
  };
  return classes[severity];
}

export function manifestStatusClasses(status: ManifestStatus): string {
  const classes: Record<ManifestStatus, string> = {
    Pending: 'bg-slate-700 text-slate-300',
    Processing: 'bg-sky-900 text-sky-300',
    Accepted: 'bg-emerald-900 text-emerald-300',
    AcceptedWithWarnings: 'bg-amber-900 text-amber-300',
    Rejected: 'bg-rose-900 text-rose-300',
  };
  return classes[status];
}

/** "3 min ago" style relative time; the dashboard cares about freshness, not timestamps. */
export function relativeTime(iso: string | null): string {
  if (!iso) {
    return 'never';
  }

  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);

  if (seconds < 5) return 'just now';
  if (seconds < 60) return `${seconds}s ago`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86_400) return `${Math.floor(seconds / 3600)}h ago`;
  return `${Math.floor(seconds / 86_400)}d ago`;
}
