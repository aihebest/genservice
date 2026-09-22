using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GenService.API.Data;
using GenService.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Services;

/// <summary>
/// Produces the department's "Repairs and Maintenance Register" workbook with
/// live platform data.
///
/// Why this is not built with ClosedXML like the other exports: the value of
/// this workbook to the General Service Manager is the DashBoard — 33 charts,
/// 15 pivot tables and 7 slicers built over the Vehicle, Equipment and Facility
/// tables. ClosedXML cannot create charts, and round-tripping the file through
/// it would drop them. So instead the department's own workbook ships as a
/// template with its data rows removed, and this service rewrites only the rows
/// of those three sheets using the raw OpenXML SDK. Everything else in the
/// package — charts, pivot caches, slicers, drawings, conditional formats — is
/// never opened and comes through byte-for-byte.
///
/// The pivots source from the Excel Tables named Vehicle/Equipment/Facility
/// rather than fixed ranges, so extending each table's ref to cover the rows we
/// wrote is all that is needed for the whole dashboard to follow. The template
/// carries refreshOnLoad and fullCalcOnLoad, so Excel rebuilds the pivots and
/// the Data_Analysis formulas the moment the file is opened.
/// </summary>
public class MaintenanceRegisterExportService(
    GenServiceDbContext db,
    ILogger<MaintenanceRegisterExportService> logger)
{
    private const string TemplateFile = "MaintenanceRegister.xlsx";

    /// <summary>Row the data starts on. Rows 1–8 are the title band and headers.</summary>
    private const uint FirstDataRow = 9;

    // Sheet name → (Excel Table name, first column, last column)
    private static readonly (string Sheet, string Table, string First, string Last) VehicleSpec
        = ("Vehicle", "Vehicle", "C", "V");
    private static readonly (string Sheet, string Table, string First, string Last) EquipmentSpec
        = ("Equipment", "Equipment", "C", "U");
    private static readonly (string Sheet, string Table, string First, string Last) FacilitySpec
        = ("Facility", "Facility", "C", "P");

    public string FileName(DateOnly? from, DateOnly? to)
    {
        var stamp = (from, to) switch
        {
            (not null, not null) => $"{from:dd-MM-yy}_to_{to:dd-MM-yy}",
            (not null, null)     => $"from_{from:dd-MM-yy}",
            (null, not null)     => $"to_{to:dd-MM-yy}",
            _                    => DateTime.UtcNow.ToString("dd-MM-yy"),
        };
        return $"{stamp} REPAIRS AND MAINTENANCE REGISTER.xlsx";
    }

    public async Task<byte[]> BuildAsync(
        DateOnly? from, DateOnly? to, string? location, CancellationToken ct = default)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", TemplateFile);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException(
                $"The register template is missing from the deployment ({templatePath}).", templatePath);

        var vehicles  = await LoadVehiclesAsync(from, to, location, ct);
        var equipment = await LoadEquipmentAsync(from, to, location, ct);
        var facility  = await LoadFacilityAsync(from, to, location, ct);

        // Work on a copy in memory — the template on disk is read-only input.
        var ms = new MemoryStream();
        await using (var src = File.OpenRead(templatePath))
            await src.CopyToAsync(ms, ct);
        ms.Position = 0;

        using (var doc = SpreadsheetDocument.Open(ms, isEditable: true))
        {
            var wb = doc.WorkbookPart
                     ?? throw new InvalidOperationException("Register template has no workbook part.");

            WriteSheet(wb, VehicleSpec,   vehicles);
            WriteSheet(wb, EquipmentSpec, equipment);
            WriteSheet(wb, FacilitySpec,  facility);

            wb.Workbook.Save();
        }

        logger.LogInformation(
            "Maintenance register exported: {Veh} vehicle, {Eq} equipment, {Fac} facility rows.",
            vehicles.Count, equipment.Count, facility.Count);

        return ms.ToArray();
    }

    // ── Sheet writing ─────────────────────────────────────────────────────────

    private static void WriteSheet(
        WorkbookPart wb,
        (string Sheet, string Table, string First, string Last) spec,
        List<object?[]> rows)
    {
        var wsPart = FindWorksheet(wb, spec.Sheet)
            ?? throw new InvalidOperationException($"Register template has no '{spec.Sheet}' sheet.");

        var sheetData = wsPart.Worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException($"'{spec.Sheet}' has no sheet data.");

        var firstCol = ColumnIndex(spec.First);
        var lastCol  = ColumnIndex(spec.Last);

        // The template's row 9 carries one styled cell per column. Take those
        // style ids, then discard the row — every row we write is a clone of it,
        // so dates, currency and borders match the department's formatting
        // without this service knowing anything about number formats.
        var styles = new Dictionary<int, uint>();
        foreach (var row in sheetData.Elements<Row>().Where(r => r.RowIndex?.Value >= FirstDataRow).ToList())
        {
            if (row.RowIndex?.Value == FirstDataRow)
                foreach (var cell in row.Elements<Cell>())
                {
                    var col = ColumnIndex(ColumnOf(cell.CellReference?.Value ?? ""));
                    if (col > 0 && cell.StyleIndex is not null)
                        styles[col] = cell.StyleIndex.Value;
                }
            row.Remove();
        }

        uint rowIndex = FirstDataRow;
        foreach (var values in rows)
        {
            var row = new Row { RowIndex = rowIndex };

            for (int col = firstCol; col <= lastCol; col++)
            {
                var value = values.ElementAtOrDefault(col - firstCol);
                var cell  = new Cell { CellReference = $"{ColumnName(col)}{rowIndex}" };
                if (styles.TryGetValue(col, out var style)) cell.StyleIndex = style;

                switch (value)
                {
                    case null:
                        break;

                    // Dates are written as Excel serial numbers so the template's
                    // own date format renders them — not as text, which would
                    // break sorting and the timeline slicer.
                    case DateTime dt:
                        cell.CellValue = new CellValue(
                            dt.ToOADate().ToString(CultureInfo.InvariantCulture));
                        break;

                    case decimal dec:
                        cell.CellValue = new CellValue(dec.ToString(CultureInfo.InvariantCulture));
                        break;

                    case double dbl:
                        cell.CellValue = new CellValue(dbl.ToString(CultureInfo.InvariantCulture));
                        break;

                    case int i:
                        cell.CellValue = new CellValue(i.ToString(CultureInfo.InvariantCulture));
                        break;

                    default:
                        // InlineString rather than a shared string: it keeps the
                        // shared-string table untouched, which matters because the
                        // pivot caches were built against it.
                        cell.DataType = CellValues.InlineString;
                        cell.AppendChild(new InlineString(new Text(Clean(value.ToString()))));
                        break;
                }

                row.AppendChild(cell);
            }

            sheetData.AppendChild(row);
            rowIndex++;
        }

        // An empty result still needs one row, or the table reference collapses
        // into the header and Excel reports the file as corrupt.
        var lastRow = rows.Count == 0 ? FirstDataRow : rowIndex - 1;
        if (rows.Count == 0)
            sheetData.AppendChild(BlankRow(FirstDataRow, firstCol, lastCol, styles));

        var dimension = wsPart.Worksheet.GetFirstChild<SheetDimension>();
        if (dimension is not null) dimension.Reference = $"A1:{spec.Last}{lastRow}";

        wsPart.Worksheet.Save();

        // Extend the Excel Table so the pivots — and therefore every chart on the
        // DashBoard — cover the rows just written.
        var tableRef = $"{spec.First}8:{spec.Last}{lastRow}";
        foreach (var tdp in wsPart.TableDefinitionParts)
        {
            if (!string.Equals(tdp.Table.Name?.Value, spec.Table, StringComparison.OrdinalIgnoreCase))
                continue;
            tdp.Table.Reference = tableRef;
            if (tdp.Table.AutoFilter is not null) tdp.Table.AutoFilter.Reference = tableRef;
            tdp.Table.Save();
        }
    }

    private static Row BlankRow(
        uint rowIndex, int firstCol, int lastCol, Dictionary<int, uint> styles)
    {
        var row = new Row { RowIndex = rowIndex };
        for (int col = firstCol; col <= lastCol; col++)
        {
            var cell = new Cell { CellReference = $"{ColumnName(col)}{rowIndex}" };
            if (styles.TryGetValue(col, out var s)) cell.StyleIndex = s;
            row.AppendChild(cell);
        }
        return row;
    }

    private static WorksheetPart? FindWorksheet(WorkbookPart wb, string sheetName)
    {
        var sheet = wb.Workbook.Descendants<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, sheetName, StringComparison.OrdinalIgnoreCase));
        return sheet?.Id?.Value is string id ? (WorksheetPart)wb.GetPartById(id) : null;
    }

    // ── Row builders — column order matches the register exactly ──────────────

    private async Task<List<object?[]>> LoadVehiclesAsync(
        DateOnly? from, DateOnly? to, string? location, CancellationToken ct)
    {
        var q = db.VehicleMaintenanceRequests.AsNoTracking()
                  .Where(r => r.Status != VehicleMaintenanceStatus.Rejected);
        if (!string.IsNullOrWhiteSpace(location))
            q = q.Where(r => r.CurrentLocation == location);

        var rows = await q.ToListAsync(ct);

        return rows
            .Select(r => new { r, When = r.DateOfRequest ?? r.CreatedAt })
            .Where(x => InRange(x.When, from, to))
            .OrderBy(x => x.When)
            .Select(x =>
            {
                var r = x.r;
                return new object?[]
                {
                    x.When.ToString("MMM", CultureInfo.InvariantCulture),   // MONTH
                    x.When.Date,                                           // DATE OF REQUEST
                    r.RequestedByName,                                     // REQUESTOR
                    r.RequestNumber,                                       // MRSF No.
                    r.NotificationStatus ?? "Open",                        // NOTIFICATION STATUS
                    r.CurrentLocation,                                     // REPAIR / MAINTENANCE LOCATION
                    r.AssetNo,                                             // ASSET NO.
                    // The register writes the plate and the vehicle together.
                    string.IsNullOrWhiteSpace(r.VehicleType)
                        ? r.VehicleRegNo
                        : $"{r.VehicleRegNo} - {r.VehicleType}",           // VEHICLE REG NO.
                    r.VehicleType,                                         // VEHICLE DESCRIPTION
                    Numeric(r.OdometerReading),                            // ODOMETER READING
                    r.Description,                                         // DESCRIPTION OF REQUEST
                    r.WorkDone,                                            // DESCRIPTION OF WORK DONE
                    r.PartsSuppliedBy,                                     // PARTS SUPPLIED BY
                    r.WorkshopName,                                        // WORK DONE AT
                    r.DateDeliveredToWorkshop?.Date,                       // DATE TAKEN TO WORKSHOP
                    StatusOfWork(r.Status),                                // STATUS OF WORK
                    r.CompletedAt?.Date,                                   // DATE COMPLETED
                    DaysTaken(r.DateDeliveredToWorkshop, r.CompletedAt),   // DAYS TAKEN TO COMPLETE
                    r.FinalAmountNaira ?? r.SparesCostNaira,               // SPARES COST (NGN)
                    r.JustificationEvaluation,                             // JUSTIFICATION / EVALUATION
                };
            })
            .ToList();
    }

    private async Task<List<object?[]>> LoadEquipmentAsync(
        DateOnly? from, DateOnly? to, string? location, CancellationToken ct)
    {
        var q = db.EquipmentMaintenanceRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(location))
            q = q.Where(r => r.Location == location);

        var rows = await q.ToListAsync(ct);

        return rows
            .Select(r => new { r, When = r.DateOfRequest ?? r.CreatedAt })
            .Where(x => InRange(x.When, from, to))
            .OrderBy(x => x.When)
            .Select(x =>
            {
                var r = x.r;
                return new object?[]
                {
                    x.When.ToString("MMM", CultureInfo.InvariantCulture),   // MONTH
                    x.When.Date,                                           // DATE OF REQUEST
                    r.Requestor ?? r.RequestedByName,                      // REQUESTOR
                    r.RequestNumber,                                       // MRSF No.
                    r.NotificationStatus ?? "Open",                        // NOTIFICATION STATUS
                    r.EndUser,                                             // END USER
                    r.Location,                                            // LOCATION
                    r.AssetNo,                                             // ASSET NO.
                    r.AssetDescription,                                    // ASSET DESCRIPTION
                    r.Description,                                         // DESCRIPTION OF REQUEST
                    r.RunningHours,                                        // GENERATOR RUNNING HOURS
                    r.NextServiceHour,                                     // NEXT SERVICE HOUR
                    r.WorkDone,                                            // DESCRIPTION OF WORK DONE
                    r.PartsSuppliedBy,                                     // PARTS SUPPLIED BY
                    r.ActionedBy,                                          // WORK DONE BY
                    StatusOfWork(r.Status),                                // STATUS OF WORK
                    r.CompletedAt?.Date,                                   // DATE COMPLETED
                    r.FinalAmountNaira ?? r.SparesCostNaira,               // SPARES COST
                    r.JustificationEvaluation,                             // JUSTIFICATION / EVALUATION
                };
            })
            .ToList();
    }

    private async Task<List<object?[]>> LoadFacilityAsync(
        DateOnly? from, DateOnly? to, string? location, CancellationToken ct)
    {
        var q = db.FacilityMaintenanceRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(location))
            q = q.Where(r => r.Location == location);

        var rows = await q.ToListAsync(ct);

        return rows
            .Select(r => new { r, When = r.DateOfRequest ?? r.CreatedAt })
            .Where(x => InRange(x.When, from, to))
            .OrderBy(x => x.When)
            .Select(x =>
            {
                var r = x.r;
                return new object?[]
                {
                    x.When.ToString("MMM", CultureInfo.InvariantCulture),   // MONTH
                    x.When.Date,                                           // DATE OF REQUEST
                    r.Requestor ?? r.RequestedByName,                      // REQUESTOR
                    r.RequestNumber,                                       // MRSF No.
                    r.NotificationStatus ?? "Open",                        // NOTIFICATION STATUS
                    r.EndUser,                                             // END USER
                    r.Location,                                            // LOCATION
                    r.RoomFlat,                                            // ROOM / FLAT
                    // The register carries a constant "FACILITY" in this column —
                    // there is no per-record asset description on the facility side.
                    "FACILITY",                                            // ASSET DESCRIPTION
                    r.Description,                                         // DESCRIPTION OF REQUEST
                    r.WorkDone,                                            // DESCRIPTION OF WORK DONE
                    r.ActionedBy,                                          // NAME
                    StatusOfWork(r.Status),                                // STATUS OF WORK
                    r.FinalAmountNaira ?? r.SparesCostNaira,               // SPARES COST (NGN)
                };
            })
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool InRange(DateTime when, DateOnly? from, DateOnly? to)
    {
        var d = DateOnly.FromDateTime(when);
        if (from is not null && d < from) return false;
        if (to   is not null && d > to)   return false;
        return true;
    }

    /// <summary>
    /// The register records work status in the department's words, not the
    /// platform's workflow states.
    /// </summary>
    private static string StatusOfWork(string status) => status switch
    {
        VehicleMaintenanceStatus.Completed     => "Completed",
        VehicleMaintenanceStatus.Pending       => "Pending",
        VehicleMaintenanceStatus.Approved      => "Pending",
        VehicleMaintenanceStatus.InWorkshop    => "In Progress",
        VehicleMaintenanceStatus.AwaitingParts => "Awaiting Parts",
        VehicleMaintenanceStatus.AwaitingFunds => "Awaiting Funds",
        _                                      => status,
    };

    private static int? DaysTaken(DateTime? takenIn, DateTime? completed)
        => takenIn is null || completed is null
            ? null
            : Math.Max(0, (int)(completed.Value.Date - takenIn.Value.Date).TotalDays);

    /// <summary>Odometer is free text in the platform but a number in the register.</summary>
    private static object? Numeric(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : raw;
    }

    /// <summary>Strip control characters that would make Excel report the file as corrupt.</summary>
    private static string Clean(string? s)
        => string.IsNullOrEmpty(s)
            ? ""
            : new string(s.Where(c => c == '\n' || c == '\t' || !char.IsControl(c)).ToArray());

    private static string ColumnOf(string cellRef)
        => new(cellRef.TakeWhile(char.IsLetter).ToArray());

    private static int ColumnIndex(string column)
    {
        var n = 0;
        foreach (var c in column.ToUpperInvariant())
        {
            if (c < 'A' || c > 'Z') return 0;
            n = n * 26 + (c - 'A' + 1);
        }
        return n;
    }

    private static string ColumnName(int index)
    {
        var s = "";
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            s = (char)('A' + rem) + s;
            index = (index - 1) / 26;
        }
        return s;
    }
}
