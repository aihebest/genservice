-- =============================================================================
--  GenService Demo Database Seed Script
--  Runs automatically when the Demo environment starts.
--  Creates realistic sample data for the management presentation.
-- =============================================================================

-- Wait for the database to be ready, then seed
USE GenServiceDemo;
GO

-- ── Demo Users ───────────────────────────────────────────────────────────────
-- Passwords are hashed (BCrypt) for "DemoXXXX2026!"
-- The API's DevJwt auth mode accepts these users for login

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'manager@demo.local')
BEGIN
    INSERT INTO Users (Id, FullName, Email, Department, Role, IsActive, CreatedAt)
    VALUES
      (NEWID(), 'Bobby Tholath',       'manager@demo.local',    'General Service', 'DepartmentManager', 1, GETUTCDATE()),
      (NEWID(), 'Emeka Okonkwo',       'supervisor@demo.local', 'General Service', 'Supervisor',        1, GETUTCDATE()),
      (NEWID(), 'Chukwudi Nwosu',      'technician@demo.local', 'General Service', 'Technician',        1, GETUTCDATE()),
      (NEWID(), 'Bola Adeyemi',        'driver@demo.local',     'General Service', 'Driver',            1, GETUTCDATE()),
      (NEWID(), 'Fatima Al-Hassan',    'requester1@demo.local', 'Finance',         'Requester',         1, GETUTCDATE()),
      (NEWID(), 'Tunde Babatunde',     'requester2@demo.local', 'HR',              'Requester',         1, GETUTCDATE()),
      (NEWID(), 'Grace Obi',           'tech2@demo.local',      'General Service', 'Technician',        1, GETUTCDATE()),
      (NEWID(), 'Kwame Asante',        'driver2@demo.local',    'General Service', 'Driver',            1, GETUTCDATE());
    PRINT 'Demo users seeded.';
END
GO

-- ── Demo Vehicles ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Vehicles WHERE PlateNumber = 'LAG-001-GS')
BEGIN
    INSERT INTO Vehicles (Id, PlateNumber, Make, Model, Year, FuelType, SeatingCapacity, Status, CreatedAt)
    VALUES
      (NEWID(), 'LAG-001-GS', 'Toyota',   'Hilux',       2022, 'Diesel',  5, 'Available',   GETUTCDATE()),
      (NEWID(), 'LAG-002-GS', 'Toyota',   'Land Cruiser', 2021, 'Diesel', 7, 'InUse',       GETUTCDATE()),
      (NEWID(), 'LAG-003-GS', 'Ford',     'Transit',     2023, 'Diesel', 12, 'Available',   GETUTCDATE()),
      (NEWID(), 'LAG-004-GS', 'Honda',    'Accord',      2022, 'Petrol',  5, 'Maintenance', GETUTCDATE()),
      (NEWID(), 'ABJ-001-GS', 'Mitsubishi','Pajero',      2020, 'Diesel',  7, 'Available',   GETUTCDATE());
    PRINT 'Demo vehicles seeded.';
END
GO

-- ── Demo Requests (mix of statuses to show full workflow) ────────────────────
IF NOT EXISTS (SELECT 1 FROM Requests WHERE TrackingNumber = 'GS-2026-000001')
BEGIN
    DECLARE @ManagerId   UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'manager@demo.local');
    DECLARE @SuperId     UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'supervisor@demo.local');
    DECLARE @TechId      UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'technician@demo.local');
    DECLARE @Req1Id      UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'requester1@demo.local');
    DECLARE @Req2Id      UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'requester2@demo.local');

    INSERT INTO Requests (Id, TrackingNumber, Title, Description, Priority, Status, RequiresApproval, RequestedById, AssignedToId, CreatedAt)
    VALUES
      (NEWID(),'GS-2026-000001','AC Unit Fault - Block B Conference Room','Air conditioning unit is not cooling and making unusual noise',3,'InProgress',  0, @Req1Id, @TechId,  DATEADD(day,-3,GETUTCDATE())),
      (NEWID(),'GS-2026-000002','Generator Maintenance - Head Office',    'Quarterly preventive maintenance due for the 100KVA generator',2,'Completed',   0, @Req2Id, @TechId,  DATEADD(day,-7,GETUTCDATE())),
      (NEWID(),'GS-2026-000003','Diesel Requisition - 500 Litres',        'Monthly diesel purchase for generators at all sites',          3,'PendingApproval',1,@Req1Id, NULL,   DATEADD(day,-1,GETUTCDATE())),
      (NEWID(),'GS-2026-000004','Weekend Access Request - Finance Team',  'Finance team requires Saturday access for month-end closing',   2,'Approved',    1, @Req2Id, @SuperId, DATEADD(day,-2,GETUTCDATE())),
      (NEWID(),'GS-2026-000005','Water Tank Washing - Block C',           'Quarterly tank washing overdue by 2 days',                     3,'Assigned',    0, @Req1Id, @TechId,  DATEADD(day,-5,GETUTCDATE())),
      (NEWID(),'GS-2026-000006','Faulty Light Fixtures - Floor 3',        'Multiple fluorescent tubes need replacement on 3rd floor',     1,'Submitted',   0, @Req2Id, NULL,     GETUTCDATE()),
      (NEWID(),'GS-2026-000007','Accommodation Request - Visiting Auditor','Visiting audit team requires accommodation for 3 nights',      2,'PendingApproval',1,@Req1Id,NULL,  DATEADD(hour,-4,GETUTCDATE())),
      (NEWID(),'GS-2026-000008','Store Item Request - Safety Equipment',  'PPE stock replenishment: gloves, helmets, safety boots',       2,'Approved',    1, @Req2Id, @SuperId, DATEADD(day,-4,GETUTCDATE()));
    PRINT 'Demo requests seeded.';
