namespace GenService.API.Services;

/// <summary>
/// Dip-to-litres conversion for the department's calibrated AGO storage tanks.
///
/// Source: the calibration charts supplied by the General Service Manager
/// ("Desicon Engineering Limited Port Harcourt River State AGO Storage Tank
/// No. 1–4", calibration date February 2026). Each chart's "Page 1a" gives a
/// handful of breakpoints with a litres-per-mm increment; the detailed pages
/// are those increments applied from each breakpoint and truncated to whole
/// litres. Reproducing that rule matches the published tables to within ±1 L
/// (the chart rounds its increments to 3 decimals), well inside the precision
/// of a dip stick.
///
/// Tank names here are what the Tank Level Log stores in TankIdentifier, and
/// must match the frontend list (frontend/src/utils/tankCalibration.ts).
/// </summary>
public static class TankCalibration
{
    public sealed record Segment(int FromMm, double VolumeAtFrom, double LitresPerMm);

    public sealed record Chart(
        string TankName, string Location, int TankNo,
        int ReferenceHeightMm, int MaxDipMm, double MaxVolume,
        Segment[] Segments);

    public static readonly Chart[] Charts =
    [
        new("Tank No. 1 — PHC Office (calibrated)", "Port Harcourt Office", 1, 2485, 2445, 9088,
        [
            new(0,    0,    3.700), new(10,   37,   3.711), new(100, 371, 3.718),
            new(1000, 3717, 3.717), new(2000, 7434, 3.717),
        ]),
        new("Tank No. 2 — DGS House (calibrated)", "DGS", 2, 1260, 1220, 3686,
        [
            new(0, 0, 3.000), new(10, 30, 3.022), new(100, 302, 3.022), new(1000, 3022, 3.022),
        ]),
        new("Tank No. 3 — Woji Spar yellow tank (calibrated)", "Woji", 3, 1600, 1511, 2391,
        [
            new(0, 0, 1.500), new(10, 15, 1.589), new(100, 158, 1.583), new(1000, 1583, 1.583),
        ]),
        new("Tank No. 4 — DR (calibrated)", "DR", 4, 1550, 1511, 7308,
        [
            new(0, 0, 4.800), new(10, 48, 4.833), new(100, 483, 4.838), new(1000, 4837, 4.837),
        ]),
    ];

    /// <summary>
    /// The calibrated tank serving a site, for Daily Readings — every generator at
    /// that location draws from it. Accepts the location names used across the
    /// platform ("Port Harcourt Office", older "Office" entries, etc.).
    /// </summary>
    public static Chart? ForLocation(string? location)
    {
        var l = (location ?? "").Trim().ToLowerInvariant();
        return l switch
        {
            "port harcourt office" or "phc office" or "office" or "port harcourt (ph) office" => Charts[0],
            "dgs" or "dgs house" or "dgs residence"                                           => Charts[1],
            "woji" or "woji spar"                                                             => Charts[2],
            "dr"                                                                              => Charts[3],
            _ => null,
        };
    }

    public static Chart? For(string? tankName) =>
        Charts.FirstOrDefault(c => string.Equals(c.TankName, tankName?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Litres in the tank at a dip reading, per its calibration chart.</summary>
    public static double Litres(Chart chart, double dipMm)
    {
        if (dipMm <= 0) return 0;
        if (dipMm >= chart.MaxDipMm) return chart.MaxVolume;
        var seg = chart.Segments.Last(s => s.FromMm <= dipMm);
        return Math.Floor(seg.VolumeAtFrom + seg.LitresPerMm * (dipMm - seg.FromMm) + 1e-9);
    }
}
