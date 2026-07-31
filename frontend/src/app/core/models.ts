export type VesselStatus =
  | 'Unknown'
  | 'InPort'
  | 'UnderWay'
  | 'AtAnchor'
  | 'Maintenance'
  | 'Decommissioned';

export type VesselType =
  | 'Unknown'
  | 'ContainerShip'
  | 'BulkCarrier'
  | 'Tanker'
  | 'RoRo'
  | 'Tug'
  | 'Reefer';

export type AnomalySeverity = 'Info' | 'Warning' | 'Critical';

export type AnomalyKind =
  | 'Unknown'
  | 'EngineOverheat'
  | 'FuelConsumptionSpike'
  | 'ImplausibleSpeed'
  | 'PositionJump'
  | 'SensorDropout';

export type ManifestStatus =
  | 'Pending'
  | 'Processing'
  | 'Accepted'
  | 'AcceptedWithWarnings'
  | 'Rejected';

export interface VesselSummary {
  id: string;
  imoNumber: string;
  name: string;
  type: VesselType;
  status: VesselStatus;
  homePort: string;
  grossTonnage: number;
  lastSpeedKn: number | null;
  lastEngineTempC: number | null;
  lastReportedAtUtc: string | null;
  openAnomalyCount: number;
}

export interface Vessel {
  id: string;
  imoNumber: string;
  name: string;
  type: VesselType;
  status: VesselStatus;
  homePort: string;
  grossTonnage: number;
  createdAtUtc: string;
}

export interface TelemetryReading {
  id: string;
  vesselId: string;
  recordedAtUtc: string;
  latitude: number;
  longitude: number;
  speedOverGroundKn: number;
  engineRpm: number;
  fuelFlowLitresPerHour: number;
  engineTempC: number;
  fuelPerNauticalMile: number | null;
}

export interface Anomaly {
  id: string;
  vesselId: string;
  vesselName: string;
  kind: AnomalyKind;
  severity: AnomalySeverity;
  detail: string;
  detectedAtUtc: string;
  isAcknowledged: boolean;
  acknowledgedBy: string | null;
}

export interface FleetHealth {
  totalVessels: number;
  underWay: number;
  inPort: number;
  inMaintenance: number;
  openAnomalies: number;
  criticalAnomalies: number;
  averageSpeedKn: number;
  averageEngineTempC: number;
}

export interface CargoManifest {
  id: string;
  voyageNumber: string;
  vesselId: string;
  sourceObjectKey: string;
  status: ManifestStatus;
  receivedAtUtc: string;
  processedAtUtc: string | null;
  lineItemCount: number;
  totalGrossWeightKg: number;
  hazardousCount: number;
  validationErrors: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  username: string;
  role: string;
}
