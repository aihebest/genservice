using GenService.API.Data;
using GenService.API.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<GenServiceDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// ── Authentication — JWT Bearer ───────────────────────────────────────────────
var jwtSecret   = builder.Configuration["Auth:DevJwt:Secret"]   ?? "GenServiceDevJwtSecret2026LocalKey32chars!";
var jwtIssuer   = builder.Configuration["Auth:DevJwt:Issuer"]   ?? "genservice-local";
var jwtAudience = builder.Configuration["Auth:DevJwt:Audience"] ?? "genservice-web";
var signingKey  = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = signingKey,
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ── Controllers & API Explorer ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── Swagger with JWT support ──────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "GenService API",
        Version     = "v1",
        Description = "General Service & Logistics Management Platform API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token (without 'Bearer' prefix)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Build app ─────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Database bootstrap (dev / demo) ─────────────────────────────────────────
if (!app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var db  = scope.ServiceProvider.GetRequiredService<GenServiceDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Schema
    try
    {
        var created = await db.Database.EnsureCreatedAsync();
        log.LogInformation(created
            ? "✅ Database created fresh — seeding demo data..."
            : "✅ Database already exists — checking seed guards...");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "❌ EnsureCreated failed — DB may be unavailable.");
        // continue; auth still works without DB
    }

    // ── Seed demo requests ───────────────────────────────────────────────────
    try
    {
        if (!await db.ServiceRequests.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.ServiceRequests.AddRange(
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0001", Title = "AC repair – Conference Room B",   Description = "The air conditioning unit in Conference Room B is making a loud noise and cooling poorly.",   Category = RequestCategory.Maintenance,       RequiresApproval = false, Status = RequestStatus.InProgress,      Priority = RequestPriority.High,   Location = "Conference Room B",   RequestedByEmail = "requester1@demo.local", RequestedByName = "Fatima Al-Hassan",  AssignedToEmail = "technician@demo.local", AssignedToName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-2) },
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0002", Title = "Weekend office access – Finance team",   Description = "Finance team needs access to the office on Saturday 25 May for month-end closing.",   Category = RequestCategory.WeekendAccess,    RequiresApproval = true,  Status = RequestStatus.PendingApproval, Priority = RequestPriority.Normal, Location = "Finance Floor – Block A", RequestedByEmail = "requester1@demo.local", RequestedByName = "Fatima Al-Hassan",  CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) },
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0003", Title = "Generator fuel top-up – HQ",   Description = "Diesel level at head office generator is below 30%. Requesting emergency top-up.",   Category = RequestCategory.Diesel,            RequiresApproval = true,  Status = RequestStatus.Approved,        Priority = RequestPriority.Urgent, Location = "HQ Generator Room",      RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo",     ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddHours(-4), CreatedAt = now.AddDays(-2), UpdatedAt = now.AddHours(-4) },
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0004", Title = "Faulty UPS – Server Room",   Description = "The UPS unit in the server room failed during last night's power fluctuation. Requires immediate replacement.",   Category = RequestCategory.FaultyAsset,       RequiresApproval = false, Status = RequestStatus.Open,            Priority = RequestPriority.Urgent, Location = "Server Room – IT Block",  RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu",    CreatedAt = now.AddHours(-5), UpdatedAt = now.AddHours(-5) },
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0005", Title = "Quarterly water tank washing – Block C",   Description = "Scheduled quarterly washing of overhead water storage tanks at Block C.",   Category = RequestCategory.Maintenance,       RequiresApproval = false, Status = RequestStatus.Open,            Priority = RequestPriority.Normal, Location = "Block C Rooftop",         RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo",     CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) },
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0006", Title = "Accommodation request – Abuja trip",   Description = "Staff member travelling to Abuja branch for 3 days. Requesting hotel accommodation.",   Category = RequestCategory.Accommodation,     RequiresApproval = true,  Status = RequestStatus.PendingApproval, Priority = RequestPriority.Normal, Location = "Abuja",                   RequestedByEmail = "requester2@demo.local", RequestedByName = "Tunde Babatunde",   CreatedAt = now.AddHours(-8), UpdatedAt = now.AddHours(-8) },
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0007", Title = "Broken window – HR Office",   Description = "Window pane in the HR office cracked after yesterday's storm. Security concern.",   Category = RequestCategory.FacilityComplaint, RequiresApproval = false, Status = RequestStatus.InProgress,      Priority = RequestPriority.High,   Location = "HR Office – 3rd Floor",  RequestedByEmail = "requester2@demo.local", RequestedByName = "Tunde Babatunde",   AssignedToEmail = "technician@demo.local", AssignedToName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-1) },
                new ServiceRequest { Id = Guid.NewGuid(), TicketNumber = "REQ-2026-0008", Title = "Store items – cleaning supplies",   Description = "Requesting cleaning supplies (mop, detergent, disinfectant) for weekly office clean.",   Category = RequestCategory.StoreItems,        RequiresApproval = true,  Status = RequestStatus.Completed,       Priority = RequestPriority.Low,    Location = "General Store",          RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu",    ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-5), CompletedAt = now.AddDays(-3), CreatedAt = now.AddDays(-7), UpdatedAt = now.AddDays(-3) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 8 demo service requests.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: ServiceRequests"); }

    // ── Seed demo staff activities ───────────────────────────────────────────
    try
    {
        if (!await db.StaffActivities.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.StaffActivities.AddRange(
                new GenService.API.Domain.StaffActivity { StaffEmail = "technician@demo.local",  StaffName = "Chukwudi Nwosu",  ActivityDescription = "Repairing faulty AC unit in Conference Room B",  Category = "Repair",        Location = "Conference Room B",      Status = "Active",    IsProxy = false, LoggedByEmail = "technician@demo.local", LoggedByName = "Chukwudi Nwosu", StartedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2) },
                new GenService.API.Domain.StaffActivity { StaffEmail = "tech2@demo.local",       StaffName = "Grace Obi",       ActivityDescription = "Routine inspection of plumbing fixtures on 2nd floor",  Category = "Inspection",    Location = "2nd Floor – Admin Wing",  Status = "Active",    IsProxy = false, LoggedByEmail = "tech2@demo.local",      LoggedByName = "Grace Obi",      StartedAt = now.AddHours(-1), UpdatedAt = now.AddHours(-1) },
                new GenService.API.Domain.StaffActivity { StaffEmail = "driver@demo.local",      StaffName = "Bola Adeyemi",    ActivityDescription = "Vehicle delivery run to Victoria Island branch",         Category = "Delivery",      Location = "Victoria Island, Lagos",  Status = "Active",    IsProxy = true,  LoggedByEmail = "supervisor@demo.local", LoggedByName = "Emeka Okonkwo",  StartedAt = now.AddHours(-3), UpdatedAt = now.AddHours(-3) },
                new GenService.API.Domain.StaffActivity { StaffEmail = "driver2@demo.local",     StaffName = "Kwame Asante",    ActivityDescription = "Generator preventive maintenance – HQ",                  Category = "Maintenance",   Location = "HQ Generator Room",       Status = "Completed", IsProxy = false, LoggedByEmail = "driver2@demo.local",    LoggedByName = "Kwame Asante",   StartedAt = now.AddDays(-1), CompletedAt = now.AddHours(-4), UpdatedAt = now.AddHours(-4) },
                new GenService.API.Domain.StaffActivity { StaffEmail = "technician@demo.local",  StaffName = "Chukwudi Nwosu",  ActivityDescription = "Electrical wiring inspection – Server Room",            Category = "Electrical",    Location = "Server Room – IT Block",  Status = "Completed", IsProxy = false, LoggedByEmail = "technician@demo.local", LoggedByName = "Chukwudi Nwosu", StartedAt = now.AddDays(-2), CompletedAt = now.AddDays(-2).AddHours(3), UpdatedAt = now.AddDays(-2).AddHours(3) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 5 demo staff activities.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: StaffActivities"); }

    // ── Seed demo maintenance schedules ─────────────────────────────────────
    try
    {
        if (!await db.MaintenanceSchedules.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.MaintenanceSchedules.AddRange(
                new GenService.API.Domain.MaintenanceSchedule { TaskName = "Quarterly Fumigation – All Buildings",        Description = "Pest control fumigation of all office buildings including storage areas.", Category = "Fumigation",        Location = "All Buildings",          FrequencyLabel = "Quarterly", FrequencyDays = 90,  NextDueAt = now.AddDays(-5),  LastCompletedAt = now.AddDays(-95),  IsActive = true, AssignedToEmail = "supervisor@demo.local", AssignedToName = "Emeka Okonkwo",  CreatedAt = now.AddDays(-200), UpdatedAt = now.AddDays(-95) },
                new GenService.API.Domain.MaintenanceSchedule { TaskName = "Water Tank Washing – Block A & B",            Description = "Quarterly washing and disinfection of overhead water storage tanks.", Category = "TankWashing",       Location = "Block A & B Rooftop",    FrequencyLabel = "Quarterly", FrequencyDays = 90,  NextDueAt = now.AddDays(10),  LastCompletedAt = now.AddDays(-80),  IsActive = true, AssignedToEmail = "tech2@demo.local",      AssignedToName = "Grace Obi",      CreatedAt = now.AddDays(-180), UpdatedAt = now.AddDays(-80) },
                new GenService.API.Domain.MaintenanceSchedule { TaskName = "Generator Servicing – HQ",                   Description = "Full service of the main HQ generator including oil change, filter replacement, and load test.", Category = "GeneratorService", Location = "HQ Generator Room",  FrequencyLabel = "Monthly",   FrequencyDays = 30,  NextDueAt = now.AddDays(3),   LastCompletedAt = now.AddDays(-27),  IsActive = true, AssignedToEmail = "technician@demo.local", AssignedToName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-90),  UpdatedAt = now.AddDays(-27) },
                new GenService.API.Domain.MaintenanceSchedule { TaskName = "Waste Disposal – All Sites",                 Description = "Weekly collection and disposal of office waste including recycling.", Category = "WasteDisposal",     Location = "All Sites",              FrequencyLabel = "Weekly",    FrequencyDays = 7,   NextDueAt = now.AddDays(2),   LastCompletedAt = now.AddDays(-5),   IsActive = true, AssignedToEmail = "tech2@demo.local",      AssignedToName = "Grace Obi",      CreatedAt = now.AddDays(-60),  UpdatedAt = now.AddDays(-5) },
                new GenService.API.Domain.MaintenanceSchedule { TaskName = "HVAC Filter Replacement – Office Floors",    Description = "Replace air filters on all HVAC units across all office floors.", Category = "HVAC",              Location = "All Office Floors",       FrequencyLabel = "Monthly",   FrequencyDays = 30,  NextDueAt = now.AddDays(15),  LastCompletedAt = now.AddDays(-15),  IsActive = true, AssignedToEmail = "technician@demo.local", AssignedToName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-120), UpdatedAt = now.AddDays(-15) },
                new GenService.API.Domain.MaintenanceSchedule { TaskName = "Fire Safety Equipment Inspection",           Description = "Monthly inspection and testing of fire extinguishers, smoke detectors, and emergency exits.", Category = "FireSafety",        Location = "All Buildings",          FrequencyLabel = "Monthly",   FrequencyDays = 30,  NextDueAt = now.AddDays(-2),  LastCompletedAt = now.AddDays(-32),  IsActive = true, AssignedToEmail = "supervisor@demo.local", AssignedToName = "Emeka Okonkwo",  CreatedAt = now.AddDays(-150), UpdatedAt = now.AddDays(-32) },
                new GenService.API.Domain.MaintenanceSchedule { TaskName = "Plumbing Inspection – All Floors",           Description = "Quarterly inspection of all plumbing, drainage, and water systems.", Category = "Plumbing",          Location = "All Floors",              FrequencyLabel = "Quarterly", FrequencyDays = 90,  NextDueAt = now.AddDays(45),  LastCompletedAt = now.AddDays(-45),  IsActive = true, AssignedToEmail = "tech2@demo.local",      AssignedToName = "Grace Obi",      CreatedAt = now.AddDays(-130), UpdatedAt = now.AddDays(-45) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 7 demo maintenance schedules.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: MaintenanceSchedules"); }

    // ── Seed demo generator logs ─────────────────────────────────────────────
    try
    {
        if (!await db.GeneratorLogs.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.GeneratorLogs.AddRange(
                // Completed outage sessions
                new GenService.API.Domain.GeneratorLog { Location = "HQ Generator Room",    StartTime = now.AddDays(-25), EndTime = now.AddDays(-25).AddHours(4.5),  RuntimeHours = 4.5,  FuelLevelBefore = 180, FuelLevelAfter = 135, FuelConsumed = 45,  RunReason = "PowerOutage",   Status = "Stopped", OutageCause = "EKEDC grid failure",         LoggedByEmail = "supervisor@demo.local",  LoggedByName = "Emeka Okonkwo",  CreatedAt = now.AddDays(-25), UpdatedAt = now.AddDays(-25) },
                new GenService.API.Domain.GeneratorLog { Location = "Block B Generator",    StartTime = now.AddDays(-20), EndTime = now.AddDays(-20).AddHours(2.0),  RuntimeHours = 2.0,  FuelLevelBefore = 120, FuelLevelAfter = 100, FuelConsumed = 20,  RunReason = "LoadShedding",  Status = "Stopped", OutageCause = "Scheduled load shedding",    LoggedByEmail = "technician@demo.local",  LoggedByName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-20) },
                new GenService.API.Domain.GeneratorLog { Location = "HQ Generator Room",    StartTime = now.AddDays(-15), EndTime = now.AddDays(-15).AddHours(6.0),  RuntimeHours = 6.0,  FuelLevelBefore = 135, FuelLevelAfter = 63,  FuelConsumed = 72,  RunReason = "PowerOutage",   Status = "Stopped", OutageCause = "Transformer fault at substation", LoggedByEmail = "supervisor@demo.local", LoggedByName = "Emeka Okonkwo", CreatedAt = now.AddDays(-15), UpdatedAt = now.AddDays(-15) },
                new GenService.API.Domain.GeneratorLog { Location = "HQ Generator Room",    StartTime = now.AddDays(-10), EndTime = now.AddDays(-10).AddHours(1.5),  RuntimeHours = 1.5,  FuelLevelBefore = 200, FuelLevelAfter = 182, FuelConsumed = 18,  RunReason = "ScheduledTest", Status = "Stopped", OutageCause = null,                          LoggedByEmail = "technician@demo.local",  LoggedByName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-10) },
                new GenService.API.Domain.GeneratorLog { Location = "Block B Generator",    StartTime = now.AddDays(-8),  EndTime = now.AddDays(-8).AddHours(3.0),   RuntimeHours = 3.0,  FuelLevelBefore = 100, FuelLevelAfter = 64,  FuelConsumed = 36,  RunReason = "PowerOutage",   Status = "Stopped", OutageCause = "EKEDC maintenance outage",    LoggedByEmail = "tech2@demo.local",       LoggedByName = "Grace Obi",      CreatedAt = now.AddDays(-8),  UpdatedAt = now.AddDays(-8) },
                new GenService.API.Domain.GeneratorLog { Location = "HQ Generator Room",    StartTime = now.AddDays(-3),  EndTime = now.AddDays(-3).AddHours(5.25), RuntimeHours = 5.25, FuelLevelBefore = 182, FuelLevelAfter = 119, FuelConsumed = 63,  RunReason = "PowerOutage",   Status = "Stopped", OutageCause = "EKEDC grid failure",          LoggedByEmail = "supervisor@demo.local",  LoggedByName = "Emeka Okonkwo",  CreatedAt = now.AddDays(-3),  UpdatedAt = now.AddDays(-3) },
                // Currently running
                new GenService.API.Domain.GeneratorLog { Location = "HQ Generator Room",    StartTime = now.AddHours(-2), EndTime = null,                            RuntimeHours = null, FuelLevelBefore = 119, FuelLevelAfter = null, FuelConsumed = null, RunReason = "PowerOutage",  Status = "Running", OutageCause = "EKEDC load shedding – Zone 3", LoggedByEmail = "supervisor@demo.local", LoggedByName = "Emeka Okonkwo", CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 7 demo generator logs.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: GeneratorLogs"); }

    // ── Seed demo diesel records ─────────────────────────────────────────────
    try
    {
        if (!await db.DieselRecords.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.DieselRecords.AddRange(
                // Purchases
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-28), RecordType = "Purchase",  QuantityLitres = 500, UnitCostNaira = 1200, TotalCostNaira = 600000, Supplier = "Total Energies – Ilupeju",  Destination = "HQ Diesel Tank",    RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-28), CreatedAt = now.AddDays(-28), UpdatedAt = now.AddDays(-28) },
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-21), RecordType = "Purchase",  QuantityLitres = 300, UnitCostNaira = 1220, TotalCostNaira = 366000, Supplier = "Ardova Petroleum – Ikeja",  Destination = "HQ Diesel Tank",    RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-21), CreatedAt = now.AddDays(-21), UpdatedAt = now.AddDays(-21) },
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-7),  RecordType = "Purchase",  QuantityLitres = 400, UnitCostNaira = 1250, TotalCostNaira = 500000, Supplier = "Total Energies – Ilupeju",  Destination = "HQ Diesel Tank",    RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-7),  CreatedAt = now.AddDays(-7),  UpdatedAt = now.AddDays(-7) },
                // Dispensed
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-25), RecordType = "Dispensed", QuantityLitres = 100, UnitCostNaira = 0,    TotalCostNaira = 0,      Supplier = null,                        Destination = "HQ Generator Room", RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu", ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-25), CreatedAt = now.AddDays(-25), UpdatedAt = now.AddDays(-25) },
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-15), RecordType = "Dispensed", QuantityLitres = 150, UnitCostNaira = 0,    TotalCostNaira = 0,      Supplier = null,                        Destination = "HQ Generator Room", RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu", ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-15), CreatedAt = now.AddDays(-15), UpdatedAt = now.AddDays(-15) },
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-8),  RecordType = "Dispensed", QuantityLitres = 80,  UnitCostNaira = 0,    TotalCostNaira = 0,      Supplier = null,                        Destination = "Block B Generator", RequestedByEmail = "tech2@demo.local",      RequestedByName = "Grace Obi",      ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-8),  CreatedAt = now.AddDays(-8),  UpdatedAt = now.AddDays(-8) },
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-3),  RecordType = "Dispensed", QuantityLitres = 120, UnitCostNaira = 0,    TotalCostNaira = 0,      Supplier = null,                        Destination = "HQ Generator Room", RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu", ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-3),  CreatedAt = now.AddDays(-3),  UpdatedAt = now.AddDays(-3) },
                // Vehicle fills
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-14), RecordType = "Dispensed", QuantityLitres = 60,  UnitCostNaira = 0,    TotalCostNaira = 0,      Supplier = null,                        Destination = "Company Vehicle – LAG-342-TE", RequestedByEmail = "driver@demo.local", RequestedByName = "Bola Adeyemi", ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-14), CreatedAt = now.AddDays(-14), UpdatedAt = now.AddDays(-14) },
                new GenService.API.Domain.DieselRecord { RecordDate = now.AddDays(-5),  RecordType = "Dispensed", QuantityLitres = 55,  UnitCostNaira = 0,    TotalCostNaira = 0,      Supplier = null,                        Destination = "Company Vehicle – LAG-098-BT", RequestedByEmail = "driver2@demo.local", RequestedByName = "Kwame Asante", ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-5),  CreatedAt = now.AddDays(-5),  UpdatedAt = now.AddDays(-5) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 9 demo diesel records.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: DieselRecords"); }

    log.LogInformation("🚀 Database bootstrap complete.");
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseCors("AppPolicy");           // CORS must be before auth

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GenService API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
});

// NOTE: No UseHttpsRedirection — HTTPS is handled at Azure Front Door level.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/api/v1/ping", () => new
{
    status    = "GenService API is running",
    version   = "1.0",
    authMode  = app.Configuration["Auth:Mode"] ?? "DevJwt",
    timestamp = DateTime.UtcNow
}).WithName("Ping").WithOpenApi();

app.Run();
