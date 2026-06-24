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
// GetConnectionString reads the Azure "Connection strings" tab (SQLAZURECONNSTR_DefaultConnection).
// Fallback to plain app setting "DefaultConnection" in case it was set in the "App settings" tab instead.
var dbConnectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")          // Connection strings tab (SQLAzure)
    ?? builder.Configuration["DefaultConnection"]                            // App settings tab (plain name)
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]         // App settings tab (prefixed)
    ?? builder.Configuration["SQLAZURECONNSTR_DefaultConnection"];           // raw env var

builder.Services.AddDbContext<GenServiceDbContext>(options =>
    options.UseSqlServer(
        dbConnectionString,
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
// Hardcoded production origin so this works even if Azure env-var mapping breaks.
// WithOrigins() is simpler and more reliable than SetIsOriginAllowed() on Azure Linux.
var hardcodedOrigins = new[]
{
    "https://nice-meadow-065144b03.7.azurestaticapps.net",  // Azure SWA production
    "http://localhost:5173",                                  // Vite dev
    "https://localhost:5173",
    "http://localhost:3000",
};
// Also include anything in Cors:AllowedOrigins config (additive, not a replacement)
var configuredOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var allCorsOrigins = hardcodedOrigins
    .Concat(configuredOrigins)
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppPolicy", policy =>
        policy.WithOrigins(allCorsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AuditService>();

// ── Background services ────────────────────────────────────────────────────────
builder.Services.AddHostedService<MaintenanceReminderService>();

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

// ── QuestPDF Community license ────────────────────────────────────────────────
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// ── Build app ─────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Database bootstrap ───────────────────────────────────────────────────────
// Schema creation runs in ALL environments (required for first-time Azure deploy).
// Demo data seeding runs only in non-Production environments.
{
    using var scope = app.Services.CreateScope();
    var db  = scope.ServiceProvider.GetRequiredService<GenServiceDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Schema — create fresh OR apply column additions to existing DB
    try
    {
        var created = await db.Database.EnsureCreatedAsync();
        log.LogInformation(created
            ? "✅ Database created fresh — seeding demo data..."
            : "✅ Database already exists — checking seed guards...");

        if (!created)
        {
            // Add any columns that were introduced after the initial EnsureCreated.
            // Each statement is wrapped in IF NOT EXISTS so it is safe to run repeatedly.
            await ApplySchemaUpdatesAsync(db, log);
        }
    }
    catch (Exception ex)
    {
        log.LogError(ex, "❌ EnsureCreated failed — DB may be unavailable.");
        // continue; auth still works without DB
    }

    // ── Demo data seeding — skipped in Production ────────────────────────────
    if (!app.Environment.IsProduction())
    {

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
                // V/26/001 — In Workshop, assessed, awaiting parts
                new GenService.API.Domain.VehicleMaintenanceRequest {
                    RequestNumber = "V/26/001", VehicleRegNo = "PF 4079 SPY", VehicleType = "TOYOTA LAND CRUISER PRADO",
                    MaintenanceType = "MajorRepair", OdometerReading = "142,500 km",
                    Description = "Engine oil leak detected during pre-trip inspection. Oil dripping from undercarriage near crankshaft area.",
                    Priority = "High", Status = "AwaitingParts", CurrentLocation = "Port Harcourt Office",
                    ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-10),
                    WorkshopName = "AutoFix Garage", WorkshopLocation = "Woji, Port Harcourt",
                    DateDeliveredToWorkshop = now.AddDays(-9), SentToWorkshopAt = now.AddDays(-9),
                    FaultIdentified = "Crankshaft rear oil seal worn out. Oil leaking into bell housing.",
                    ProposedSolution = "Replace crankshaft rear oil seal and inspect flywheel housing gasket.",
                    ResolutionType = "Outsourced", PartsRequired = true, PartsSource = "NewPurchase",
                    ProcurementMethod = "PurchaseOrder", PartsSuppliedBy = "Third Party Vendor",
                    SparesCostNaira = 45000,
                    RequestedByEmail = "driver@demo.local", RequestedByName = "Bola Adeyemi", CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-9)
                },
                // V/26/002 — In Workshop, routine service
                new GenService.API.Domain.VehicleMaintenanceRequest {
                    RequestNumber = "V/26/002", VehicleRegNo = "PHC 185 AM", VehicleType = "NISSAN PICKUP",
                    MaintenanceType = "RoutineService", OdometerReading = "87,200 km",
                    Description = "Routine 10,000km service — oil change, oil filter, fuel filter, air filter and brake inspection.",
                    Priority = "Normal", Status = "InWorkshop", CurrentLocation = "Port Harcourt Office",
                    ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-3),
                    WorkshopName = "PH Motors Workshop", WorkshopLocation = "GRA, Port Harcourt",
                    DateDeliveredToWorkshop = now.AddDays(-2), SentToWorkshopAt = now.AddDays(-2),
                    RequestedByEmail = "driver2@demo.local", RequestedByName = "Kwame Asante", CreatedAt = now.AddDays(-3), UpdatedAt = now.AddDays(-2)
                },
                // V/26/003 — Approved, minor repair
                new GenService.API.Domain.VehicleMaintenanceRequest {
                    RequestNumber = "V/26/003", VehicleRegNo = "RVS 526 BC", VehicleType = "TOYOTA LAND CRUISER (200 SERIES)",
                    MaintenanceType = "MinorRepair",
                    Description = "Two front tyres have severely worn treads and need replacement before next field trip to Bonny.",
                    Priority = "Urgent", Status = "Approved", CurrentLocation = "Port Harcourt Office",
                    ApprovedByEmail = "manager@demo.local", ApprovedByName = "Bobby Tholath", ApprovedAt = now.AddHours(-6),
                    RequestedByEmail = "driver@demo.local", RequestedByName = "Bola Adeyemi", CreatedAt = now.AddDays(-1), UpdatedAt = now.AddHours(-6)
                },
                // V/26/004 — Pending
                new GenService.API.Domain.VehicleMaintenanceRequest {
                    RequestNumber = "V/26/004", VehicleRegNo = "LAG 001 DES", VehicleType = "TOYOTA HILUX",
                    MaintenanceType = "RoutineService",
                    Description = "Pre-travel vehicle inspection required before Lagos to Bonny trip scheduled next week.",
                    Priority = "Normal", Status = "Pending", CurrentLocation = "Lagos Office",
                    RequestedByEmail = "driver2@demo.local", RequestedByName = "Kwame Asante", CreatedAt = now.AddHours(-4), UpdatedAt = now.AddHours(-4)
                },
                // V/26/005 — Completed with handover
                new GenService.API.Domain.VehicleMaintenanceRequest {
                    RequestNumber = "V/26/005", VehicleRegNo = "BER 500 MU", VehicleType = "HYUNDAI MINI TRUCK",
                    MaintenanceType = "MinorRepair", OdometerReading = "63,400 km",
                    Description = "Minor dent and paint damage on front bumper after compound parking incident.",
                    Priority = "Low", Status = "Completed", CurrentLocation = "Lagos Office",
                    ApprovedByEmail = "supervisor@demo.local", ApprovedByName = "Emeka Okonkwo", ApprovedAt = now.AddDays(-22),
                    WorkshopName = "Classic Body Works", WorkshopLocation = "Ikeja, Lagos",
                    DateDeliveredToWorkshop = now.AddDays(-21), SentToWorkshopAt = now.AddDays(-21),
                    FaultIdentified = "Front bumper has 3 dents and cracked paint on driver side. No structural damage.",
                    ProposedSolution = "Panel beating and full repaint of front bumper.", ResolutionType = "Outsourced",
                    PartsRequired = false, WorkDone = "Front bumper panel beaten, primed and repainted to match original colour.",
                    ActionedBy = "Classic Body Works", SparesCostNaira = 25000,
                    HandoverConfirmed = true, HandedOverBy = "Emeka Okonkwo", DateHandedOver = now.AddDays(-15),
                    CompletedAt = now.AddDays(-16), RequestedByEmail = "driver@demo.local",
                    RequestedByName = "Bola Adeyemi", CreatedAt = now.AddDays(-25), UpdatedAt = now.AddDays(-15)
                }
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

    // ── Seed daily parameter logs ────────────────────────────────────────────
    try
    {
        if (!await db.DailyParameterLogs.AnyAsync())
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            db.DailyParameterLogs.AddRange(
                // PHC Office — today
                new GenService.API.Domain.DailyParameterLog
                {
                    LogDate=today, Location="Port Harcourt Office",
                    NepaHoursAvailable=8, GeneratorHoursRun=8.5, DieselConsumedLitres=65, DieselBalanceLitres=380,
                    GeneratorStatus="Running", GeneratorRunHourMeter=16245,
                    WaterSource="Borehole", WaterTankLevelPercent=75, WaterStatus="Adequate",
                    StaffPresent=32, ExpectedStaff=35, VisitorCount=4,
                    CleaningDone=true, WasteDisposed=true, SecurityStatus="Normal",
                    MaintenanceIssues="AC unit in Conference Room B making noise — already reported.",
                    ActionsTaken="Conference room AC referred to maintenance. Generator oil level topped up.",
                    PendingActions="Await AC repair completion.",
                    GeneralRemarks="Day went smoothly. NEPA was available from 08:00-16:00.",
                    LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo",
                    CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                // PHC Office — yesterday
                new GenService.API.Domain.DailyParameterLog
                {
                    LogDate=today.AddDays(-1), Location="Port Harcourt Office",
                    NepaHoursAvailable=10, GeneratorHoursRun=6, DieselConsumedLitres=55, DieselBalanceLitres=445,
                    GeneratorStatus="Standby", GeneratorRunHourMeter=16238,
                    WaterSource="Borehole", WaterTankLevelPercent=80, WaterStatus="Adequate",
                    StaffPresent=35, ExpectedStaff=35, VisitorCount=2,
                    CleaningDone=true, WasteDisposed=true, SecurityStatus="Normal",
                    MaintenanceIssues=null, ActionsTaken="Routine checks completed.",
                    PendingActions=null, GeneralRemarks="Good NEPA supply today.",
                    LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo",
                    CreatedAt=DateTime.UtcNow.AddDays(-1), UpdatedAt=DateTime.UtcNow.AddDays(-1)
                },
                // Lagos Office — today
                new GenService.API.Domain.DailyParameterLog
                {
                    LogDate=today, Location="Lagos Office",
                    NepaHoursAvailable=14, GeneratorHoursRun=4, DieselConsumedLitres=40, DieselBalanceLitres=180,
                    GeneratorStatus="Standby", GeneratorRunHourMeter=8920,
                    WaterSource="Municipal", WaterTankLevelPercent=90, WaterStatus="Adequate",
                    StaffPresent=18, ExpectedStaff=20, VisitorCount=6,
                    CleaningDone=true, WasteDisposed=false, SecurityStatus="Normal",
                    MaintenanceIssues="Waste disposal pending — contractor did not show up.",
                    ActionsTaken="Generator ran for 4hrs during midday outage (12:00-16:00).",
                    PendingActions="Chase waste disposal contractor tomorrow.",
                    GeneralRemarks="Relatively good NEPA supply in Lagos today.",
                    LoggedByEmail="supervisor@demo.local", LoggedByName="Emeka Okonkwo",
                    CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                // DR — today
                new GenService.API.Domain.DailyParameterLog
                {
                    LogDate=today, Location="DR",
                    NepaHoursAvailable=4, GeneratorHoursRun=8, DieselConsumedLitres=80, DieselBalanceLitres=220,
                    GeneratorStatus="Running", GeneratorRunHourMeter=13750,
                    WaterSource="Borehole", WaterTankLevelPercent=60, WaterStatus="Adequate",
                    StaffPresent=8, ExpectedStaff=10, VisitorCount=1,
                    CleaningDone=true, WasteDisposed=true, SecurityStatus="Normal",
                    MaintenanceIssues="Borehole pump pressure slightly low.",
                    ActionsTaken="Adjusted pressure valve. Will monitor tomorrow.",
                    PendingActions="Monitor borehole pump pressure.",
                    GeneralRemarks="Poor NEPA supply — generator ran most of the day.",
                    LoggedByEmail="technician@demo.local", LoggedByName="Chukwudi Nwosu",
                    CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                // Woji — today (low diesel warning)
                new GenService.API.Domain.DailyParameterLog
                {
                    LogDate=today, Location="Woji",
                    NepaHoursAvailable=16, GeneratorHoursRun=3.5, DieselConsumedLitres=35, DieselBalanceLitres=45,
                    GeneratorStatus="Standby", GeneratorRunHourMeter=10835,
                    WaterSource="Borehole", WaterTankLevelPercent=85, WaterStatus="Adequate",
                    StaffPresent=5, ExpectedStaff=6, VisitorCount=0,
                    CleaningDone=true, WasteDisposed=true, SecurityStatus="Normal",
                    MaintenanceIssues="Diesel tank critically low — 45 litres remaining.",
                    ActionsTaken="Diesel requisition raised urgently (DR-2026-087).",
                    PendingActions="Await diesel delivery — expected tomorrow morning.",
                    GeneralRemarks="Good NEPA supply. Diesel urgent — supply expected tomorrow.",
                    LoggedByEmail="driver@demo.local", LoggedByName="Bola Adeyemi",
                    CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 5 demo daily parameter logs.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: DailyParameterLogs"); }

    // ── Seed AppUsers ────────────────────────────────────────────────────────
    try
    {
        // Seed all dev/demo users if the table is empty
        if (!await db.AppUsers.AnyAsync())
        {
            string Hash(string pw) => BCrypt.Net.BCrypt.HashPassword(pw, workFactor: 11);

            db.AppUsers.AddRange(
                new GenService.API.Domain.AppUser
                {
                    Email="admin@dev.local",          FullName="Dev Administrator",
                    PasswordHash=Hash("Dev2026!"),
                    Role="SystemAdmin",               Department="IT",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="manager@demo.local",       FullName="Bobby Tholath",
                    PasswordHash=Hash("DemoManager2026!"),
                    Role="DepartmentManager",         Department="General Service",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="supervisor@demo.local",    FullName="Emeka Okonkwo",
                    PasswordHash=Hash("DemoSuper2026!"),
                    Role="Supervisor",                Department="General Service",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="technician@demo.local",    FullName="Chukwudi Nwosu",
                    PasswordHash=Hash("DemoTech2026!"),
                    Role="Technician",                Department="General Service",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="tech2@demo.local",         FullName="Grace Obi",
                    PasswordHash=Hash("DemoTech2026!"),
                    Role="Technician",                Department="General Service",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="driver@demo.local",        FullName="Bola Adeyemi",
                    PasswordHash=Hash("DemoDriver2026!"),
                    Role="Driver",                    Department="General Service",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="driver2@demo.local",       FullName="Kwame Asante",
                    PasswordHash=Hash("DemoDriver2026!"),
                    Role="Driver",                    Department="General Service",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="requester1@demo.local",    FullName="Fatima Al-Hassan",
                    PasswordHash=Hash("DemoReq2026!"),
                    Role="Requester",                 Department="Finance",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                },
                new GenService.API.Domain.AppUser
                {
                    Email="requester2@demo.local",    FullName="Tunde Babatunde",
                    PasswordHash=Hash("DemoReq2026!"),
                    Role="Requester",                 Department="HR",
                    IsActive=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow
                }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 9 app users with BCrypt-hashed passwords.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: AppUsers"); }

    // ── Ensure production Microsoft SSO user always exists ───────────────────
    // This runs unconditionally so the real production account is always present
    // even after demo data was seeded first.
    try
    {
        const string prodEmail = "best.aihebholoria@desicongroup.com";
        if (!await db.AppUsers.AnyAsync(u => u.Email == prodEmail))
        {
            db.AppUsers.Add(new GenService.API.Domain.AppUser
            {
                Email        = prodEmail,
                FullName     = "Aihe Bholoria",
                // Password hash is a random guid — production login uses Microsoft SSO only
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 11),
                Role         = "DepartmentManager",
                Department   = "General Service",
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            log.LogInformation("✅ Production user created: {Email}", prodEmail);
        }
        else
        {
            log.LogInformation("ℹ️ Production user already exists: {Email}", prodEmail);
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Failed to ensure production user"); }

    // ── Seed store items ─────────────────────────────────────────────────────
    try
    {
        if (!await db.StoreItems.AnyAsync())
        {
            var year = DateTime.UtcNow.Year.ToString()[2..]; // "26"
            int seq = 1;
            string Code() => $"SI/{year}/{seq++:D3}";
            var now = DateTime.UtcNow;

            db.StoreItems.AddRange(
                // Generator parts
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Engine Oil (Total 15W-40) 20L",         Category="Lubricants & Oils",   Unit="Drums",  QuantityInStock=8,  ReorderLevel=3,  UnitCostNaira=52000,  StoreLocation="Store A — Shelf 1", Supplier="Total Nigeria",   CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Fuel Filter (CAT Generator)",           Category="Generator Parts",     Unit="Pieces", QuantityInStock=6,  ReorderLevel=4,  UnitCostNaira=8500,   StoreLocation="Store A — Shelf 2", Supplier="CAT Dealer Lagos",CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Oil Filter (CAT 350KVA)",              Category="Generator Parts",     Unit="Pieces", QuantityInStock=5,  ReorderLevel=4,  UnitCostNaira=7200,   StoreLocation="Store A — Shelf 2", Supplier="CAT Dealer Lagos",CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Air Filter (Cummins 275KVA)",          Category="Generator Parts",     Unit="Pieces", QuantityInStock=3,  ReorderLevel=2,  UnitCostNaira=12500,  StoreLocation="Store A — Shelf 2", Supplier="Cummins Nigeria", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="V-Belt (Generator Alternator)",        Category="Generator Parts",     Unit="Pieces", QuantityInStock=4,  ReorderLevel=2,  UnitCostNaira=3800,   StoreLocation="Store A — Shelf 3", Supplier="Local Supplier",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Coolant (Generator) 5L",              Category="Lubricants & Oils",   Unit="Pieces", QuantityInStock=10, ReorderLevel=4,  UnitCostNaira=6500,   StoreLocation="Store A — Shelf 1", Supplier="Total Nigeria",   CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                // AC parts
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="R22 Refrigerant Gas 500g",            Category="AC Parts",            Unit="Pieces", QuantityInStock=12, ReorderLevel=5,  UnitCostNaira=18500,  StoreLocation="Store A — Shelf 4", Supplier="AC Supplies Ltd", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="R410A Refrigerant Gas 500g",          Category="AC Parts",            Unit="Pieces", QuantityInStock=8,  ReorderLevel=4,  UnitCostNaira=22000,  StoreLocation="Store A — Shelf 4", Supplier="AC Supplies Ltd", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="AC Capacitor (25μF)",                 Category="AC Parts",            Unit="Pieces", QuantityInStock=6,  ReorderLevel=3,  UnitCostNaira=3500,   StoreLocation="Store A — Shelf 4", Supplier="Local Supplier",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="AC Compressor Gasket",               Category="AC Parts",            Unit="Pieces", QuantityInStock=2,  ReorderLevel=3,  UnitCostNaira=5200,   StoreLocation="Store A — Shelf 4", Supplier="AC Supplies Ltd", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                // Electrical
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="LED Bulb 18W (Energy Saver)",         Category="Electrical Items",    Unit="Pieces", QuantityInStock=45, ReorderLevel=20, UnitCostNaira=2200,   StoreLocation="Store B — Shelf 1", Supplier="Philips Distributor", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="MCB Circuit Breaker 32A",             Category="Electrical Items",    Unit="Pieces", QuantityInStock=8,  ReorderLevel=4,  UnitCostNaira=4800,   StoreLocation="Store B — Shelf 2", Supplier="Schneider Distributor", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Extension Socket (4-Way)",            Category="Electrical Items",    Unit="Pieces", QuantityInStock=15, ReorderLevel=6,  UnitCostNaira=3500,   StoreLocation="Store B — Shelf 2", Supplier="Local Supplier",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Electrical Cable 2.5mm (Per Roll)",   Category="Electrical Items",    Unit="Rolls",  QuantityInStock=4,  ReorderLevel=2,  UnitCostNaira=35000,  StoreLocation="Store B — Shelf 3", Supplier="Nigerchin",       CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                // Plumbing
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="PVC Pipe 1/2\" (Per Length)",         Category="Plumbing Items",      Unit="Pieces", QuantityInStock=20, ReorderLevel=8,  UnitCostNaira=1800,   StoreLocation="Store B — Shelf 5", Supplier="Local Supplier",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Water Tap (Brass)",                   Category="Plumbing Items",      Unit="Pieces", QuantityInStock=5,  ReorderLevel=3,  UnitCostNaira=4200,   StoreLocation="Store B — Shelf 5", Supplier="Local Supplier",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                // Cleaning supplies
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Floor Cleaner 5L",                    Category="Cleaning Supplies",   Unit="Pieces", QuantityInStock=18, ReorderLevel=8,  UnitCostNaira=6500,   StoreLocation="Store C — Shelf 1", Supplier="Unilever Dist.",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Toilet Cleaner 1L",                   Category="Cleaning Supplies",   Unit="Pieces", QuantityInStock=24, ReorderLevel=10, UnitCostNaira=1800,   StoreLocation="Store C — Shelf 1", Supplier="Unilever Dist.",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Garbage Bags (Pack of 50)",           Category="Cleaning Supplies",   Unit="Packets",QuantityInStock=30, ReorderLevel=12, UnitCostNaira=2200,   StoreLocation="Store C — Shelf 2", Supplier="Local Supplier",  CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Hand Wash Liquid 5L",                 Category="Cleaning Supplies",   Unit="Pieces", QuantityInStock=10, ReorderLevel=5,  UnitCostNaira=4800,   StoreLocation="Store C — Shelf 2", Supplier="PZ Cussons Dist.",CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                // Vehicle parts (critical — low stock)
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Engine Oil Filter (Toyota Hilux)",    Category="Vehicle Parts",       Unit="Pieces", QuantityInStock=2,  ReorderLevel=3,  UnitCostNaira=3500,   StoreLocation="Store A — Shelf 6", Supplier="Toyota Authorised",CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Brake Pad Set (Toyota Land Cruiser)", Category="Vehicle Parts",       Unit="Sets",   QuantityInStock=1,  ReorderLevel=2,  UnitCostNaira=35000,  StoreLocation="Store A — Shelf 6", Supplier="Toyota Authorised",CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                // Safety
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Safety Gloves (Pairs)",              Category="Safety Items",        Unit="Pairs",  QuantityInStock=25, ReorderLevel=10, UnitCostNaira=1500,   StoreLocation="Store C — Shelf 3", Supplier="Safety Supplies", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now },
                new GenService.API.Domain.StoreItem { ItemCode=Code(), Name="Safety Boots (Size 42)",             Category="Safety Items",        Unit="Pairs",  QuantityInStock=3,  ReorderLevel=3,  UnitCostNaira=18000,  StoreLocation="Store C — Shelf 3", Supplier="Safety Supplies", CreatedByEmail="admin@dev.local", CreatedAt=now, UpdatedAt=now }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 24 store items.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: StoreItems"); }

    // ── Seed store requisitions ──────────────────────────────────────────────
    try
    {
        if (!await db.StoreRequisitions.AnyAsync())
        {
            var items   = await db.StoreItems.ToListAsync();
            var now     = DateTime.UtcNow;
            var year    = now.Year.ToString()[2..];

            var getItem = (string name) => items.FirstOrDefault(i => i.Name.Contains(name));

            var oilItem     = getItem("Engine Oil (Total");
            var fuelFilter  = getItem("Fuel Filter");
            var ledBulb     = getItem("LED Bulb");
            var floorCleaner= getItem("Floor Cleaner");
            var toilet      = getItem("Toilet Cleaner");
            var garbageBags = getItem("Garbage Bags");
            var rfGas       = getItem("R22 Refrigerant");

            var reqs = new List<GenService.API.Domain.StoreRequisition>();

            // SR/26/001 — Issued (completed)
            if (oilItem != null && fuelFilter != null)
            {
                reqs.Add(new GenService.API.Domain.StoreRequisition
                {
                    RequisitionNumber = $"SR/{year}/001",
                    RequestedByEmail  = "technician@demo.local",
                    RequestedByName   = "Chukwudi Nwosu",
                    Department        = "General Service",
                    Purpose           = "Generator 250hr service — CAT 350KVA PHC Office",
                    LinkedReference   = "E/26/001",
                    Status            = GenService.API.Domain.StoreRequisitionStatus.Issued,
                    ApprovedByEmail   = "supervisor@demo.local",
                    ApprovedByName    = "Emeka Okonkwo",
                    ApprovedAt        = now.AddDays(-5),
                    IssuedByEmail     = "admin@dev.local",
                    IssuedByName      = "Dev Administrator",
                    IssuedAt          = now.AddDays(-5),
                    CreatedAt         = now.AddDays(-6),
                    UpdatedAt         = now.AddDays(-5),
                    Items             =
                    [
                        new() { StoreItemId=oilItem.Id,    ItemName=oilItem.Name,    Unit=oilItem.Unit,    QuantityRequested=2, QuantityIssued=2, UnitCostNaira=oilItem.UnitCostNaira },
                        new() { StoreItemId=fuelFilter.Id, ItemName=fuelFilter.Name, Unit=fuelFilter.Unit, QuantityRequested=1, QuantityIssued=1, UnitCostNaira=fuelFilter.UnitCostNaira },
                    ]
                });
            }

            // SR/26/002 — Approved (waiting for issuance)
            if (ledBulb != null)
            {
                reqs.Add(new GenService.API.Domain.StoreRequisition
                {
                    RequisitionNumber = $"SR/{year}/002",
                    RequestedByEmail  = "supervisor@demo.local",
                    RequestedByName   = "Emeka Okonkwo",
                    Department        = "General Service",
                    Purpose           = "Replacement of faulty lights in HR corridor and meeting rooms",
                    Status            = GenService.API.Domain.StoreRequisitionStatus.Approved,
                    ApprovedByEmail   = "manager@demo.local",
                    ApprovedByName    = "Bobby Tholath",
                    ApprovedAt        = now.AddDays(-1),
                    CreatedAt         = now.AddDays(-2),
                    UpdatedAt         = now.AddDays(-1),
                    Items             =
                    [
                        new() { StoreItemId=ledBulb.Id, ItemName=ledBulb.Name, Unit=ledBulb.Unit, QuantityRequested=10, QuantityIssued=0, UnitCostNaira=ledBulb.UnitCostNaira },
                    ]
                });
            }

            // SR/26/003 — Pending (just submitted)
            if (floorCleaner != null && toilet != null && garbageBags != null)
            {
                reqs.Add(new GenService.API.Domain.StoreRequisition
                {
                    RequisitionNumber = $"SR/{year}/003",
                    RequestedByEmail  = "driver@demo.local",
                    RequestedByName   = "Bola Adeyemi",
                    Department        = "General Service",
                    Purpose           = "Monthly cleaning supply replenishment — Lagos Office",
                    Status            = GenService.API.Domain.StoreRequisitionStatus.Pending,
                    CreatedAt         = now.AddHours(-3),
                    UpdatedAt         = now.AddHours(-3),
                    Items             =
                    [
                        new() { StoreItemId=floorCleaner.Id, ItemName=floorCleaner.Name, Unit=floorCleaner.Unit, QuantityRequested=3, QuantityIssued=0, UnitCostNaira=floorCleaner.UnitCostNaira },
                        new() { StoreItemId=toilet.Id,       ItemName=toilet.Name,       Unit=toilet.Unit,       QuantityRequested=6, QuantityIssued=0, UnitCostNaira=toilet.UnitCostNaira },
                        new() { StoreItemId=garbageBags.Id,  ItemName=garbageBags.Name,  Unit=garbageBags.Unit,  QuantityRequested=5, QuantityIssued=0, UnitCostNaira=garbageBags.UnitCostNaira },
                    ]
                });
            }

            if (reqs.Any())
            {
                db.StoreRequisitions.AddRange(reqs);
                await db.SaveChangesAsync();
                log.LogInformation("✅ Seeded {Count} store requisitions.", reqs.Count);
            }
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: StoreRequisitions"); }

    // ── Seed diesel requisitions ─────────────────────────────────────────────
    try
    {
        if (!await db.DieselRequisitions.AnyAsync())
        {
            var now  = DateTime.UtcNow;
            var year = now.Year.ToString()[2..];

            db.DieselRequisitions.AddRange(
                // DR/26/001 — Dispensed (completed full cycle)
                new GenService.API.Domain.DieselRequisition
                {
                    RequisitionNumber        = $"DR/{year}/001",
                    Purpose                  = "Weekly generator refuel — DR Building CAT 350KVA",
                    EquipmentType            = GenService.API.Domain.DieselEquipmentType.Generator,
                    EquipmentReference       = "CAT-350KVA-DR",
                    Location                 = "DR Building",
                    QuantityRequestedLitres  = 400,
                    RequestedByEmail         = "technician@demo.local",
                    RequestedByName          = "Chukwudi Nwosu",
                    Department               = "General Service",
                    Status                   = GenService.API.Domain.DieselRequisitionStatus.Dispensed,
                    ApprovedByEmail          = "manager@demo.local",
                    ApprovedByName           = "Bobby Tholath",
                    ApprovedAt               = now.AddDays(-3),
                    DispensedByEmail         = "supervisor@demo.local",
                    DispensedByName          = "Emeka Okonkwo",
                    DispensedAt              = now.AddDays(-3).AddHours(2),
                    QuantityDispensedLitres  = 400,
                    TankLevelBeforeLitres    = 180,
                    TankLevelAfterLitres     = 580,
                    UnitCostPerLitreNaira    = 1050,
                    TotalCostNaira           = 420000,
                    Notes                    = "Regular weekly top-up. Tank was approaching critical level.",
                    CreatedAt                = now.AddDays(-4),
                    UpdatedAt                = now.AddDays(-3).AddHours(2),
                },
                // DR/26/002 — Approved (waiting for dispensing)
                new GenService.API.Domain.DieselRequisition
                {
                    RequisitionNumber        = $"DR/{year}/002",
                    Purpose                  = "Emergency fuel top-up — PHC Office Cummins 275KVA generator running low",
                    EquipmentType            = GenService.API.Domain.DieselEquipmentType.Generator,
                    EquipmentReference       = "CUMMINS-275KVA-PHC",
                    Location                 = "PHC Office",
                    QuantityRequestedLitres  = 300,
                    RequestedByEmail         = "supervisor@demo.local",
                    RequestedByName          = "Emeka Okonkwo",
                    Department               = "General Service",
                    Status                   = GenService.API.Domain.DieselRequisitionStatus.Approved,
                    ApprovedByEmail          = "manager@demo.local",
                    ApprovedByName           = "Bobby Tholath",
                    ApprovedAt               = now.AddHours(-2),
                    Notes                    = "Tank at 15% capacity — below minimum operating threshold.",
                    CreatedAt                = now.AddHours(-4),
                    UpdatedAt                = now.AddHours(-2),
                },
                // DR/26/003 — Pending (just submitted)
                new GenService.API.Domain.DieselRequisition
                {
                    RequisitionNumber        = $"DR/{year}/003",
                    Purpose                  = "Diesel for site vehicle — monthly fleet fuelling allocation",
                    EquipmentType            = GenService.API.Domain.DieselEquipmentType.Vehicle,
                    EquipmentReference       = "PHC 185 AM",
                    Location                 = "PHC Office",
                    QuantityRequestedLitres  = 80,
                    RequestedByEmail         = "driver@demo.local",
                    RequestedByName          = "Bola Adeyemi",
                    Department               = "General Service",
                    Status                   = GenService.API.Domain.DieselRequisitionStatus.Pending,
                    Notes                    = "Vehicle fuel gauge at quarter-tank.",
                    CreatedAt                = now.AddMinutes(-45),
                    UpdatedAt                = now.AddMinutes(-45),
                }
            );
            await db.SaveChangesAsync();
            log.LogInformation("✅ Seeded 3 diesel requisitions.");
        }
    }
    catch (Exception ex) { log.LogError(ex, "❌ Seed failed: DieselRequisitions"); }

    } // end if (!IsProduction) seed block

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
app.MapHealthChecks("/health").RequireCors("AppPolicy");

app.MapGet("/api/v1/ping", () => new
{
    status    = "GenService API is running",
    version   = "1.0",
    authMode  = app.Configuration["Auth:Mode"] ?? "DevJwt",
    timestamp = DateTime.UtcNow
}).WithName("Ping").WithOpenApi();

app.Run();

// ── Schema upgrade helper ─────────────────────────────────────────────────────
// Safely adds columns introduced after the initial EnsureCreated.
// Each ALTER TABLE is wrapped in IF NOT EXISTS, so this is idempotent.
static async Task ApplySchemaUpdatesAsync(
    GenService.API.Data.GenServiceDbContext db,
    ILogger log)
{
    try
    {
        // Helper: add a nullable column if it doesn't already exist
        static string AddColIfMissing(string table, string col, string sqlType)
            => $"""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'{table}') AND name = N'{col}')
                ALTER TABLE {table} ADD {col} {sqlType} NULL;
                """;

        // Helper: add a NOT NULL bit column with default 0
        static string AddBitColIfMissing(string table, string col)
            => $"""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'{table}') AND name = N'{col}')
                ALTER TABLE {table} ADD {col} bit NOT NULL DEFAULT 0;
                """;

        var statements = new List<string>
        {
            // ── VehicleMaintenanceRequests ──────────────────────────────────
            AddColIfMissing ("VehicleMaintenanceRequests", "OdometerReading",          "nvarchar(100)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "DateDeliveredToWorkshop",   "datetime2"),
            AddColIfMissing ("VehicleMaintenanceRequests", "SentToWorkshopAt",          "datetime2"),
            AddColIfMissing ("VehicleMaintenanceRequests", "FaultIdentified",           "nvarchar(2000)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "ProposedSolution",          "nvarchar(2000)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "ResolutionType",            "nvarchar(30)"),
            AddBitColIfMissing("VehicleMaintenanceRequests", "PartsRequired"),
            AddColIfMissing ("VehicleMaintenanceRequests", "PartsSource",               "nvarchar(30)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "ProcurementMethod",         "nvarchar(30)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "PartsSuppliedBy",           "nvarchar(200)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "SparesCostNaira",           "decimal(18,2)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "WorkDone",                  "nvarchar(2000)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "ActionedBy",                "nvarchar(200)"),
            AddColIfMissing ("VehicleMaintenanceRequests", "CompletedAt",               "datetime2"),
            AddBitColIfMissing("VehicleMaintenanceRequests", "HandoverConfirmed"),
            AddColIfMissing ("VehicleMaintenanceRequests", "DateHandedOver",            "datetime2"),
            AddColIfMissing ("VehicleMaintenanceRequests", "HandedOverBy",              "nvarchar(100)"),

            // ── EquipmentMaintenanceRequests ────────────────────────────────
            AddColIfMissing ("EquipmentMaintenanceRequests", "FaultIdentified",         "nvarchar(2000)"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "ProposedSolution",        "nvarchar(2000)"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "ResolutionType",          "nvarchar(30)"),
            AddBitColIfMissing("EquipmentMaintenanceRequests", "PartsRequired"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "PartsSource",             "nvarchar(30)"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "ProcurementMethod",       "nvarchar(30)"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "SparesCostNaira",         "decimal(18,2)"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "CompletedAt",             "datetime2"),
            AddBitColIfMissing("EquipmentMaintenanceRequests", "HandoverConfirmed"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "DateHandedOver",          "datetime2"),
            AddColIfMissing ("EquipmentMaintenanceRequests", "HandedOverBy",            "nvarchar(100)"),

            // ── FacilityMaintenanceRequests ─────────────────────────────────
            AddColIfMissing ("FacilityMaintenanceRequests", "FaultIdentified",          "nvarchar(2000)"),
            AddColIfMissing ("FacilityMaintenanceRequests", "ProposedSolution",         "nvarchar(2000)"),
            AddColIfMissing ("FacilityMaintenanceRequests", "ResolutionType",           "nvarchar(30)"),
            AddBitColIfMissing("FacilityMaintenanceRequests", "PartsRequired"),
            AddColIfMissing ("FacilityMaintenanceRequests", "PartsSource",              "nvarchar(30)"),
            AddColIfMissing ("FacilityMaintenanceRequests", "ProcurementMethod",        "nvarchar(30)"),
            AddColIfMissing ("FacilityMaintenanceRequests", "SparesCostNaira",          "decimal(18,2)"),
            AddColIfMissing ("FacilityMaintenanceRequests", "CompletedAt",              "datetime2"),
            AddBitColIfMissing("FacilityMaintenanceRequests", "HandoverConfirmed"),
            AddColIfMissing ("FacilityMaintenanceRequests", "DateHandedOver",           "datetime2"),
            AddColIfMissing ("FacilityMaintenanceRequests", "HandedOverBy",             "nvarchar(100)"),

            // ── MaintenanceSchedules — reminder / escalation tracking columns ──
            """
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'MaintenanceSchedules') AND name = N'EscalationLevel')
            ALTER TABLE MaintenanceSchedules ADD EscalationLevel int NOT NULL DEFAULT 0;
            """,
            AddColIfMissing("MaintenanceSchedules", "LastReminderSentAt",   "datetime2"),
            AddColIfMissing("MaintenanceSchedules", "LastEscalationSentAt", "datetime2"),

            // ── DailyParameterLogs (new table — create if missing) ──────────
            // EnsureCreated will create it on fresh DBs; on existing DBs we
            // use IF NOT EXISTS to create the table only if it doesn't exist yet.
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DailyParameterLogs')
            CREATE TABLE DailyParameterLogs (
                Id                    uniqueidentifier  NOT NULL PRIMARY KEY DEFAULT NEWID(),
                LogDate               date              NOT NULL,
                Location              nvarchar(200)     NOT NULL,
                NepaHoursAvailable    float             NULL,
                GeneratorHoursRun     float             NULL,
                DieselConsumedLitres  float             NULL,
                DieselBalanceLitres   float             NULL,
                GeneratorStatus       nvarchar(30)      NULL,
                GeneratorRunHourMeter float             NULL,
                WaterSource           nvarchar(30)      NULL,
                WaterTankLevelPercent float             NULL,
                WaterStatus           nvarchar(30)      NULL,
                StaffPresent          int               NULL,
                ExpectedStaff         int               NULL,
                VisitorCount          int               NULL,
                CleaningDone          bit               NOT NULL DEFAULT 0,
                WasteDisposed         bit               NOT NULL DEFAULT 0,
                SecurityStatus        nvarchar(50)      NULL,
                MaintenanceIssues     nvarchar(2000)    NULL,
                ActionsTaken          nvarchar(2000)    NULL,
                PendingActions        nvarchar(2000)    NULL,
                GeneralRemarks        nvarchar(2000)    NULL,
                LoggedByEmail         nvarchar(150)     NOT NULL,
                LoggedByName          nvarchar(100)     NOT NULL,
                CreatedAt             datetime2         NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt             datetime2         NOT NULL DEFAULT GETUTCDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DailyParameterLogs_Location_LogDate')
                CREATE UNIQUE INDEX IX_DailyParameterLogs_Location_LogDate ON DailyParameterLogs (Location, LogDate);
            """,

            // ── AppUsers — ensure nullable columns added after initial create ──
            AddColIfMissing("AppUsers", "LastLoginAt",    "datetime2"),
            AddColIfMissing("AppUsers", "CreatedByEmail", "nvarchar(150)"),

            // ── AppUsers (new table) ─────────────────────────────────────────
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AppUsers')
            CREATE TABLE AppUsers (
                Id              uniqueidentifier  NOT NULL PRIMARY KEY DEFAULT NEWID(),
                Email           nvarchar(150)     NOT NULL,
                FullName        nvarchar(100)     NOT NULL,
                PasswordHash    nvarchar(100)     NOT NULL,
                Role            nvarchar(30)      NOT NULL DEFAULT 'Requester',
                Department      nvarchar(100)     NOT NULL DEFAULT 'General Service',
                IsActive        bit               NOT NULL DEFAULT 1,
                LastLoginAt     datetime2         NULL,
                CreatedByEmail  nvarchar(150)     NULL,
                CreatedAt       datetime2         NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt       datetime2         NOT NULL DEFAULT GETUTCDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUsers_Email')
                CREATE UNIQUE INDEX IX_AppUsers_Email ON AppUsers (Email);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUsers_Role')
                CREATE INDEX IX_AppUsers_Role ON AppUsers (Role);
            """,

            // ── StoreItems ────────────────────────────────────────────────────
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StoreItems')
            CREATE TABLE StoreItems (
                Id              uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
                ItemCode        nvarchar(20)     NOT NULL,
                Name            nvarchar(200)    NOT NULL,
                Category        nvarchar(60)     NOT NULL,
                Unit            nvarchar(30)     NOT NULL DEFAULT 'Pieces',
                QuantityInStock float            NOT NULL DEFAULT 0,
                ReorderLevel    float            NOT NULL DEFAULT 0,
                UnitCostNaira   decimal(18,2)    NOT NULL DEFAULT 0,
                Description     nvarchar(500)    NULL,
                StoreLocation   nvarchar(200)    NULL,
                Supplier        nvarchar(200)    NULL,
                IsActive        bit              NOT NULL DEFAULT 1,
                CreatedByEmail  nvarchar(150)    NOT NULL DEFAULT '',
                CreatedAt       datetime2        NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt       datetime2        NOT NULL DEFAULT GETUTCDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoreItems_ItemCode')
                CREATE UNIQUE INDEX IX_StoreItems_ItemCode ON StoreItems (ItemCode);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoreItems_Category')
                CREATE INDEX IX_StoreItems_Category ON StoreItems (Category);
            """,

            // ── StoreRequisitions ─────────────────────────────────────────────
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StoreRequisitions')
            CREATE TABLE StoreRequisitions (
                Id                  uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
                RequisitionNumber   nvarchar(20)     NOT NULL,
                RequestedByEmail    nvarchar(150)    NOT NULL,
                RequestedByName     nvarchar(100)    NOT NULL,
                Department          nvarchar(100)    NOT NULL,
                Purpose             nvarchar(500)    NOT NULL,
                LinkedReference     nvarchar(50)     NULL,
                Status              nvarchar(20)     NOT NULL DEFAULT 'Pending',
                ApprovedByEmail     nvarchar(150)    NULL,
                ApprovedByName      nvarchar(100)    NULL,
                ApprovedAt          datetime2        NULL,
                RejectedByEmail     nvarchar(150)    NULL,
                RejectionReason     nvarchar(500)    NULL,
                RejectedAt          datetime2        NULL,
                IssuedByEmail       nvarchar(150)    NULL,
                IssuedByName        nvarchar(100)    NULL,
                IssuedAt            datetime2        NULL,
                Notes               nvarchar(1000)   NULL,
                CreatedAt           datetime2        NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt           datetime2        NOT NULL DEFAULT GETUTCDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoreRequisitions_RequisitionNumber')
                CREATE UNIQUE INDEX IX_StoreRequisitions_RequisitionNumber ON StoreRequisitions (RequisitionNumber);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoreRequisitions_Status')
                CREATE INDEX IX_StoreRequisitions_Status ON StoreRequisitions (Status);
            """,

            // ── StoreRequisitionItems ─────────────────────────────────────────
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StoreRequisitionItems')
            CREATE TABLE StoreRequisitionItems (
                Id                  uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
                RequisitionId       uniqueidentifier NOT NULL REFERENCES StoreRequisitions(Id) ON DELETE CASCADE,
                StoreItemId         uniqueidentifier NOT NULL REFERENCES StoreItems(Id),
                ItemName            nvarchar(200)    NOT NULL,
                Unit                nvarchar(30)     NOT NULL,
                QuantityRequested   float            NOT NULL,
                QuantityIssued      float            NOT NULL DEFAULT 0,
                UnitCostNaira       decimal(18,2)    NOT NULL DEFAULT 0
            );
            """,

            // ── StoreMovements ────────────────────────────────────────────────
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StoreMovements')
            CREATE TABLE StoreMovements (
                Id              uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
                StoreItemId     uniqueidentifier NOT NULL REFERENCES StoreItems(Id),
                ItemCode        nvarchar(20)     NOT NULL,
                ItemName        nvarchar(200)    NOT NULL,
                MovementType    nvarchar(20)     NOT NULL,
                QuantityBefore  float            NOT NULL,
                QuantityChange  float            NOT NULL,
                QuantityAfter   float            NOT NULL,
                Reference       nvarchar(50)     NULL,
                Notes           nvarchar(500)    NULL,
                MovedByEmail    nvarchar(150)    NOT NULL,
                MovedByName     nvarchar(100)    NOT NULL,
                CreatedAt       datetime2        NOT NULL DEFAULT GETUTCDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoreMovements_StoreItemId')
                CREATE INDEX IX_StoreMovements_StoreItemId ON StoreMovements (StoreItemId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoreMovements_CreatedAt')
                CREATE INDEX IX_StoreMovements_CreatedAt ON StoreMovements (CreatedAt);
            """,

            // ── DieselRequisitions ────────────────────────────────────────────
            """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DieselRequisitions')
            CREATE TABLE DieselRequisitions (
                Id                       uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
                RequisitionNumber        nvarchar(20)     NOT NULL,
                Purpose                  nvarchar(500)    NOT NULL,
                EquipmentType            nvarchar(30)     NOT NULL DEFAULT 'Generator',
                EquipmentReference       nvarchar(100)    NULL,
                Location                 nvarchar(150)    NOT NULL,
                QuantityRequestedLitres  float            NOT NULL,
                RequestedByEmail         nvarchar(150)    NOT NULL,
                RequestedByName          nvarchar(100)    NOT NULL,
                Department               nvarchar(100)    NOT NULL DEFAULT 'General Service',
                Status                   nvarchar(20)     NOT NULL DEFAULT 'Pending',
                ApprovedByEmail          nvarchar(150)    NULL,
                ApprovedByName           nvarchar(100)    NULL,
                ApprovedAt               datetime2        NULL,
                RejectedByEmail          nvarchar(150)    NULL,
                RejectionReason          nvarchar(500)    NULL,
                RejectedAt               datetime2        NULL,
                DispensedByEmail         nvarchar(150)    NULL,
                DispensedByName          nvarchar(100)    NULL,
                DispensedAt              datetime2        NULL,
                QuantityDispensedLitres  float            NULL,
                TankLevelBeforeLitres    float            NULL,
                TankLevelAfterLitres     float            NULL,
                UnitCostPerLitreNaira    decimal(18,4)    NULL,
                TotalCostNaira           decimal(18,2)    NULL,
                LinkedDieselRecordId     uniqueidentifier NULL,
                Notes                    nvarchar(1000)   NULL,
                CreatedAt                datetime2        NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt                datetime2        NOT NULL DEFAULT GETUTCDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DieselRequisitions_RequisitionNumber')
                CREATE UNIQUE INDEX IX_DieselRequisitions_RequisitionNumber ON DieselRequisitions (RequisitionNumber);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DieselRequisitions_Status')
                CREATE INDEX IX_DieselRequisitions_Status ON DieselRequisitions (Status);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DieselRequisitions_RequestedByEmail')
                CREATE INDEX IX_DieselRequisitions_RequestedByEmail ON DieselRequisitions (RequestedByEmail);
            """,
        };

        int applied = 0;
        foreach (var sql in statements)
        {
            await db.Database.ExecuteSqlRawAsync(sql);
            applied++;
        }

        log.LogInformation("✅ Schema upgrade: checked/applied {Count} column statements.", applied);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "❌ Schema upgrade failed — some new columns may be missing.");
    }
}
