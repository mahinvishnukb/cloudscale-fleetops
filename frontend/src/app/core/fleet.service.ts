import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  Anomaly,
  CargoManifest,
  FleetHealth,
  PagedResult,
  TelemetryReading,
  Vessel,
  VesselStatus,
  VesselSummary,
} from './models';

@Injectable({ providedIn: 'root' })
export class FleetService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  searchVessels(options: {
    search?: string;
    status?: VesselStatus | null;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<VesselSummary>> {
    let params = new HttpParams()
      .set('page', String(options.page ?? 1))
      .set('pageSize', String(options.pageSize ?? 20));

    if (options.search) {
      params = params.set('search', options.search);
    }

    if (options.status) {
      params = params.set('status', options.status);
    }

    return this.http.get<PagedResult<VesselSummary>>(`${this.base}/vessels`, { params });
  }

  getVessel(id: string): Observable<Vessel> {
    return this.http.get<Vessel>(`${this.base}/vessels/${id}`);
  }

  changeStatus(id: string, status: VesselStatus): Observable<Vessel> {
    return this.http.patch<Vessel>(`${this.base}/vessels/${id}/status`, { status });
  }

  getTelemetry(vesselId: string, hours = 6, maxPoints = 500): Observable<TelemetryReading[]> {
    const to = new Date();
    const from = new Date(to.getTime() - hours * 3_600_000);

    const params = new HttpParams()
      .set('from', from.toISOString())
      .set('to', to.toISOString())
      .set('maxPoints', String(maxPoints));

    return this.http.get<TelemetryReading[]>(`${this.base}/vessels/${vesselId}/telemetry`, { params });
  }

  getOpenAnomalies(limit = 50): Observable<Anomaly[]> {
    return this.http.get<Anomaly[]>(`${this.base}/anomalies`, {
      params: new HttpParams().set('limit', String(limit)),
    });
  }

  acknowledgeAnomaly(id: string): Observable<Anomaly> {
    return this.http.post<Anomaly>(`${this.base}/anomalies/${id}/acknowledge`, {});
  }

  getFleetHealth(): Observable<FleetHealth> {
    return this.http.get<FleetHealth>(`${this.base}/analytics/fleet-health`);
  }

  getManifests(vesselId?: string, limit = 50): Observable<CargoManifest[]> {
    let params = new HttpParams().set('limit', String(limit));
    if (vesselId) {
      params = params.set('vesselId', vesselId);
    }
    return this.http.get<CargoManifest[]>(`${this.base}/manifests`, { params });
  }

  uploadManifest(file: File, voyageNumber: string, vesselId: string): Observable<CargoManifest> {
    const form = new FormData();
    form.append('file', file);
    form.append('voyageNumber', voyageNumber);
    form.append('vesselId', vesselId);

    return this.http.post<CargoManifest>(`${this.base}/manifests/upload`, form);
  }
}
