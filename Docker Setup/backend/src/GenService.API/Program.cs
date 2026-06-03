using GenService.API.Data;
using GenService.API.Domain;
using GenService.API.Services;
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

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AuditService>();

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

    // ── Seed vehicle maintenance requests ────────────────────────────────────
    try
    {
        if (!await db.VehicleMaintenanceRequests.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.VehicleMaintenanceRequests.AddRange(
                new GenService.API.Domain.VehicleMaintenanceRequest { RequestNumber = "V/26/001", VehicleRegNo = "LAG-342-TE", VehicleType = "Toyota Hilux", MaintenanceType = "Repair",      Description = "Engine oil leak detected during routine check. Oil dripping from the undercarriage.",      Priority = "High",   Status = "InWorkshop", CurrentLocation = "Lagos Office",         ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-10), WorkshopName = "AutoFix Garage", WorkshopLocation = "Ilupeju, Lagos", SentToWorkshopAt = now.AddDays(-9),  RequestedByEmail = "driver@demo.local", RequestedByName = "Bola Adeyemi", CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-9) },
                new GenService.API.Domain.VehicleMaintenanceRequest { RequestNumber = "V/26/002", VehicleRegNo = "RVS-098-BT", VehicleType = "Toyota Coaster Bus", MaintenanceType = "Servicing",  Description = "Routine 10,000km service — oil change, filter replacement, brake inspection.",            Priority = "Normal", Status = "InWorkshop", CurrentLocation = "Port Harcourt Office", ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-3),  WorkshopName = "PH Motors Workshop", WorkshopLocation = "GRA, Port Harcourt", SentToWorkshopAt = now.AddDays(-2),  RequestedByEmail = "driver2@demo.local", RequestedByName = "Kwame Asante",  CreatedAt = now.AddDays(-3),  UpdatedAt = now.AddDays(-2) },
                new GenService.API.Domain.VehicleMaintenanceRequest { RequestNumber = "V/26/003", VehicleRegNo = "ABJ-211-FC", VehicleType = "Ford Ranger", MaintenanceType = "TyreChange",    Description = "Two front tyres have worn treads and need immediate replacement before next field trip.",   Priority = "Urgent", Status = "Approved",   CurrentLocation = "Abuja Office",         ApprovedByEmail = "manager@demo.local",    ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddHours(-6),                                                                                                                          RequestedByEmail = "driver@demo.local", RequestedByName = "Bola Adeyemi", CreatedAt = now.AddDays(-1),  UpdatedAt = now.AddHours(-6) },
                new GenService.API.Domain.VehicleMaintenanceRequest { RequestNumber = "V/26/004", VehicleRegNo = "LAG-502-KE", VehicleType = "Honda CR-V",    MaintenanceType = "Inspection",    Description = "Pre-travel vehicle inspection required before Lagos to Bonny trip scheduled next week.",   Priority = "Normal", Status = "Pending",    CurrentLocation = "Lagos Office",                                                                                                                                                                                                                       RequestedByEmail = "driver2@demo.local", RequestedByName = "Kwame Asante",  CreatedAt = now.AddHours(-4),  UpdatedAt = now.AddHours(-4) },
                new GenService.API.Domain.VehicleMaintenanceRequest { RequestNumber = "V/26/005", VehicleRegNo = "LAG-342-TE", VehicleType = "Toyota Hilux", MaintenanceType = "Bodywork",      Description = "Minor dent and paint damage on the front bumper after a parking lot incident.",            Priority = "Low",    Status = "Completed",  CurrentLocation = "Lagos Office",         ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-22), WorkshopName = "Classic Body Works",  WorkshopLocation = "Ikeja, Lagos",       SentToWorkshopAt = now.AddDays(-21), RequestedByEmail = "driver@demo.local", RequestedByName = "Bola Adeyemi", CompletedAt = now.AddDays(-15), CreatedAt = now.AddDays(-25), UpdatedAt = now.AddDays(-15) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 5 demo vehicle maintenance requests.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: VehicleMaintenanceRequests"); }

    // ── Seed equipment maintenance requests ──────────────────────────────────
    try
    {
        if (!await db.EquipmentMaintenanceRequests.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.EquipmentMaintenanceRequests.AddRange(
                new GenService.API.Domain.EquipmentMaintenanceRequest { RequestNumber = "E/26/001", AssetNo = "6660003188", AssetDescription = "DR CUMMINS 275KVA GENERATOR",         MaintenanceType = "GeneratorService", EndUser = "DR",         Location = "DR",                  Description = "This generator is due for servicing",                           Priority = "Normal", Status = "Completed", RunningHours = 13602, NextServiceHour = 13852, WorkDone = "Water separator filter, fuel filter, oil filter and 35L Total engine oil 15W-40 replaced", ActionedBy = "Third Party", RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo",  ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-25), CompletedAt = now.AddDays(-24), CreatedAt = now.AddDays(-26), UpdatedAt = now.AddDays(-24) },
                new GenService.API.Domain.EquipmentMaintenanceRequest { RequestNumber = "E/26/002", AssetNo = "6660002135", AssetDescription = "PHC OFFICE CAT 350KVA GENERATOR",    MaintenanceType = "GeneratorService", EndUser = "PHC Office", Location = "Port Harcourt Office", Description = "The generator shut down unexpectedly",                           Priority = "High",   Status = "Completed", RunningHours = 16064, NextServiceHour = 16201, WorkDone = "Fuel filter CAT IR-0749 and water separator filter DAHL151 replaced. Radiator flushed.", ActionedBy = "Third Party", RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo",  ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-18), CompletedAt = now.AddDays(-17), CreatedAt = now.AddDays(-19), UpdatedAt = now.AddDays(-17) },
                new GenService.API.Domain.EquipmentMaintenanceRequest { RequestNumber = "E/26/003", AssetNo = "MDYO100",    AssetDescription = "AK UYO PERKINS 100KVA GENERATOR",    MaintenanceType = "GeneratorService", EndUser = "AK Uyo",    Location = "Uyo",                 Description = "Servicing of 100kva generator – filters, oil and radiator",      Priority = "Normal", Status = "Ongoing",   RunningHours = 3206,  NextServiceHour = null,  RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-5), CreatedAt = now.AddDays(-6), UpdatedAt = now.AddDays(-5) },
                new GenService.API.Domain.EquipmentMaintenanceRequest { RequestNumber = "E/26/004", AssetNo = "OFFICE-AC1", AssetDescription = "CONFERENCE ROOM B – DAIKIN 3HP AC",   MaintenanceType = "ACService",        EndUser = "Lagos Office", Location = "Lagos Office",       Description = "Air conditioning unit not cooling properly. Gas may need recharging.", Priority = "High", Status = "Pending", RequestedByEmail = "requester1@demo.local", RequestedByName = "Fatima Al-Hassan", CreatedAt = now.AddHours(-6), UpdatedAt = now.AddHours(-6) },
                new GenService.API.Domain.EquipmentMaintenanceRequest { RequestNumber = "E/26/005", AssetNo = "UPS-SVR-01", AssetDescription = "SERVER ROOM APC 20KVA UPS",          MaintenanceType = "UPSMaintenance",   EndUser = "Lagos Office", Location = "Lagos Office",       Description = "UPS battery backup time has dropped significantly. Battery replacement required.", Priority = "Urgent", Status = "AwaitingSpares", RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-3), CreatedAt = now.AddDays(-4), UpdatedAt = now.AddDays(-3) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 5 demo equipment maintenance requests.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: EquipmentMaintenanceRequests"); }

    // ── Seed facility maintenance requests ───────────────────────────────────
    try
    {
        if (!await db.FacilityMaintenanceRequests.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.FacilityMaintenanceRequests.AddRange(
                new GenService.API.Domain.FacilityMaintenanceRequest { RequestNumber = "F/26/001", MaintenanceType = "Electrical",  Description = "Faulty hand dryers in all three floors of PHC Office need replacement – 8 units", Location = "Port Harcourt Office", EndUser = "PHC Office", RoomFlat = "All Floors – Conveniences",   Priority = "Normal", Status = "Completed", WorkDone = "8 hand dryer units replaced across ground, first and second floors", ActionedBy = "John Ojie", RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-20), CompletedAt = now.AddDays(-19), CreatedAt = now.AddDays(-21), UpdatedAt = now.AddDays(-19) },
                new GenService.API.Domain.FacilityMaintenanceRequest { RequestNumber = "F/26/002", MaintenanceType = "Plumbing",    Description = "Washing of all water storage tanks at Port Harcourt main office",                  Location = "Port Harcourt Office", EndUser = "PHC Office", RoomFlat = "Main Office Water Tanks",     Priority = "Normal", Status = "Completed", WorkDone = "All water tanks washed and disinfected",                             ActionedBy = "Woji Store",  RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-14), CompletedAt = now.AddDays(-13), CreatedAt = now.AddDays(-15), UpdatedAt = now.AddDays(-13) },
                new GenService.API.Domain.FacilityMaintenanceRequest { RequestNumber = "F/26/003", MaintenanceType = "TankWashing", Description = "Washing of DR GeePee overhead tank",                                               Location = "DR",                   EndUser = "DR",         RoomFlat = "Overhead Tank",               Priority = "Normal", Status = "Pending",   RequestedByEmail = "technician@demo.local", RequestedByName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-8), UpdatedAt = now.AddDays(-8) },
                new GenService.API.Domain.FacilityMaintenanceRequest { RequestNumber = "F/26/004", MaintenanceType = "CivilWorks",  Description = "Window pane in the HR office cracked after storm. Security risk – urgent replacement", Location = "Lagos Office",         EndUser = "Lagos Office", RoomFlat = "HR Office – 3rd Floor",     Priority = "High",   Status = "Ongoing",   WorkDone = null, ActionedBy = null, RequestedByEmail = "requester2@demo.local", RequestedByName = "Tunde Babatunde", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-2), CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-2) },
                new GenService.API.Domain.FacilityMaintenanceRequest { RequestNumber = "F/26/005", MaintenanceType = "Painting",    Description = "Repainting of the external walls of Block A & B at DR site",                        Location = "DR",                   EndUser = "DR",         RoomFlat = "Block A & B External Walls", Priority = "Low",    Status = "AwaitingFunds", RequestedByEmail = "supervisor@demo.local", RequestedByName = "Emeka Okonkwo", ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddDays(-10), CreatedAt = now.AddDays(-12), UpdatedAt = now.AddDays(-10) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 5 demo facility maintenance requests.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: FacilityMaintenanceRequests"); }

    // ── Seed task progress logs ──────────────────────────────────────────────
    try
    {
        if (!await db.TaskProgressLogs.AnyAsync())
        {
            // Get actual request IDs from the seeded service requests
            var req1 = await db.ServiceRequests.Where(r => r.TicketNumber == "REQ-2026-0001").FirstOrDefaultAsync();
            var req7 = await db.ServiceRequests.Where(r => r.TicketNumber == "REQ-2026-0007").FirstOrDefaultAsync();
            var eq1  = await db.EquipmentMaintenanceRequests.Where(r => r.RequestNumber == "E/26/003").FirstOrDefaultAsync();
            var vm1  = await db.VehicleMaintenanceRequests.Where(r => r.RequestNumber == "V/26/001").FirstOrDefaultAsync();
            var now  = DateTime.UtcNow;

            var logs = new List<GenService.API.Domain.TaskProgressLog>();

            if (req1 != null)
            {
                logs.Add(new GenService.API.Domain.TaskProgressLog { Module = "Requests", EntityId = req1.Id.ToString(), RefNumber = req1.TicketNumber, TaskTitle = req1.Title, LogDate = now.AddDays(-2).Date, ActivityPerformed = "Inspected the AC unit in Conference Room B. Found that the compressor is making an unusual noise. Cleaned the air filters.", ProgressStatus = "WorkInProgress", MaterialsRequired = null, NextAction = "Check refrigerant levels and test compressor performance tomorrow.", LoggedByEmail = "technician@demo.local", LoggedByName = "Chukwudi Nwosu", IsProxy = false, CreatedAt = now.AddDays(-2) });
                logs.Add(new GenService.API.Domain.TaskProgressLog { Module = "Requests", EntityId = req1.Id.ToString(), RefNumber = req1.TicketNumber, TaskTitle = req1.Title, LogDate = now.AddDays(-1).Date, ActivityPerformed = "Tested refrigerant levels — found gas is low. Identified that the compressor gasket is damaged and needs replacement.", ProgressStatus = "AwaitingMaterials", MaterialsRequired = "1 x Compressor gasket (Model: DAI-COMP-B), 500g R22 refrigerant gas", NextAction = "Raise material request to store. Will complete repair once parts arrive.", LoggedByEmail = "technician@demo.local", LoggedByName = "Chukwudi Nwosu", IsProxy = false, CreatedAt = now.AddDays(-1) });
            }

            if (req7 != null)
            {
                logs.Add(new GenService.API.Domain.TaskProgressLog { Module = "Requests", EntityId = req7.Id.ToString(), RefNumber = req7.TicketNumber, TaskTitle = req7.Title, LogDate = now.AddDays(-1).Date, ActivityPerformed = "Visited HR Office. Assessed window damage — two panes completely shattered, frame is intact. Temporary board-up done for security.", ProgressStatus = "AwaitingVendor", MaterialsRequired = "2 x Tempered glass panes (60cm x 80cm)", NextAction = "Contacted glass vendor for supply and installation. Awaiting quotation.", LoggedByEmail = "supervisor@demo.local", LoggedByName = "Emeka Okonkwo", IsProxy = true, ProxyForName = "Chukwudi Nwosu", CreatedAt = now.AddDays(-1) });
            }

            if (eq1 != null)
            {
                logs.Add(new GenService.API.Domain.TaskProgressLog { Module = "Equipment", EntityId = eq1.Id.ToString(), RefNumber = eq1.RequestNumber, TaskTitle = eq1.AssetDescription, LogDate = now.AddDays(-3).Date, ActivityPerformed = "Sourced filters from third-party supplier. Replaced fuel filter and oil filter. Refilled engine oil (35L Total 15W-40).", ProgressStatus = "WorkInProgress", MaterialsRequired = null, NextAction = "Run generator for 30 minutes and check for leaks. Update running hours.", LoggedByEmail = "technician@demo.local", LoggedByName = "Chukwudi Nwosu", IsProxy = false, CreatedAt = now.AddDays(-3) });
            }

            if (vm1 != null)
            {
                logs.Add(new GenService.API.Domain.TaskProgressLog { Module = "Vehicle", EntityId = vm1.Id.ToString(), RefNumber = vm1.RequestNumber, TaskTitle = $"{vm1.VehicleRegNo} – {vm1.VehicleType}", LogDate = now.AddDays(-5).Date, ActivityPerformed = "Vehicle delivered to AutoFix Garage. Engineer diagnosed oil leak from crankshaft seal. Parts have been ordered.", ProgressStatus = "AwaitingMaterials", MaterialsRequired = "1 x Crankshaft rear oil seal (Toyota Hilux 2.5D4D)", NextAction = "Parts expected in 3 days. Workshop will proceed immediately on arrival.", LoggedByEmail = "supervisor@demo.local", LoggedByName = "Emeka Okonkwo", IsProxy = false, CreatedAt = now.AddDays(-5) });
                logs.Add(new GenService.API.Domain.TaskProgressLog { Module = "Vehicle", EntityId = vm1.Id.ToString(), RefNumber = vm1.RequestNumber, TaskTitle = $"{vm1.VehicleRegNo} – {vm1.VehicleType}", LogDate = now.AddDays(-9).Date, ActivityPerformed = "Initial assessment done. Vehicle inspected at Lagos Office compound before dispatch to workshop.", ProgressStatus = "WorkInProgress", MaterialsRequired = null, NextAction = "Arrange transportation of vehicle to AutoFix Garage tomorrow.", LoggedByEmail = "driver@demo.local", LoggedByName = "Bola Adeyemi", IsProxy = false, CreatedAt = now.AddDays(-9) });
            }

            if (logs.Any())
            {
                db.TaskProgressLogs.AddRange(logs);
                await db.SaveChangesAsync();
                log.LogInformation("✅ Seeded {Count} demo task progress logs.", logs.Count);
            }
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: TaskProgressLogs"); }

    // ── Seed generator daily readings ────────────────────────────────────────
    try
    {
        if (!await db.GeneratorDailyReadings.AnyAsync())
        {
            var now = DateTime.UtcNow.Date;
            db.GeneratorDailyReadings.AddRange(
                // PHC Main Office CAT 350KVA — approaching service (alert active)
                new GenService.API.Domain.GeneratorDailyReading { AssetNo="6660002135", AssetDescription="PHC OFFICE CAT 350KVA GENERATOR",  Location="Port Harcourt Office", ReadingDate=now,             CumulativeRunHours=16245, RunHoursToday=6.5,  GeneratorStatus="Running",  FuelLevelLitres=380, FuelConsumedLitres=65,  UtilityAvailableHours=8,  ServiceIntervalHours=250, LastServicedAtHours=16010, ServiceAlertActive=true,  LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now },
                new GenService.API.Domain.GeneratorDailyReading { AssetNo="6660002135", AssetDescription="PHC OFFICE CAT 350KVA GENERATOR",  Location="Port Harcourt Office", ReadingDate=now.AddDays(-1), CumulativeRunHours=16238, RunHoursToday=5.0,  GeneratorStatus="Running",  FuelLevelLitres=445, FuelConsumedLitres=55,  UtilityAvailableHours=10, ServiceIntervalHours=250, LastServicedAtHours=16010, ServiceAlertActive=false, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now.AddDays(-1) },
                new GenService.API.Domain.GeneratorDailyReading { AssetNo="6660002135", AssetDescription="PHC OFFICE CAT 350KVA GENERATOR",  Location="Port Harcourt Office", ReadingDate=now.AddDays(-2), CumulativeRunHours=16233, RunHoursToday=4.5,  GeneratorStatus="Standby",  FuelLevelLitres=500, FuelConsumedLitres=null, UtilityAvailableHours=18, ServiceIntervalHours=250, LastServicedAtHours=16010, ServiceAlertActive=false, LoggedByEmail="technician@demo.local", LoggedByName="Chukwudi Nwosu", CreatedAt=now.AddDays(-2) },
                // DR Cummins 275KVA — normal
                new GenService.API.Domain.GeneratorDailyReading { AssetNo="6660003188", AssetDescription="DR CUMMINS 275KVA GENERATOR",      Location="DR",                   ReadingDate=now,             CumulativeRunHours=13750, RunHoursToday=8.0,  GeneratorStatus="Running",  FuelLevelLitres=220, FuelConsumedLitres=80,  UtilityAvailableHours=4,  ServiceIntervalHours=250, LastServicedAtHours=13602, ServiceAlertActive=false, LoggedByEmail="technician@demo.local", LoggedByName="Chukwudi Nwosu", CreatedAt=now },
                new GenService.API.Domain.GeneratorDailyReading { AssetNo="6660003188", AssetDescription="DR CUMMINS 275KVA GENERATOR",      Location="DR",                   ReadingDate=now.AddDays(-1), CumulativeRunHours=13742, RunHoursToday=7.5,  GeneratorStatus="Running",  FuelLevelLitres=300, FuelConsumedLitres=75,  UtilityAvailableHours=6,  ServiceIntervalHours=250, LastServicedAtHours=13602, ServiceAlertActive=false, LoggedByEmail="technician@demo.local", LoggedByName="Chukwudi Nwosu", CreatedAt=now.AddDays(-1) },
                // Woji FG Wilson 40KVA — low fuel warning
                new GenService.API.Domain.GeneratorDailyReading { AssetNo="6660000017", AssetDescription="WOJI YARD FG WILSON 40KVA GENERATOR",Location="Woji",              ReadingDate=now,             CumulativeRunHours=10835, RunHoursToday=3.5,  GeneratorStatus="Standby",  FuelLevelLitres=45,  FuelConsumedLitres=35,  UtilityAvailableHours=16, ServiceIntervalHours=250, LastServicedAtHours=10780, ServiceAlertActive=false, LoggedByEmail="driver@demo.local", LoggedByName="Bola Adeyemi", CreatedAt=now },
                // Lagos Office Cummins 135KVA
                new GenService.API.Domain.GeneratorDailyReading { AssetNo="6660002108", AssetDescription="LAGOS OFFICE CUMMINS 135KVA GENERATOR",Location="Lagos Office",    ReadingDate=now,             CumulativeRunHours=8920,  RunHoursToday=4.0,  GeneratorStatus="Standby",  FuelLevelLitres=180, FuelConsumedLitres=40,  UtilityAvailableHours=14, ServiceIntervalHours=250, LastServicedAtHours=8750,  ServiceAlertActive=false, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 7 demo generator daily readings.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: GeneratorDailyReadings"); }

    // ── Seed power meter readings ─────────────────────────────────────────────
    try
    {
        if (!await db.PowerMeterReadings.AnyAsync())
        {
            var now = DateTime.UtcNow.Date;
            db.PowerMeterReadings.AddRange(
                new GenService.API.Domain.PowerMeterReading { Location="Port Harcourt Office", MeterNumber="NPA-PHC-001", ReadingDate=now,             MeterReadingKwh=124580, UnitsConsumedToday=320, UtilityAvailableHours=8,  LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now },
                new GenService.API.Domain.PowerMeterReading { Location="Port Harcourt Office", MeterNumber="NPA-PHC-001", ReadingDate=now.AddDays(-1), MeterReadingKwh=124260, UnitsConsumedToday=290, UtilityAvailableHours=10, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now.AddDays(-1) },
                new GenService.API.Domain.PowerMeterReading { Location="Port Harcourt Office", MeterNumber="NPA-PHC-001", ReadingDate=now.AddDays(-2), MeterReadingKwh=123970, UnitsConsumedToday=350, UtilityAvailableHours=6,  LoggedByEmail="technician@demo.local", LoggedByName="Chukwudi Nwosu", CreatedAt=now.AddDays(-2) },
                new GenService.API.Domain.PowerMeterReading { Location="Lagos Office",         MeterNumber="NPA-LAG-001", ReadingDate=now,             MeterReadingKwh=87420,  UnitsConsumedToday=210, UtilityAvailableHours=14, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now },
                new GenService.API.Domain.PowerMeterReading { Location="Lagos Office",         MeterNumber="NPA-LAG-001", ReadingDate=now.AddDays(-1), MeterReadingKwh=87210,  UnitsConsumedToday=195, UtilityAvailableHours=16, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now.AddDays(-1) }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 5 demo power meter readings.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: PowerMeterReadings"); }

    // ── Seed diesel tank readings ────────────────────────────────────────────
    try
    {
        if (!await db.DieselTankReadings.AnyAsync())
        {
            var now = DateTime.UtcNow.Date;
            db.DieselTankReadings.AddRange(
                // PHC Main Office — Main Generator Tank (3-day history)
                new GenService.API.Domain.DieselTankReading { Location="Port Harcourt Office", TankIdentifier="Main Generator Tank", ReadingDate=now.AddDays(-2), TankLevelLitres=5000, PreviousLevelLitres=null,  ConsumptionLitres=null, CostPerLitreNaira=1250, TotalConsumptionCostNaira=null, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now.AddDays(-2) },
                new GenService.API.Domain.DieselTankReading { Location="Port Harcourt Office", TankIdentifier="Main Generator Tank", ReadingDate=now.AddDays(-1), TankLevelLitres=4650, PreviousLevelLitres=5000, ConsumptionLitres=350, CostPerLitreNaira=1250, TotalConsumptionCostNaira=437500, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now.AddDays(-1) },
                new GenService.API.Domain.DieselTankReading { Location="Port Harcourt Office", TankIdentifier="Main Generator Tank", ReadingDate=now,             TankLevelLitres=4280, PreviousLevelLitres=4650, ConsumptionLitres=370, CostPerLitreNaira=1250, TotalConsumptionCostNaira=462500, LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo", CreatedAt=now },
                // DR Overhead Tank
                new GenService.API.Domain.DieselTankReading { Location="DR", TankIdentifier="DR Overhead Tank", ReadingDate=now.AddDays(-1), TankLevelLitres=2800, PreviousLevelLitres=3200, ConsumptionLitres=400, CostPerLitreNaira=1250, TotalConsumptionCostNaira=500000, LoggedByEmail="technician@demo.local", LoggedByName="Chukwudi Nwosu", CreatedAt=now.AddDays(-1) },
                new GenService.API.Domain.DieselTankReading { Location="DR", TankIdentifier="DR Overhead Tank", ReadingDate=now,             TankLevelLitres=2450, PreviousLevelLitres=2800, ConsumptionLitres=350, CostPerLitreNaira=1250, TotalConsumptionCostNaira=437500, LoggedByEmail="technician@demo.local", LoggedByName="Chukwudi Nwosu", CreatedAt=now },
                // Woji Store Tank
                new GenService.API.Domain.DieselTankReading { Location="Woji", TankIdentifier="Woji Store Tank", ReadingDate=now.AddDays(-1), TankLevelLitres=1200, PreviousLevelLitres=1500, ConsumptionLitres=300, CostPerLitreNaira=1250, TotalConsumptionCostNaira=375000, LoggedByEmail="driver@demo.local", LoggedByName="Bola Adeyemi", CreatedAt=now.AddDays(-1) },
                new GenService.API.Domain.DieselTankReading { Location="Woji", TankIdentifier="Woji Store Tank", ReadingDate=now,             TankLevelLitres=980,  PreviousLevelLitres=1200, ConsumptionLitres=220, CostPerLitreNaira=1250, TotalConsumptionCostNaira=275000, LoggedByEmail="driver@demo.local", LoggedByName="Bola Adeyemi", CreatedAt=now }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 7 demo diesel tank readings.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: DieselTankReadings"); }

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
