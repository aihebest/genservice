namespace GenService.API.Services;

/// <summary>
/// One name per site for reporting.
///
/// The modules grew separately and label the same place differently — "OFFICE",
/// "PHC OFFICE", "Port Harcourt Office" and "Port Harcourt (PH) Office" are one
/// building. Reports that group by location must fold these together or each
/// site appears several times with its cost split between them. Stored records
/// are not changed; this only affects how reports group them.
/// </summary>
public static class SiteNames
{
    /// <summary>Canonical site name, as the department writes it on its reports.</summary>
    public static string Site(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0) return "Unspecified";
        var l = s.ToLowerInvariant();

        if (l is "office" or "phc office" or "ph office" or "port harcourt office"
              or "port harcourt (ph) office" or "phc" or "port harcourt")        return "PHC OFFICE";
        if (l is "dr" || l.StartsWith("dr "))                                    return "DR";
        if (l is "dgs" or "dmgs" or "dgs house" or "dgs residence")              return "DGS";
        if (l is "gml" or "gml residence")                                       return "GML";
        if (l.StartsWith("woji"))                                                return "WOJI";
        if (l.StartsWith("lagos office"))                                        return "LAGOS OFFICE";
        if (l is "ak lagos" or "ak, lagos")                                      return "AK, LAGOS";
        if (l is "ak uyo" or "ak, uyo")                                          return "AK, UYO";
        if (l.Contains("chairman"))                                              return "CHAIRMAN UYO";
        if (l.StartsWith("abuja"))                                               return "ABUJA OFFICE";
        return s.ToUpperInvariant();
    }

    /// <summary>The city a site belongs to — LAGOS, PORT HARCOURT, UYO, ABUJA or OTHER.</summary>
    public static string Region(string? raw)
    {
        var l = (raw ?? "").Trim().ToLowerInvariant();
        if (l.Length == 0) return "OTHER";
        if (l.Contains("lagos") || l.Contains("lekki") || l.Contains("tomy")) return "LAGOS";
        if (l.Contains("uyo"))                                                return "UYO";
        if (l.Contains("abuja"))                                              return "ABUJA";
        var site = Site(raw);
        return site is "PHC OFFICE" or "DR" or "DGS" or "GML" or "WOJI" || l.Contains("harcourt") || l.Contains("bonny")
            ? "PORT HARCOURT" : "OTHER";
    }
}
