namespace GenService.API.Domain;

/// <summary>
/// One occurrence of a scheduled task actually being carried out — the date the
/// fumigation, AC service, tank cleaning or evacuation drill really happened.
///
/// The schedule itself only remembers its *last* completion, overwriting it each
/// cycle, so there was no history to show an auditor or the manager. Every
/// "Done" now writes one of these, and the history is kept permanently.
/// Task details are copied in, so the log still reads correctly if the
/// schedule is later renamed or retired.
/// </summary>
public class MaintenanceScheduleCompletion
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public Guid     ScheduleId    { get; set; }

    public string   TaskName      { get; set; } = "";
    public string   Category      { get; set; } = "";
    public string?  Location      { get; set; }

    /// <summary>The date the work was actually done — may be earlier than the day it was logged.</summary>
    public DateOnly CompletedOn   { get; set; }

    /// <summary>Who carried it out — a staff member or an outside vendor.</summary>
    public string?  DoneBy        { get; set; }
    public string?  Notes         { get; set; }

    public string   LoggedByEmail { get; set; } = "";
    public string   LoggedByName  { get; set; } = "";
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
}
