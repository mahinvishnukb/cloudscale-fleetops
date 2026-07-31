using FleetOps.Domain.Vessels;

namespace FleetOps.Application.Ais;

/// <summary>
/// Maps the ITU ship-and-cargo type code broadcast in AIS static data onto the fleet's own
/// vessel types. The tens digit gives the broad category; the units digit describes cargo
/// hazard class, which is not modelled here.
/// </summary>
public static class AisShipType
{
    public static VesselType ToVesselType(int? code) => (code / 10) switch
    {
        3 => VesselType.Tug,          // 30–39 fishing, towing, dredging, diving
        5 => VesselType.Tug,          // 50–59 pilot, SAR, tug, port tender, law enforcement
        6 => VesselType.RoRo,         // 60–69 passenger — RoRo is the closest modelled type
        7 => VesselType.ContainerShip, // 70–79 cargo
        8 => VesselType.Tanker,       // 80–89 tanker
        _ => VesselType.Unknown,
    };

    /// <summary>
    /// Rough gross tonnage from broadcast dimensions. Gross tonnage is a volumetric measure,
    /// so length × beam × draught × a block coefficient gets within the right order of
    /// magnitude. AIS does not broadcast tonnage, and this is an estimate, not a fact —
    /// it exists only so the vessel record has a plausible figure.
    /// </summary>
    public static int EstimateGrossTonnage(AisDimension? dimension, double? draughtMetres)
    {
        var length = dimension?.LengthMetres ?? 0;
        var beam = dimension?.BeamMetres ?? 0;
        var draught = draughtMetres is > 0 and < 30 ? draughtMetres.Value : 8.0;

        if (length <= 0 || beam <= 0)
        {
            return 1_000;
        }

        // 0.25 is a blended block coefficient plus the volume-to-GT conversion.
        var estimate = (int)Math.Round(length * beam * draught * 0.25);

        // The domain rejects anything outside 1..300,000.
        return Math.Clamp(estimate, 1, 300_000);
    }
}