END
GO

-- ── Demo Staff Activities ─────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM StaffActivities WHERE Description LIKE '%Generator maintenance%')
BEGIN
    DECLARE @TechId2  UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'technician@demo.local');
    DECLARE @Tech2Id  UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'tech2@demo.local');
    DECLARE @DriverId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'driver@demo.local');

    INSERT INTO StaffActivities (Id, UserId, ActivityType, Description, Location, Status, StartTime, EndTime, CreatedAt)
    VALUES
      (NEWID(), @TechId2,  'Maintenance',  'Generator maintenance at Head Office — 100KVA unit quarterly service', 'Head Office — Generator Room', 'Active',    DATEADD(hour,-2,GETUTCDATE()), NULL,                       GETUTCDATE()),
      (NEWID(), @Tech2Id,  'Repair',       'AC repair in Conference Room B — replacing compressor unit',           'Block B — Conference Room B',  'Active',    DATEADD(hour,-1,GETUTCDATE()), NULL,                       GETUTCDATE()),
      (NEWID(), @DriverId, 'VehicleTrip',  'Transporting maintenance team to Lekki branch for site inspection',   'En Route — Lekki Branch',      'Active',    DATEADD(hour,-3,GETUTCDATE()), NULL,                       GETUTCDATE()),
      (NEWID(), @TechId2,  'Inspection',   'Electrical inspection — Block A office floors 1 and 2',               'Block A',                      'Completed', DATEADD(hour,-5,GETUTCDATE()), DATEADD(hour,-2,GETUTCDATE()),GETUTCDATE());
    PRINT 'Demo staff activities seeded.';
END
GO

-- ── Demo Fuel Logs ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM FuelLogs WHERE Notes = 'DEMO_SEED')
BEGIN
    DECLARE @Vehicle1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Vehicles WHERE PlateNumber = 'LAG-001-GS');
    DECLARE @Vehicle2Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Vehicles WHERE PlateNumber = 'LAG-002-GS');
    DECLARE @DriverSeedId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'driver@demo.local');

    INSERT INTO FuelLogs (Id, VehicleId, LoggedById, FuelType, LitresPurchased, CostPerLitre, TotalCost, OdometerReading, PurchaseDate, Notes, CreatedAt)
    VALUES
      (NEWID(), @Vehicle1Id, @DriverSeedId, 'Diesel', 80.00,  850.00, 68000.00, 42500, DATEADD(day,-2, GETUTCDATE()), 'DEMO_SEED', GETUTCDATE()),
      (NEWID(), @Vehicle2Id, @DriverSeedId, 'Diesel', 120.00, 850.00, 102000.00,78200, DATEADD(day,-5, GETUTCDATE()), 'DEMO_SEED', GETUTCDATE()),
      (NEWID(), @Vehicle1Id, @DriverSeedId, 'Diesel', 75.00,  850.00, 63750.00, 41200, DATEADD(day,-9, GETUTCDATE()), 'DEMO_SEED', GETUTCDATE()),
      (NEWID(), @Vehicle2Id, @DriverSeedId, 'Diesel', 100.00, 850.00, 85000.00, 76500, DATEADD(day,-12,GETUTCDATE()), 'DEMO_SEED', GETUTCDATE());
    PRINT 'Demo fuel logs seeded.';
END
GO

-- ── Demo Maintenance Schedules ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM MaintenanceSchedules WHERE MaintenanceType = 'Quarterly Fumigation')
BEGIN
    INSERT INTO MaintenanceSchedules (Id, EntityType, EntityId, MaintenanceType, FrequencyDays, LastDoneDate, NextDueDate, ReminderDaysBefore, EscalateAfterDays, IsActive, CreatedAt)
    VALUES
      (NEWID(), 'Building', NEWID(), 'Quarterly Fumigation',     90, DATEADD(day,-88,GETDATE()), DATEADD(day, 2,GETDATE()), 7, 3, 1, GETUTCDATE()),
      (NEWID(), 'Building', NEWID(), 'Waste Disposal',            7,  DATEADD(day, -6,GETDATE()), DATEADD(day, 1,GETDATE()), 1, 1, 1, GETUTCDATE()),
      (NEWID(), 'Vehicle',  NEWID(), 'Oil Change — LAG-001-GS',  90, DATEADD(day,-95,GETDATE()), DATEADD(day,-5,GETDATE()), 7, 3, 1, GETUTCDATE()),  -- OVERDUE
      (NEWID(), 'Equipment',NEWID(), 'Generator Servicing',       30, DATEADD(day,-28,GETDATE()), DATEADD(day, 2,GETDATE()), 5, 2, 1, GETUTCDATE()),
      (NEWID(), 'Building', NEWID(), 'Water Tank Washing',        90, DATEADD(day,-92,GETDATE()), DATEADD(day,-2,GETDATE()), 7, 3, 1, GETUTCDATE());  -- OVERDUE
    PRINT 'Demo maintenance schedules seeded.';
END
GO

PRINT 'All demo seed data loaded successfully.';
GO
