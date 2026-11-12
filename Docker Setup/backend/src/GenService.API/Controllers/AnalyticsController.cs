using System.Globalization;
using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Controllers;

/// <summary>
/// Management dashboards modelled on the department's own Excel reports:
///   • Summary of Repairs and Maintenance Register
///   • Overall Diesel / PHED Purchase Analysis
///   • Per-location Consumption Analysis
///
/// Everything is computed from records the modules already hold — nothing is
/// entered for the reports themselves. Sites are grouped through
/// <see cref="SiteNames"/> so one place is not split across several spellings.
/// </summary>
[ApiController]
[Route("api/v1/reports/analytics")]
[Authorize]
public class AnalyticsController(GenServiceDbContext db) : ControllerBase
{
    private static DateTime? Day(string? s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static bool InRange(DateTime when, DateTime? from, DateTime? to) =>
        (from is null || when.Date >= from.Value.Date) && (to is null || when.Date <= to.Value.Date);

    private static double Pct(decimal part, decimal whole) =>
        whole > 0 ? Math.Round((double)(part / whole) * 100, 2) : 0;

    private static double Pct(double part, double whole) =>
        whole > 0 ? Math.Round(part / whole * 100, 2) : 0;

    /// <summary>One maintenance record reduced to what the dashboard needs.</summary>
    private sealed record Job(string Kind, DateTime When, decimal Cost, bool Closed, bool Rejected,
                              string Site, string Region, string Asset);

    // ── GET /api/v1/reports/analytics/maintenance ───────────────────────────
    /// <summary>
    /// "Summary of Repairs and Maintenance Register": costs overall and per
    /// register, open / closed counts, cost per location and end user, monthly
    /// cost with percentage change, cost per asset and the top five.
    /// Cost = final amount where captured, otherwise spares cost.
    /// </summary>
    [HttpGet("maintenance")]
    public async Task<IActionResult> Maintenance([FromQuery] string? from = null, [FromQuery] string? to = null)
    {
        var f = Day(from); var t = Day(to);

        var vehicles  = await db.VehicleMaintenanceRequests.AsNoTracking().ToListAsync();
        var equipment = await db.EquipmentMaintenanceRequests.AsNoTracking().ToListAsync();
        var facility  = await db.FacilityMaintenanceRequests.AsNoTracking().ToListAsync();

        var jobs = new List<Job>();
        jobs.AddRange(vehicles.Select(r => new Job("Vehicle", r.DateOfRequest ?? r.CreatedAt,
            r.FinalAmountNaira ?? r.SparesCostNaira ?? 0,
            r.Status == VehicleMaintenanceStatus.Completed, r.Status == VehicleMaintenanceStatus.Rejected,
            SiteNames.Site(r.CurrentLocation), SiteNames.Region(r.CurrentLocation),
            string.IsNullOrWhiteSpace(r.VehicleType) ? r.VehicleRegNo : $"{r.VehicleRegNo} - {r.VehicleType}")));
        jobs.AddRange(equipment.Select(r => new Job("Equipment", r.DateOfRequest ?? r.CreatedAt,
            r.FinalAmountNaira ?? r.SparesCostNaira ?? 0,
            r.Status == "Completed", r.Status == "Rejected",
            SiteNames.Site(string.IsNullOrWhiteSpace(r.EndUser) ? r.Location : r.EndUser),
            SiteNames.Region(string.IsNullOrWhiteSpace(r.Location) ? r.EndUser : r.Location),
            r.AssetDescription ?? "")));
        jobs.AddRange(facility.Select(r => new Job("Facility", r.DateOfRequest ?? r.CreatedAt,
            r.FinalAmountNaira ?? r.SparesCostNaira ?? 0,
            r.Status == "Completed", r.Status == "Rejected",
            SiteNames.Site(string.IsNullOrWhiteSpace(r.EndUser) ? r.Location : r.EndUser),
            SiteNames.Region(string.IsNullOrWhiteSpace(r.Location) ? r.EndUser : r.Location),
            SiteNames.Site(r.Location))));

        // Rejected requests were never carried out — they are not maintenance spend.
        jobs = jobs.Where(j => !j.Rejected && InRange(j.When, f, t)).ToList();

        decimal Total(string kind) => jobs.Where(j => j.Kind == kind).Sum(j => j.Cost);
        var overall = jobs.Sum(j => j.Cost);

        object Split(IEnumerable<Job> src, Func<Job, string> key)
        {
            var list  = src.ToList();
            var whole = list.Sum(j => j.Cost);
            return list.GroupBy(key)
                .Select(g => new { label = g.Key, cost = g.Sum(j => j.Cost), percent = Pct(g.Sum(j => j.Cost), whole), count = g.Count() })
                .OrderByDescending(x => x.cost).ToList();
        }

        object Monthly(string kind)
        {
            var rows = jobs.Where(j => j.Kind == kind)
                .GroupBy(j => new DateTime(j.When.Year, j.When.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new { Month = g.Key, Cost = g.Sum(j => j.Cost) }).ToList();
            return rows.Select((r, i) => new
            {
                month = r.Month.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                cost  = r.Cost,
                // Change against the previous month that has records.
                changePercent = i > 0 && rows[i - 1].Cost > 0
                    ? Math.Round((double)((r.Cost - rows[i - 1].Cost) / rows[i - 1].Cost) * 100, 0)
                    : (double?)null,
            }).ToList();
        }

        object ByAsset(string kind) => jobs.Where(j => j.Kind == kind && !string.IsNullOrWhiteSpace(j.Asset))
            .GroupBy(j => j.Asset.Trim().ToUpperInvariant())
            .Select(g => new { label = g.Key, cost = g.Sum(j => j.Cost), jobs = g.Count(), region = g.First().Region })
            .OrderByDescending(x => x.cost).ToList();

        object Tracker(string kind) => new
        {
            closed = jobs.Count(j => j.Kind == kind && j.Closed),
            open   = jobs.Count(j => j.Kind == kind && !j.Closed),
        };

        return Ok(new
        {
            totals  = new { overall, vehicle = Total("Vehicle"), equipment = Total("Equipment"), facility = Total("Facility") },
            tracker = new { vehicle = Tracker("Vehicle"), equipment = Tracker("Equipment"), facility = Tracker("Facility") },
            vehicleByRegion    = Split(jobs.Where(j => j.Kind == "Vehicle"),   j => j.Region),
            equipmentByEndUser = Split(jobs.Where(j => j.Kind == "Equipment"), j => j.Site),
            facilityByEndUser  = Split(jobs.Where(j => j.Kind == "Facility"),  j => j.Site),
            overallByRegion    = Split(jobs, j => j.Region),
            monthly = new { vehicle = Monthly("Vehicle"), equipment = Monthly("Equipment"), facility = Monthly("Facility") },
            byAsset = new { vehicle = ByAsset("Vehicle"), equipment = ByAsset("Equipment"), facility = ByAsset("Facility") },
            // How many jobs carry no cost yet — the totals understate by these.
            uncosted = jobs.Count(j => j.Closed && j.Cost == 0),
        });
    }

    // ── GET /api/v1/reports/analytics/energy-purchase ───────────────────────
    /// <summary>
    /// "Overall Diesel / PHED Purchase Analysis": diesel bought (cost, litres) and
    /// how it was shared between end users, and PHED spend and kWh per end user,
    /// with monthly series. Diesel reaches an end user through a distribution, so
    /// its share is the litres issued to that site, costed at the batch price.
    /// </summary>
    [HttpGet("energy-purchase")]
    public async Task<IActionResult> EnergyPurchase([FromQuery] string? from = null, [FromQuery] string? to = null)
    {
        var f = Day(from); var t = Day(to);

        var supplies = await db.DieselBulkSupplies.AsNoTracking().ToListAsync();
        var dists    = await db.DieselDistributions.AsNoTracking().ToListAsync();
        var power    = await db.ElectricityPurchases.AsNoTracking().ToListAsync();
        var price    = supplies.ToDictionary(s => s.Id, s => s.UnitPriceNaira);

        var bought = supplies.Where(s => InRange(s.SupplyDate, f, t)).ToList();

        var issued = dists.Where(d => InRange(d.DistributionDate, f, t)).Select(d => new
        {
            // Vehicles are one end user; each site is its own.
            EndUser = d.DistributionType == DieselDistributionType.Vehicle ? "VEHICLES" : SiteNames.Site(d.DestinationLocation),
            Month   = new DateTime(d.DistributionDate.Year, d.DistributionDate.Month, 1),
            Litres  = d.QuantityLitres,
            Cost    = (decimal)d.QuantityLitres * price.GetValueOrDefault(d.BulkSupplyId),
        }).ToList();

        var dieselCost   = issued.Sum(x => x.Cost);
        var dieselLitres = issued.Sum(x => x.Litres);

        var bills = power.Where(p => InRange(p.PurchaseDate, f, t)).Select(p => new
        {
            EndUser = SiteNames.Site(p.Location),
            Month   = new DateTime(p.PurchaseDate.Year, p.PurchaseDate.Month, 1),
            Cost    = p.AmountNaira,
            Kwh     = p.UnitsKwh,
        }).ToList();
        var phedCost = bills.Sum(b => b.Cost);
        var phedKwh  = bills.Sum(b => b.Kwh);

        static string M(DateTime d) => d.ToString("MMM yyyy", CultureInfo.InvariantCulture);

        return Ok(new
        {
            diesel = new
            {
                purchasedCost   = bought.Sum(s => s.TotalCostNaira),
                purchasedLitres = bought.Sum(s => s.QuantityLitres),
                issuedCost      = dieselCost,
                issuedLitres    = dieselLitres,
                byEndUser = issued.GroupBy(x => x.EndUser).Select(g => new
                {
                    label = g.Key, litres = g.Sum(x => x.Litres), cost = g.Sum(x => x.Cost),
                    percentCost = Pct(g.Sum(x => x.Cost), dieselCost), percentLitres = Pct(g.Sum(x => x.Litres), dieselLitres),
                }).OrderByDescending(x => x.cost).ToList(),
                monthly = issued.GroupBy(x => new { x.Month, x.EndUser }).OrderBy(g => g.Key.Month)
                    .Select(g => new { month = M(g.Key.Month), endUser = g.Key.EndUser, litres = g.Sum(x => x.Litres), cost = g.Sum(x => x.Cost) }).ToList(),
                monthlyPurchase = bought.GroupBy(s => new DateTime(s.SupplyDate.Year, s.SupplyDate.Month, 1)).OrderBy(g => g.Key)
                    .Select(g => new { month = M(g.Key), litres = g.Sum(s => s.QuantityLitres), cost = g.Sum(s => s.TotalCostNaira) }).ToList(),
            },
            phed = new
            {
                totalCost = phedCost,
                totalKwh  = phedKwh,
                byEndUser = bills.GroupBy(b => b.EndUser).Select(g => new
                {
                    label = g.Key, kwh = g.Sum(b => b.Kwh), cost = g.Sum(b => b.Cost),
                    percentCost = Pct(g.Sum(b => b.Cost), phedCost), percentKwh = Pct(g.Sum(b => b.Kwh), phedKwh),
                }).OrderByDescending(x => x.cost).ToList(),
                monthly = bills.GroupBy(b => new { b.Month, b.EndUser }).OrderBy(g => g.Key.Month)
                    .Select(g => new { month = M(g.Key.Month), endUser = g.Key.EndUser, kwh = g.Sum(b => b.Kwh), cost = g.Sum(b => b.Cost) }).ToList(),
            },
        });
    }

    // ── GET /api/v1/reports/analytics/location-consumption ──────────────────
    /// <summary>
    /// "&lt;Site&gt; Consumption Analysis – &lt;Month&gt;": generator running hours, diesel
    /// consumed, current diesel level, PHED kWh and last diesel received, with the
    /// last seven days, weekday / weekend split and the year's monthly consumption.
    /// Diesel consumed uses the calibrated tank dip where the site has one,
    /// otherwise the generators' own fuel figures.
    /// </summary>
    [HttpGet("location-consumption")]
    public async Task<IActionResult> LocationConsumption([FromQuery] string? site = null, [FromQuery] string? month = null)
    {
        var monthStart = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
            ? m : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var readings = await db.GeneratorDailyReadings.AsNoTracking().ToListAsync();
        var sites = readings.Select(r => SiteNames.Site(r.Location)).Distinct().OrderBy(s => s).ToList();
        var chosen = string.IsNullOrWhiteSpace(site) ? sites.FirstOrDefault() ?? "PHC OFFICE" : SiteNames.Site(site);

        var here = readings.Where(r => SiteNames.Site(r.Location) == chosen).ToList();

        // Litres used on a day: dip-based where any reading that day has it.
        double DayLitres(IEnumerable<GeneratorDailyReading> day)
        {
            var list = day.ToList();
            var dip  = list.Where(r => r.TankConsumptionLitres != null).Sum(r => r.TankConsumptionLitres!.Value);
            return list.Any(r => r.TankConsumptionLitres != null) ? dip : list.Sum(r => r.FuelConsumedLitres ?? 0);
        }

        var byDay = here.GroupBy(r => r.ReadingDate.Date)
            .Select(g => new { Date = g.Key, Litres = DayLitres(g), Hours = g.Sum(r => r.RunHoursToday) })
            .OrderBy(x => x.Date).ToList();
        var inMonth = byDay.Where(d => d.Date >= monthStart && d.Date < monthEnd).ToList();

        // Current level: the latest dip if the site is dipped, else the sum of
        // each generator's latest fuel level.
        var latestDip = here.Where(r => r.TankLevelLitres != null)
            .OrderByDescending(r => r.ReadingDate).ThenByDescending(r => r.CreatedAt).FirstOrDefault();
        var currentLevel = latestDip?.TankLevelLitres
            ?? here.GroupBy(r => r.AssetNo)
                   .Sum(g => g.OrderByDescending(r => r.ReadingDate).ThenByDescending(r => r.CreatedAt).First().FuelLevelLitres);

        var meters = await db.PowerMeterReadings.AsNoTracking().ToListAsync();
        var kwhMeter = meters.Where(p => SiteNames.Site(p.Location) == chosen && p.ReadingDate >= monthStart && p.ReadingDate < monthEnd)
                             .Sum(p => p.UnitsConsumedToday ?? 0);
        var bills = await db.ElectricityPurchases.AsNoTracking().ToListAsync();
        var kwhBilled = bills.Where(p => SiteNames.Site(p.Location) == chosen && p.PurchaseDate >= monthStart && p.PurchaseDate < monthEnd)
                             .Sum(p => p.UnitsKwh);

        var dists = await db.DieselDistributions.AsNoTracking()
            .Where(d => d.DistributionType == DieselDistributionType.Location).ToListAsync();
        var lastIn = dists.Where(d => SiteNames.Site(d.DestinationLocation) == chosen)
                          .OrderByDescending(d => d.DistributionDate).ThenByDescending(d => d.CreatedAt).FirstOrDefault();

        var last7 = byDay.Where(d => d.Date < monthEnd).TakeLast(7).ToList();
        bool Weekend(DateTime d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        return Ok(new
        {
            sites,
            site  = chosen,
            month = monthStart.ToString("yyyy-MM"),
            totals = new
            {
                runHours       = Math.Round(inMonth.Sum(d => d.Hours), 1),
                dieselConsumed = Math.Round(inMonth.Sum(d => d.Litres), 1),
                currentLevel   = Math.Round(currentLevel, 1),
                // Daily meter readings where they are kept; otherwise units billed.
                phedKwh        = Math.Round(kwhMeter > 0 ? kwhMeter : kwhBilled, 1),
                phedKwhSource  = kwhMeter > 0 ? "meter" : "purchases",
                lastDieselLitres = lastIn?.QuantityLitres,
                lastDieselDate   = lastIn?.DistributionDate,
                daysLogged     = inMonth.Count,
            },
            last7Days = last7.Select(d => new { date = d.Date, day = d.Date.ToString("ddd", CultureInfo.InvariantCulture), litres = Math.Round(d.Litres, 1) }),
            weekdayWeekend = new
            {
                weekday = Math.Round(inMonth.Where(d => !Weekend(d.Date)).Sum(d => d.Litres), 1),
                weekend = Math.Round(inMonth.Where(d =>  Weekend(d.Date)).Sum(d => d.Litres), 1),
            },
            daily = inMonth.Select(d => new { date = d.Date, litres = Math.Round(d.Litres, 1), hours = Math.Round(d.Hours, 1) }),
            monthly = byDay.Where(d => d.Date.Year == monthStart.Year)
                .GroupBy(d => d.Date.Month).OrderBy(g => g.Key)
                .Select(g => new
                {
                    month  = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key),
                    litres = Math.Round(g.Sum(d => d.Litres), 1),
                    hours  = Math.Round(g.Sum(d => d.Hours), 1),
                }),
        });
    }
}
