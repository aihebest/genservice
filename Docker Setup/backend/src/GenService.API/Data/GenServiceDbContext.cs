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
    public DbSet<DailyParameterLog>            DailyParameterLogs            => Set<DailyParameterLog>();
    public DbSet<AppUser>                      AppUsers                      => Set<AppUser>();
    public DbSet<StoreItem>                    StoreItems                    => Set<StoreItem>();
    public DbSet<StoreRequisition>             StoreRequisitions             => Set<StoreRequisition>();
    public DbSet<StoreRequisitionItem>         StoreRequisitionItems         => Set<StoreRequisitionItem>();
    public DbSet<StoreMovement>                StoreMovements                => Set<StoreMovement>();
    public DbSet<DieselRequisition>            DieselRequisitions            => Set<DieselRequisition>();

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
            e.Property(x => x.VehicleType)      .HasMaxLength(200).IsRequired();
            e.Property(x => x.MaintenanceType)  .HasMaxLength(50).IsRequired();
            e.Property(x => x.Description)      .HasMaxLength(2000);
            e.Property(x => x.Priority)         .HasMaxLength(20).IsRequired();
            e.Property(x => x.Status)           .HasMaxLength(30).IsRequired();
            e.Property(x => x.CurrentLocation)  .HasMaxLength(200);
            e.Property(x => x.OdometerReading)  .HasMaxLength(100);
            e.Property(x => x.RequestedByEmail) .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName)  .HasMaxLength(100).IsRequired();
            e.Property(x => x.ApprovedByEmail)  .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)   .HasMaxLength(100);
            e.Property(x => x.RejectionReason)  .HasMaxLength(1000);
            // Workshop
            e.Property(x => x.WorkshopName)     .HasMaxLength(200);
            e.Property(x => x.WorkshopLocation) .HasMaxLength(200);
            // Assessment
            e.Property(x => x.FaultIdentified)  .HasMaxLength(2000);
            e.Property(x => x.ProposedSolution) .HasMaxLength(2000);
            e.Property(x => x.ResolutionType)   .HasMaxLength(30);
            // Parts
            e.Property(x => x.PartsRequired)    .HasDefaultValue(false);
            e.Property(x => x.PartsSource)      .HasMaxLength(30);
            e.Property(x => x.ProcurementMethod).HasMaxLength(30);
            e.Property(x => x.PartsSuppliedBy)  .HasMaxLength(200);
            e.Property(x => x.SparesCostNaira)  .HasColumnType("decimal(18,2)");
            // Completion
            e.Property(x => x.WorkDone)         .HasMaxLength(2000);
            e.Property(x => x.ActionedBy)       .HasMaxLength(200);
            // Handover
            e.Property(x => x.HandoverConfirmed).HasDefaultValue(false);
            e.Property(x => x.HandedOverBy)     .HasMaxLength(100);
            e.Property(x => x.Notes)            .HasMaxLength(2000);
            e.HasIndex(x => new { x.VehicleRegNo, x.Status });
            e.HasIndex(x => x.Status);
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
            // Assessment
            e.Property(x => x.FaultIdentified)  .HasMaxLength(2000);
            e.Property(x => x.ProposedSolution) .HasMaxLength(2000);
            e.Property(x => x.ResolutionType)   .HasMaxLength(30);
            // Parts
            e.Property(x => x.PartsRequired)    .HasDefaultValue(false);
            e.Property(x => x.PartsSource)      .HasMaxLength(30);
            e.Property(x => x.ProcurementMethod).HasMaxLength(30);
            e.Property(x => x.SparesCostNaira)  .HasColumnType("decimal(18,2)");
            // Completion
            e.Property(x => x.WorkDone)         .HasMaxLength(2000);
            e.Property(x => x.ActionedBy)       .HasMaxLength(200);
            // Handover
            e.Property(x => x.HandoverConfirmed).HasDefaultValue(false);
            e.Property(x => x.HandedOverBy)     .HasMaxLength(100);
            e.Property(x => x.Notes)            .HasMaxLength(2000);
            e.HasIndex(x => x.Status);
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
            // Assessment
            e.Property(x => x.FaultIdentified)  .HasMaxLength(2000);
            e.Property(x => x.ProposedSolution) .HasMaxLength(2000);
            e.Property(x => x.ResolutionType)   .HasMaxLength(30);
            // Parts
            e.Property(x => x.PartsRequired)    .HasDefaultValue(false);
            e.Property(x => x.PartsSource)      .HasMaxLength(30);
            e.Property(x => x.ProcurementMethod).HasMaxLength(30);
            e.Property(x => x.SparesCostNaira)  .HasColumnType("decimal(18,2)");
            // Completion
            e.Property(x => x.WorkDone)         .HasMaxLength(2000);
            e.Property(x => x.ActionedBy)       .HasMaxLength(200);
            // Handover
            e.Property(x => x.HandoverConfirmed).HasDefaultValue(false);
            e.Property(x => x.HandedOverBy)     .HasMaxLength(100);
            e.Property(x => x.Notes)            .HasMaxLength(2000);
            e.HasIndex(x => x.Status);
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

        // DailyParameterLog
        mb.Entity<DailyParameterLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Location)            .HasMaxLength(200).IsRequired();
            e.Property(x => x.GeneratorStatus)     .HasMaxLength(30);
            e.Property(x => x.WaterSource)         .HasMaxLength(30);
            e.Property(x => x.WaterStatus)         .HasMaxLength(30);
            e.Property(x => x.SecurityStatus)      .HasMaxLength(50);
            e.Property(x => x.MaintenanceIssues)   .HasMaxLength(2000);
            e.Property(x => x.ActionsTaken)        .HasMaxLength(2000);
            e.Property(x => x.PendingActions)      .HasMaxLength(2000);
            e.Property(x => x.GeneralRemarks)      .HasMaxLength(2000);
            e.Property(x => x.LoggedByEmail)       .HasMaxLength(150).IsRequired();
            e.Property(x => x.LoggedByName)        .HasMaxLength(100).IsRequired();
            e.Property(x => x.CleaningDone)        .HasDefaultValue(false);
            e.Property(x => x.WasteDisposed)       .HasDefaultValue(false);
            e.HasIndex(x => new { x.Location, x.LogDate }).IsUnique();
            e.HasIndex(x => x.LogDate);
        });

        // AppUser
        mb.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email)          .HasMaxLength(150).IsRequired();
            e.Property(x => x.FullName)       .HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash)   .HasMaxLength(100).IsRequired();
            e.Property(x => x.Role)           .HasMaxLength(30) .IsRequired();
            e.Property(x => x.Department)     .HasMaxLength(100).IsRequired();
            e.Property(x => x.CreatedByEmail) .HasMaxLength(150);
            e.Property(x => x.IsActive)       .HasDefaultValue(true);
            e.HasIndex(x => x.Email)          .IsUnique();
            e.HasIndex(x => x.Role);
        });

        // StoreItem
        mb.Entity<StoreItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemCode)      .HasMaxLength(20) .IsRequired();
            e.Property(x => x.Name)          .HasMaxLength(200).IsRequired();
            e.Property(x => x.Category)      .HasMaxLength(60) .IsRequired();
            e.Property(x => x.Unit)          .HasMaxLength(30) .IsRequired();
            e.Property(x => x.UnitCostNaira) .HasColumnType("decimal(18,2)");
            e.Property(x => x.Description)   .HasMaxLength(500);
            e.Property(x => x.StoreLocation) .HasMaxLength(200);
            e.Property(x => x.Supplier)      .HasMaxLength(200);
            e.Property(x => x.CreatedByEmail).HasMaxLength(150);
            e.Property(x => x.IsActive)      .HasDefaultValue(true);
            e.HasIndex(x => x.ItemCode)      .IsUnique();
            e.HasIndex(x => x.Category);
        });

        // StoreRequisition
        mb.Entity<StoreRequisition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RequisitionNumber) .HasMaxLength(20) .IsRequired();
            e.Property(x => x.RequestedByEmail)  .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName)   .HasMaxLength(100).IsRequired();
            e.Property(x => x.Department)        .HasMaxLength(100).IsRequired();
            e.Property(x => x.Purpose)           .HasMaxLength(500).IsRequired();
            e.Property(x => x.LinkedReference)   .HasMaxLength(50);
            e.Property(x => x.Status)            .HasMaxLength(20) .IsRequired();
            e.Property(x => x.ApprovedByEmail)   .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)    .HasMaxLength(100);
            e.Property(x => x.RejectedByEmail)   .HasMaxLength(150);
            e.Property(x => x.RejectionReason)   .HasMaxLength(500);
            e.Property(x => x.IssuedByEmail)     .HasMaxLength(150);
            e.Property(x => x.IssuedByName)      .HasMaxLength(100);
            e.Property(x => x.Notes)             .HasMaxLength(1000);
            e.HasIndex(x => x.RequisitionNumber) .IsUnique();
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Items)
             .WithOne(i => i.Requisition)
             .HasForeignKey(i => i.RequisitionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // StoreRequisitionItem
        mb.Entity<StoreRequisitionItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemName)         .HasMaxLength(200).IsRequired();
            e.Property(x => x.Unit)             .HasMaxLength(30) .IsRequired();
            e.Property(x => x.UnitCostNaira)    .HasColumnType("decimal(18,2)");
            e.HasOne(x => x.StoreItem)
             .WithMany()
             .HasForeignKey(x => x.StoreItemId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // StoreMovement
        mb.Entity<StoreMovement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemCode)      .HasMaxLength(20) .IsRequired();
            e.Property(x => x.ItemName)      .HasMaxLength(200).IsRequired();
            e.Property(x => x.MovementType)  .HasMaxLength(20) .IsRequired();
            e.Property(x => x.Reference)     .HasMaxLength(50);
            e.Property(x => x.Notes)         .HasMaxLength(500);
            e.Property(x => x.MovedByEmail)  .HasMaxLength(150).IsRequired();
            e.Property(x => x.MovedByName)   .HasMaxLength(100).IsRequired();
            e.HasOne(x => x.StoreItem)
             .WithMany()
             .HasForeignKey(x => x.StoreItemId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.StoreItemId);
            e.HasIndex(x => x.CreatedAt);
        });

        // DieselRequisition
        mb.Entity<DieselRequisition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RequisitionNumber) .HasMaxLength(20) .IsRequired();
            e.Property(x => x.Purpose)           .HasMaxLength(500).IsRequired();
            e.Property(x => x.EquipmentType)     .HasMaxLength(30) .IsRequired();
            e.Property(x => x.EquipmentReference).HasMaxLength(100);
            e.Property(x => x.Location)          .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByEmail)  .HasMaxLength(150).IsRequired();
            e.Property(x => x.RequestedByName)   .HasMaxLength(100).IsRequired();
            e.Property(x => x.Department)        .HasMaxLength(100).IsRequired();
            e.Property(x => x.Status)            .HasMaxLength(20) .IsRequired();
            e.Property(x => x.ApprovedByEmail)   .HasMaxLength(150);
            e.Property(x => x.ApprovedByName)    .HasMaxLength(100);
            e.Property(x => x.RejectedByEmail)   .HasMaxLength(150);
            e.Property(x => x.RejectionReason)   .HasMaxLength(500);
            e.Property(x => x.DispensedByEmail)  .HasMaxLength(150);
            e.Property(x => x.DispensedByName)   .HasMaxLength(100);
            e.Property(x => x.UnitCostPerLitreNaira).HasColumnType("decimal(18,4)");
            e.Property(x => x.TotalCostNaira)    .HasColumnType("decimal(18,2)");
            e.Property(x => x.Notes)             .HasMaxLength(1000);
            e.HasIndex(x => x.RequisitionNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.RequestedByEmail);
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
