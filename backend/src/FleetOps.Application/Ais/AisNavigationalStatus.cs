using FleetOps.Domain.Vessels;

namespace FleetOps.Application.Ais;

/// <summary>
/// Navigational status codes from ITU-R M.1371, as broadcast in an AIS position report.
/// </summary>
public enum AisNavigationalStatus
{
    UnderWayUsingEngine = 0,
    AtAnchor = 1,
    NotUnderCommand = 2,
    RestrictedManoeuvrability = 3,
    ConstrainedByDraught = 4,
    Moored = 5,
    Aground = 6,
    EngagedInFishing = 7,
    UnderWaySailing = 8,
    AisSartActive = 14,
    Undefined = 15,
}

public static class AisNavigationalStatusMap
{
    /// <summary>
    /// Maps an AIS navigational status onto the fleet's own status, or null when the code
    /// has no honest equivalent.
    ///
    /// Returning null matters. "Not under command" and "aground" are emergency conditions,
    /// not berth states — forcing them into Maintenance or AtAnchor would quietly invent
    /// information. Code 15 (undefined) is the transponder default and is extremely common
    /// in live data, so treating it as meaningful would be worse than ignoring it.
    /// </summary>
    public static VesselStatus? ToVesselStatus(int code) => code switch
    {
        (int)AisNavigationalStatus.UnderWayUsingEngine => VesselStatus.UnderWay,
        (int)AisNavigationalStatus.AtAnchor => VesselStatus.AtAnchor,
        (int)AisNavigationalStatus.RestrictedManoeuvrability => VesselStatus.UnderWay,
        (int)AisNavigationalStatus.ConstrainedByDraught => VesselStatus.UnderWay,
        (int)AisNavigationalStatus.Moored => VesselStatus.InPort,
        (int)AisNavigationalStatus.EngagedInFishing => VesselStatus.UnderWay,
        (int)AisNavigationalStatus.UnderWaySailing => VesselStatus.UnderWay,
        _ => null,
    };
}
