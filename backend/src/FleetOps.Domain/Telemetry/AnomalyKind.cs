namespace FleetOps.Domain.Telemetry;

public enum AnomalyKind
{
    Unknown = 0,
    EngineOverheat = 1,
    FuelConsumptionSpike = 2,
    ImplausibleSpeed = 3,
    PositionJump = 4,
    SensorDropout = 5,
}
