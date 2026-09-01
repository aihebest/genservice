using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GenService.API.Controllers;

/// <summary>
/// Guest-house feeding log — replaces the "JAYVIK FEEDING LOG" spreadsheet.
/// Captures meal quantities per staff member per day against a project/cost code,
/// prices them from the editable meal-rate table, and produces the two summaries
/// the team relied on: by cost centre and by head count.
/// </summary>
[ApiController]
[Route("api/v1/feeding-log")]
[Authorize]
public class FeedingLogController(
    GenServiceDbContext db,
    ILogger<FeedingLogController> logger) : ControllerBase
{
    private string CallerEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
    private string CallerName  => User.FindFirstValue(ClaimTypes.Name)  ?? "";
    private string CallerRole  => User.FindFirst("role")?.Value
                               ?? User.FindFirstValue(ClaimTypes.Role)
                               ?? "Requester";
    /// <summary>Only managers/admins may correct records or change meal rates.</summary>
    private bool CanEditRecords => CallerRole is "DepartmentManager" or "SystemAdmin";

    private static FeedingLogDto ToDto(FeedingLogEntry e) => new(
        e.Id, e.EntryDate.ToString("yyyy-MM-dd"), e.StaffName, e.ProjectCostCode,
        e.Breakfast, e.SoftDrink, e.Water, e.Juice, e.Lunch, e.Dinner, e.Tea, e.Snacks,
        e.TotalItems, e.TotalCostNaira, e.Notes,
        e.LoggedByName, e.CreatedAt, e.LastEditedByName, e.LastEditedAt);

    /// <summary>Loads current rates, seeding defaults on first use.</summary>
    private async Task<Dictionary<string, decimal>> GetRateMapAsync()
    {
        var rates = await db.MealRates.AsNoTracking().ToListAsync();
        if (rates.Count == 0)
        {
            var seeded = MealItems.Defaults.Select(d => new MealRate
            {
                ItemKey = d.Key, Label = d.Label, UnitPriceNaira = d.Price, SortOrder = d.Order,
            }).ToList();
            db.MealRates.AddRange(seeded);
            await db.SaveChangesAsync();
            rates = seeded;
        }
        return rates.ToDictionary(r => r.ItemKey, r => r.UnitPriceNaira);
    }

    private static decimal PriceOf(Dictionary<string, decimal> rates, FeedingLogEntry e) =>
          e.Breakfast * rates.GetValueOrDefault(MealItems.Breakfast)
        + e.SoftDrink * rates.GetValueOrDefault(MealItems.SoftDrink)
        + e.Water     * rates.GetValueOrDefault(MealItems.Water)
        + e.Juice     * rates.GetValueOrDefault(MealItems.Juice)
        + e.Lunch     * rates.GetValueOrDefault(MealItems.Lunch)
        + e.Dinner    * rates.GetValueOrDefault(MealItems.Dinner)
        + e.Tea       * rates.GetValueOrDefault(MealItems.Tea)
        + e.Snacks    * rates.GetValueOrDefault(MealItems.Snacks);

    // ── GET /api/v1/feeding-log/rates ────────────────────────────────────────
    [HttpGet("rates")]
    public async Task<ActionResult<IEnumerable<MealRateDto>>> GetRates()
    {
        await GetRateMapAsync();   // ensures seeding
        var rates = await db.MealRates.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync();
        return Ok(rates.Select(r => new MealRateDto(r.ItemKey, r.Label, r.UnitPriceNaira, r.SortOrder)));
    }

    // ── PUT /api/v1/feeding-log/rates ────────────────────────────────────────
    /// <summary>Manager-only update of meal unit prices. Existing entries keep their original value.</summary>
    [HttpPut("rates")]
    public async Task<ActionResult<IEnumerable<MealRateDto>>> UpdateRates([FromBody] UpdateMealRatesRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can change meal rates." });

        await GetRateMapAsync();
        var rates = await db.MealRates.ToListAsync();
        foreach (var change in req.Rates)
        {
            var rate = rates.FirstOrDefault(r => r.ItemKey == change.ItemKey);
            if (rate is null || change.UnitPriceNaira < 0) continue;
            rate.UnitPriceNaira   = change.UnitPriceNaira;
            rate.UpdatedAt        = DateTime.UtcNow;
            rate.LastEditedByName = CallerName;
            rate.LastEditedAt     = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Meal rates updated by {User}", CallerEmail);

        return Ok(rates.OrderBy(r => r.SortOrder)
                       .Select(r => new MealRateDto(r.ItemKey, r.Label, r.UnitPriceNaira, r.SortOrder)));
    }

    // ── GET /api/v1/feeding-log ──────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<FeedingLogListResponse>> List([FromQuery] FeedingLogQuery q)
    {
        var query = Filtered(await db.FeedingLogEntries.AsNoTracking().ToListAsync(), q);

        var total     = query.Count;
        var totalCost = query.Sum(e => e.TotalCostNaira);
        var items     = query
            .OrderByDescending(e => e.EntryDate)
            .ThenBy(e => e.StaffName)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(ToDto);

        return Ok(new FeedingLogListResponse(items, total, q.Page, q.PageSize, totalCost));
    }

    private static List<FeedingLogEntry> Filtered(List<FeedingLogEntry> all, FeedingLogQuery q)
    {
        IEnumerable<FeedingLogEntry> x = all;
        if (!string.IsNullOrWhiteSpace(q.From) && DateOnly.TryParse(q.From, out var f))
            x = x.Where(e => e.EntryDate >= f);
        if (!string.IsNullOrWhiteSpace(q.To) && DateOnly.TryParse(q.To, out var t))
            x = x.Where(e => e.EntryDate <= t);
        if (!string.IsNullOrWhiteSpace(q.CostCode))
            x = x.Where(e => e.ProjectCostCode == q.CostCode);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            x = x.Where(e => e.StaffName.Contains(s, StringComparison.OrdinalIgnoreCase)
                          || (e.ProjectCostCode ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        return x.ToList();
    }

    private static FeedingSummaryRow Aggregate(string key, IEnumerable<FeedingLogEntry> rows)
    {
        var list = rows.ToList();
        return new FeedingSummaryRow(
            key,
            list.Sum(e => e.Breakfast), list.Sum(e => e.SoftDrink), list.Sum(e => e.Water),
            list.Sum(e => e.Juice),     list.Sum(e => e.Lunch),     list.Sum(e => e.Dinner),
            list.Sum(e => e.Tea),       list.Sum(e => e.Snacks),
            list.Sum(e => e.TotalCostNaira));
    }

    // ── GET /api/v1/feeding-log/summary ──────────────────────────────────────
    /// <summary>The two summaries from their spreadsheet: by cost centre and by head count.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<FeedingSummaryResponse>> Summary([FromQuery] FeedingLogQuery q)
    {
        var rows = Filtered(await db.FeedingLogEntries.AsNoTracking().ToListAsync(), q);

        var byCost = rows.GroupBy(e => string.IsNullOrWhiteSpace(e.ProjectCostCode) ? "(blank)" : e.ProjectCostCode!)
                         .Select(g => Aggregate(g.Key, g))
                         .OrderByDescending(r => r.TotalCostNaira)
                         .ToList();

        var byHead = rows.GroupBy(e => string.IsNullOrWhiteSpace(e.StaffName) ? "(blank)" : e.StaffName)
                         .Select(g => Aggregate(g.Key, g))
                         .OrderByDescending(r => r.TotalCostNaira)
                         .ToList();

        var label = (!string.IsNullOrWhiteSpace(q.From) || !string.IsNullOrWhiteSpace(q.To))
            ? $"{q.From ?? "start"} to {q.To ?? "today"}"
            : "All records";

        return Ok(new FeedingSummaryResponse(byCost, byHead, Aggregate("Grand Total", rows), label));
    }

    // ── GET /api/v1/feeding-log/stats ────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<ActionResult<FeedingStatsDto>> Stats()
    {
        var now        = DateTime.UtcNow;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var month      = await db.FeedingLogEntries.AsNoTracking()
                                 .Where(e => e.EntryDate >= monthStart).ToListAsync();

        return Ok(new FeedingStatsDto(
            EntriesThisMonth:  month.Count,
            StaffFedThisMonth: month.Select(e => e.StaffName).Distinct().Count(),
            MealsThisMonth:    month.Sum(e => e.TotalItems),
            CostThisMonth:     month.Sum(e => e.TotalCostNaira)));
    }

    // ── POST /api/v1/feeding-log ─────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<FeedingLogDto>> Create([FromBody] CreateFeedingLogRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.StaffName))
            return BadRequest(new { message = "Staff name is required (use \"Extra\" for unattributed meals)." });
        if (!DateOnly.TryParse(req.EntryDate, out var date))
            return BadRequest(new { message = "Invalid date. Use YYYY-MM-DD." });

        var e = new FeedingLogEntry
        {
            EntryDate       = date,
            StaffName       = req.StaffName.Trim(),
            ProjectCostCode = req.ProjectCostCode?.Trim(),
            Breakfast = Math.Max(0, req.Breakfast), SoftDrink = Math.Max(0, req.SoftDrink),
            Water     = Math.Max(0, req.Water),     Juice     = Math.Max(0, req.Juice),
            Lunch     = Math.Max(0, req.Lunch),     Dinner    = Math.Max(0, req.Dinner),
            Tea       = Math.Max(0, req.Tea),       Snacks    = Math.Max(0, req.Snacks),
            Notes           = req.Notes?.Trim(),
            LoggedByEmail   = CallerEmail,
            LoggedByName    = CallerName,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        };

        if (e.TotalItems == 0)
            return BadRequest(new { message = "Enter at least one meal item." });

        e.TotalCostNaira = PriceOf(await GetRateMapAsync(), e);

        db.FeedingLogEntries.Add(e);
        await db.SaveChangesAsync();
        logger.LogInformation("Feeding log {Date} {Staff} — ₦{Cost}", e.EntryDate, e.StaffName, e.TotalCostNaira);
        return Ok(ToDto(e));
    }

    // ── PUT /api/v1/feeding-log/{id} ─────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FeedingLogDto>> Update(Guid id, [FromBody] CreateFeedingLogRequest req)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can edit existing records." });

        var e = await db.FeedingLogEntries.FindAsync(id);
        if (e is null) return NotFound();

        if (DateOnly.TryParse(req.EntryDate, out var date)) e.EntryDate = date;
        if (!string.IsNullOrWhiteSpace(req.StaffName)) e.StaffName = req.StaffName.Trim();
        e.ProjectCostCode = req.ProjectCostCode?.Trim();
        e.Breakfast = Math.Max(0, req.Breakfast); e.SoftDrink = Math.Max(0, req.SoftDrink);
        e.Water     = Math.Max(0, req.Water);     e.Juice     = Math.Max(0, req.Juice);
        e.Lunch     = Math.Max(0, req.Lunch);     e.Dinner    = Math.Max(0, req.Dinner);
        e.Tea       = Math.Max(0, req.Tea);       e.Snacks    = Math.Max(0, req.Snacks);
        e.Notes     = req.Notes?.Trim() ?? e.Notes;

        // Re-price the corrected row against current rates.
        e.TotalCostNaira   = PriceOf(await GetRateMapAsync(), e);
        e.UpdatedAt        = DateTime.UtcNow;
        e.LastEditedByName = CallerName;
        e.LastEditedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToDto(e));
    }

    // ── DELETE /api/v1/feeding-log/{id} ──────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!CanEditRecords)
            return StatusCode(403, new { message = "Only a Department Manager or System Admin can delete records." });
        var e = await db.FeedingLogEntries.FindAsync(id);
        if (e is null) return NotFound();
        db.FeedingLogEntries.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/v1/feeding-log/export ───────────────────────────────────────
    /// <summary>Excel export mirroring their sheet: detail rows plus both summaries.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] FeedingLogQuery q)
    {
        var rows = Filtered(await db.FeedingLogEntries.AsNoTracking().ToListAsync(), q)
                   .OrderBy(e => e.EntryDate).ThenBy(e => e.StaffName).ToList();

        using var wb = new XLWorkbook();

        // Sheet 1 — detail
        var ws = wb.AddWorksheet("Feeding Log");
        string[] headers = ["DATE", "NAME", "Project No/ Cost Code", "BREAKFAST", "SOFT DRINK",
                            "WATER", "JUICE", "LUNCH", "DINNER", "TEA", "SNACKS", "TOTAL COST (NGN)"];
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1677ff");
            cell.Style.Font.FontColor = XLColor.White;
        }
        for (int r = 0; r < rows.Count; r++)
        {
            var e = rows[r];
            ws.Cell(r + 2, 1).Value = e.EntryDate.ToString("dd-MMM-yy");
            ws.Cell(r + 2, 2).Value = e.StaffName;
            ws.Cell(r + 2, 3).Value = e.ProjectCostCode ?? "";
            ws.Cell(r + 2, 4).Value = e.Breakfast;
            ws.Cell(r + 2, 5).Value = e.SoftDrink;
            ws.Cell(r + 2, 6).Value = e.Water;
            ws.Cell(r + 2, 7).Value = e.Juice;
            ws.Cell(r + 2, 8).Value = e.Lunch;
            ws.Cell(r + 2, 9).Value = e.Dinner;
            ws.Cell(r + 2, 10).Value = e.Tea;
            ws.Cell(r + 2, 11).Value = e.Snacks;
            ws.Cell(r + 2, 12).Value = e.TotalCostNaira;
            if (r % 2 == 1) ws.Row(r + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f5f5");
        }
        ws.Columns().AdjustToContents(8, 40);
        ws.SheetView.FreezeRows(1);

        // Sheets 2 & 3 — the two summaries they rely on
        void SummarySheet(string name, string keyHeader, IEnumerable<IGrouping<string, FeedingLogEntry>> groups)
        {
            var s = wb.AddWorksheet(name);
            string[] h = [keyHeader, "BREAKFAST", "SOFT DRINK", "WATER", "JUICE",
                          "LUNCH", "DINNER", "TEA", "SNACKS", "TOTAL COST (NGN)"];
            for (int c = 0; c < h.Length; c++)
            {
                var cell = s.Cell(1, c + 1);
                cell.Value = h[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1677ff");
                cell.Style.Font.FontColor = XLColor.White;
            }
            int i = 2;
            foreach (var g in groups)
            {
                var a = Aggregate(g.Key, g);
                s.Cell(i, 1).Value = a.Key;      s.Cell(i, 2).Value = a.Breakfast;
                s.Cell(i, 3).Value = a.SoftDrink;s.Cell(i, 4).Value = a.Water;
                s.Cell(i, 5).Value = a.Juice;    s.Cell(i, 6).Value = a.Lunch;
                s.Cell(i, 7).Value = a.Dinner;   s.Cell(i, 8).Value = a.Tea;
                s.Cell(i, 9).Value = a.Snacks;   s.Cell(i, 10).Value = a.TotalCostNaira;
                i++;
            }
            var t = Aggregate("Grand Total", rows);
            s.Cell(i, 1).Value = t.Key;       s.Cell(i, 2).Value = t.Breakfast;
            s.Cell(i, 3).Value = t.SoftDrink; s.Cell(i, 4).Value = t.Water;
            s.Cell(i, 5).Value = t.Juice;     s.Cell(i, 6).Value = t.Lunch;
            s.Cell(i, 7).Value = t.Dinner;    s.Cell(i, 8).Value = t.Tea;
            s.Cell(i, 9).Value = t.Snacks;    s.Cell(i, 10).Value = t.TotalCostNaira;
            s.Row(i).Style.Font.Bold = true;
            s.Columns().AdjustToContents(8, 40);
            s.SheetView.FreezeRows(1);
        }

        SummarySheet("Summary by Cost Centre", "Project No/ Cost Code",
            rows.GroupBy(e => string.IsNullOrWhiteSpace(e.ProjectCostCode) ? "(blank)" : e.ProjectCostCode!));
        SummarySheet("Summary by Head Count", "NAME",
            rows.GroupBy(e => string.IsNullOrWhiteSpace(e.StaffName) ? "(blank)" : e.StaffName));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Feeding_Log_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
