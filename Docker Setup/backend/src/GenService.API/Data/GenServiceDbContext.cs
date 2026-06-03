using GenService.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Data;

public class GenServiceDbContext(DbContextOptions<GenServiceDbContext> options)
    : DbContext(options)
{
    public DbSet<ServiceRequest>             ServiceRequests             => Set<ServiceRequest>();
    public DbSet<StaffActivity>             StaffActivities             => Set<StaffActivity>();
    public DbSet<MaintenanceSchedule>       MaintenanceSchedules        => Set<MaintenanceSchedule>();
    public DbSet<GeneratorLog>              GeneratorLogs               => Set<GeneratorLog>();
    public DbSet<DieselRecord>              DieselRecords               => Set<DieselRecord>();
    public DbSet<VehicleMaintenanceRequest>    VehicleMaintenanceRequests    => Set<VehicleMaintenanceRequest>();
    public DbSet<AppNotification>              AppNotifications              => Set<AppNotification>();
    public DbSet<TaskProgressLog>              TaskProgressLogs              => Set<TaskProgressLog>();
    public DbSet<GeneratorDailyReading>        GeneratorDailyReadings        => Set<GeneratorDailyReading>();
    public DbSet<PowerMeterReading>            PowerMeterReadings            => Set<PowerMeterReading>();
    public DbSet<AuditEntry>                   AuditEntries                  => Set<AuditEntry>();
    public DbSet<DieselTankReading>            DieselTankReadings            => Set<DieselTankReading>();
    public DbSet<EquipmentMaintenanceRequest>  EquipmentMaintenanceRequests  => Set<EquipmentMaintenanceRequest>();
    public DbSet<FacilityMaintenanceRequest>   FacilityMaintenanceRequests   => Set<FacilityMaintenanceRequest>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ServiceRequest
        mb.Entity<ServiceRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TicketNumber)    .HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.TicketNumber).IsUnique();
            e.Property(x => x.Title)           .HasMaxLength(250).IsRequired();
            e.Property(x => x.Description)     .HasMaxLength(4000);
            e.Property(x => x.Category)        .HasMaxLength(50).IsRequired();
            e.Property(x => x.Status)          .HasMaxLength(30).IsRequired();
            e.Property(x => x.Priority)        .HasMaxLength(20).IsRequired();
            e.Property(x => x.Location)        .HasMaxLength(200);
            e.Property(x => x.RequestedByEmail).HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName) .HasMaxLength(100).IsRequired();
            e.Property(x => x.AssignedToEmail) .HasMaxLength(150);
            e.Property(x => x.AssignedToName)  .HasMaxLength(100);
            e.Property(x => x.ApprovedByEmail) .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)  .HasMaxLength(100);
            e.Property(x => x.LineManagerEmail)      .HasMaxLength(150);
            e.Property(x => x.LineManagerName)       .HasMaxLength(100);
            e.Property(x => x.RejectionReason)       .HasMaxLength(1000);
            e.Property(x => x.Notes)                 .HasMaxLength(4000);
            e.Property(x => x.ReassignedToType)      .HasMaxLength(50);
            e.Property(x => x.ReassignedToName)      .HasMaxLength(200);
            e.Property(x => x.ReassignedNotes)       .HasMaxLength(2000);
        });

        // AppNotification
        mb.Entity<AppNotification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title)       .HasMaxLength(200).IsRequired();
            e.Property(x => x.Message)     .HasMaxLength(500).IsRequired();
            e.Property(x => x.Type)        .HasMaxLength(50).IsRequired();
            e.Property(x => x.Module)      .HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityId)    .HasMaxLength(50);
            e.Property(x => x.RefNumber)   .HasMaxLength(30);
            e.Property(x => x.TargetRole)  .HasMaxLength(30).IsRequired();
            e.Property(x => x.TargetEmail) .HasMaxLength(150);
            e.HasIndex(x => new { x.TargetRole, x.IsRead });
        });

        // StaffActivity
        mb.Entity<StaffActivity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StaffEmail)          .HasMaxLength(150).IsRequired();
            e.Property(x => x.StaffName)           .HasMaxLength(100).IsRequired();
            e.Property(x => x.ActivityDescription) .HasMaxLength(500).IsRequired();
            e.Property(x => x.Location)            .HasMaxLength(200);
            e.Property(x => x.Category)            .HasMaxLength(50).IsRequired();
            e.Property(x => x.Status)              .HasMaxLength(30).IsRequired();
            e.Property(x => x.LoggedByEmail)       .HasMaxLength(150).IsRequired();
            e.Property(x => x.LoggedByName)        .HasMaxLength(100).IsRequired();
            e.Property(x => x.Notes)               .HasMaxLength(2000);
            e.HasIndex(x => new { x.StaffEmail, x.Status });
        });

        // MaintenanceSchedule
        mb.Entity<MaintenanceSchedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TaskName)            .HasMaxLength(200).IsRequired();
            e.Property(x => x.Description)         .HasMaxLength(2000);
            e.Property(x => x.Category)            .HasMaxLength(50).IsRequired();
            e.Property(x => x.Location)            .HasMaxLength(200);
            e.Property(x => x.FrequencyLabel)      .HasMaxLength(30).IsRequired();
            e.Property(x => x.AssignedToEmail)     .HasMaxLength(150);
            e.Property(x => x.AssignedToName)      .HasMaxLength(100);
            e.Property(x => x.LastCompletedByEmail).HasMaxLength(150);
            e.Property(x => x.LastCompletedByName) .HasMaxLength(100);
            e.Property(x => x.LastCompletionNotes) .HasMaxLength(2000);
            e.Ignore(x => x.IsOverdue);
        });

        // GeneratorLog
        mb.Entity<GeneratorLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Location)     .HasMaxLength(200).IsRequired();
            e.Property(x => x.RunReason)    .HasMaxLength(50).IsRequired();
            e.Property(x => x.Status)       .HasMaxLength(30).IsRequired();
            e.Property(x => x.OutageCause)  .HasMaxLength(200);
            e.Property(x => x.Notes)        .HasMaxLength(2000);
            e.Property(x => x.LoggedByEmail).HasMaxLength(150).IsRequired();
            e.Property(x => x.LoggedByName) .HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.Location, x.StartTime });
        });

        // VehicleMaintenanceRequest
        mb.Entity<VehicleMaintenanceRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestNumber)    .HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.RequestNumber).IsUnique();
            e.Property(x => x.VehicleRegNo)     .HasMaxLength(20).IsRequired();
            e.Property(x => x.VehicleType)      .HasMaxLength(100).IsRequired();
            e.Property(x => x.MaintenanceType)  .HasMaxLength(50).IsRequired();
            e.Property(x => x.Description)      .HasMaxLength(2000);
            e.Property(x => x.Priority)         .HasMaxLength(20).IsRequired();
            e.Property(x => x.Status)           .HasMaxLength(30).IsRequired();
            e.Property(x => x.CurrentLocation)  .HasMaxLength(200);
            e.Property(x => x.RequestedByEmail) .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName)  .HasMaxLength(100).IsRequired();
            e.Property(x => x.ApprovedByEmail)  .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)   .HasMaxLength(100);
            e.Property(x => x.RejectionReason)  .HasMaxLength(1000);
            e.Property(x => x.WorkshopName)     .HasMaxLength(200);
            e.Property(x => x.WorkshopLocation) .HasMaxLength(200);
            e.Property(x => x.Notes)            .HasMaxLength(2000);
            e.HasIndex(x => new { x.VehicleRegNo, x.Status });
        });

        // DieselRecord
        mb.Entity<DieselRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RecordType)       .HasMaxLength(30).IsRequired();
            e.Property(x => x.Supplier)         .HasMaxLength(150);
            e.Property(x => x.Destination)      .HasMaxLength(200);
            e.Property(x => x.RequestedByEmail) .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName)  .HasMaxLength(100).IsRequired();
            e.Property(x => x.ApprovedByEmail)  .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)   .HasMaxLength(100);
            e.Property(x => x.Notes)            .HasMaxLength(2000);
            e.Property(x => x.UnitCostNaira)    .HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalCostNaira)   .HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.RecordDate);
        });

        // EquipmentMaintenanceRequest
        mb.Entity<EquipmentMaintenanceRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestNumber)    .HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.RequestNumber).IsUnique();
            e.Property(x => x.AssetNo)          .HasMaxLength(50).IsRequired();
            e.Property(x => x.AssetDescription) .HasMaxLength(300).IsRequired();
            e.Property(x => x.MaintenanceType)  .HasMaxLength(50).IsRequired();
            e.Property(x => x.EndUser)          .HasMaxLength(100).IsRequired();
            e.Property(x => x.Location)         .HasMaxLength(200).IsRequired();
            e.Property(x => x.Description)      .HasMaxLength(2000);
            e.Property(x => x.Priority)         .HasMaxLength(20).IsRequired();
            e.Property(x => x.Status)           .HasMaxLength(30).IsRequired();
            e.Property(x => x.RequestedByEmail) .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName)  .HasMaxLength(100).IsRequired();
            e.Property(x => x.ApprovedByEmail)  .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)   .HasMaxLength(100);
            e.Property(x => x.RejectionReason)  .HasMaxLength(1000);
            e.Property(x => x.WorkDone)         .HasMaxLength(2000);
            e.Property(x => x.ActionedBy)       .HasMaxLength(200);
            e.Property(x => x.Notes)            .HasMaxLength(2000);
        });

        // FacilityMaintenanceRequest
        mb.Entity<FacilityMaintenanceRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestNumber)    .HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.RequestNumber).IsUnique();
            e.Property(x => x.MaintenanceType)  .HasMaxLength(50).IsRequired();
            e.Property(x => x.Description)      .HasMaxLength(2000).IsRequired();
            e.Property(x => x.Location)         .HasMaxLength(200).IsRequired();
            e.Property(x => x.EndUser)          .HasMaxLength(100).IsRequired();
            e.Property(x => x.RoomFlat)         .HasMaxLength(200);
            e.Property(x => x.Priority)         .HasMaxLength(20).IsRequired();
            e.Property(x => x.Status)           .HasMaxLength(30).IsRequired();
            e.Property(x => x.RequestedByEmail) .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName)  .HasMaxLength(100).IsRequired();
            e.Property(x => x.ApprovedByEmail)  .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)   .HasMaxLength(100);
            e.Property(x => x.RejectionReason)  .HasMaxLength(1000);
            e.Property(x => x.WorkDone)         .HasMaxLength(2000);
            e.Property(x => x.ActionedBy)       .HasMaxLength(200);
            e.Property(x => x.Notes)            .HasMaxLength(2000);
        });

        // TaskProgressLog
        mb.Entity<TaskProgressLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Module)             .HasMaxLength(30).IsRequired();
            e.Property(x => x.EntityId)           .HasMaxLength(50).IsRequired();
            e.Property(x => x.RefNumber)          .HasMaxLength(30).IsRequired();
            e.Property(x => x.TaskTitle)          .HasMaxLength(300).IsRequired();
            e.Property(x => x.ActivityPerformed)  .HasMaxLength(2000).IsRequired();
            e.Property(x => x.ProgressStatus)     .HasMaxLength(50).IsRequired();
            e.Property(x => x.MaterialsRequired)  .HasMaxLength(1000);
            e.Property(x => x.NextAction)         .HasMaxLength(1000);
            e.Property(x => x.LoggedByEmail)      .HasMaxLength(150).IsRequired();
            e.Property(x => x.LoggedByName)       .HasMaxLength(100).IsRequired();
            e.Property(x => x.ProxyForName)       .HasMaxLength(100);
            e.HasIndex(x => new { x.Module, x.EntityId });
            e.HasIndex(x => new { x.LoggedByEmail, x.LogDate });
        });

        // GeneratorDailyReading
        mb.Entity<GeneratorDailyReading>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AssetNo)          .HasMaxLength(50).IsRequired();
            e.Property(x => x.AssetDescription) .HasMaxLength(300).IsRequired();
            e.Property(x => x.Location)         .HasMaxLength(200).IsRequired();
            e.Property(x => x.GeneratorStatus)  .HasMaxLength(30).IsRequired();
            e.Property(x => x.Notes)            .HasMaxLength(2000);
            e.Property(x => x.LoggedByEmail)    .HasMaxLength(150).IsRequired();
            e.Property(x => x.LoggedByName)     .HasMaxLength(100).IsRequired();
            e.Ignore(x => x.HoursSinceLastService);
            e.Ignore(x => x.HoursUntilNextService);
            e.HasIndex(x => new { x.AssetNo, x.ReadingDate });
        });

        // PowerMeterReading
        mb.Entity<PowerMeterReading>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Location)     .HasMaxLength(200).IsRequired();
            e.Property(x => x.MeterNumber)  .HasMaxLength(50).IsRequired();
            e.Property(x => x.CostPerKwhNaira)          .HasColumnType("decimal(10,2)");
            e.Property(x => x.TotalElectricityCostNaira).HasColumnType("decimal(18,2)");
            e.Property(x => x.Notes)        .HasMaxLength(2000);
            e.Property(x => x.LoggedByEmail).HasMaxLength(150).IsRequired();
            e.Property(x => x.LoggedByName) .HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.Location, x.ReadingDate });
        });

        // DieselTankReading
        mb.Entity<DieselTankReading>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Location)        .HasMaxLength(200).IsRequired();
            e.Property(x => x.TankIdentifier)  .HasMaxLength(200).IsRequired();
            e.Property(x => x.Notes)           .HasMaxLength(2000);
            e.Property(x => x.LoggedByEmail)   .HasMaxLength(150).IsRequired();
            e.Property(x => x.LoggedByName)    .HasMaxLength(100).IsRequired();
            e.Property(x => x.CostPerLitreNaira)           .HasColumnType("decimal(10,2)");
            e.Property(x => x.TotalConsumptionCostNaira)   .HasColumnType("decimal(18,2)");
            e.HasIndex(x => new { x.Location, x.TankIdentifier, x.ReadingDate });
        });

        // AuditEntry — immutable, never updated
        mb.Entity<AuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType)        .HasMaxLength(30).IsRequired();
            e.Property(x => x.EntityId)          .HasMaxLength(50).IsRequired();
            e.Property(x => x.RefNumber)         .HasMaxLength(30).IsRequired();
            e.Property(x => x.Action)            .HasMaxLength(50).IsRequired();
            e.Property(x => x.OldValue)          .HasMaxLength(200);
            e.Property(x => x.NewValue)          .HasMaxLength(200);
            e.Property(x => x.Details)           .HasMaxLength(1000);
            e.Property(x => x.PerformedByEmail)  .HasMaxLength(150).IsRequired();
            e.Property(x => x.PerformedByName)   .HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.Timestamp);
        });
    }
}
