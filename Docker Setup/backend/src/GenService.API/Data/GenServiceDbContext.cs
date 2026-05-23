using GenService.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenService.API.Data;

public class GenServiceDbContext(DbContextOptions<GenServiceDbContext> options)
    : DbContext(options)
{
    public DbSet<ServiceRequest>      ServiceRequests      => Set<ServiceRequest>();
    public DbSet<StaffActivity>       StaffActivities      => Set<StaffActivity>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<GeneratorLog>        GeneratorLogs        => Set<GeneratorLog>();
    public DbSet<DieselRecord>        DieselRecords        => Set<DieselRecord>();

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
            e.Property(x => x.RejectionReason) .HasMaxLength(1000);
            e.Property(x => x.Notes)           .HasMaxLength(4000);
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
    }
}
