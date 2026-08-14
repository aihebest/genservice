// =============================================================================
//  Shared TypeScript types for the GenService platform
// =============================================================================

// ── Auth ──────────────────────────────────────────────────────────────────────

export type UserRole =
  | 'SystemAdmin'
  | 'DepartmentManager'
  | 'Supervisor'
  | 'Technician'
  | 'Driver'
  | 'Requester'
  | 'StoreOfficer';

export interface AuthUser {
  email:      string;
  fullName:   string;
  role:       UserRole;
  department: string;
  expiresAt:  string; // ISO string
}

export interface LoginRequest  { email: string; password: string; }
export interface LoginResponse { token: string; user: AuthUser; }
export interface ApiError      { message: string; errors?: Record<string, string[]>; }

// ── Requests ──────────────────────────────────────────────────────────────────

export type RequestCategory =
  | 'Maintenance'
  | 'FaultyAsset'
  | 'FacilityComplaint'
  | 'OperationalSupport'
  | 'Accommodation'
  | 'AssetDamage'
  | 'WeekendAccess'
  | 'AfterHoursWork'
  | 'StoreItems'
  | 'Diesel'
  | 'Other';

export type RequestStatus =
  | 'Open'
  | 'PendingLineManager'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'InProgress'
  | 'MaterialAwaited'
  | 'AwaitingFunds'
  | 'Completed'
  | 'Cancelled'
  | 'Reassigned';

export type RequestPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export interface ServiceRequest {
  id:               string;
  ticketNumber:     string;
  title:            string;
  description:      string;
  category:         RequestCategory;
  requiresApproval: boolean;
  status:           RequestStatus;
  priority:         RequestPriority;
  location:         string;
  requestedByEmail: string;
  requestedByName:  string;
  assignedToEmail?: string;
  assignedToName?:       string;
  lineManagerEmail?:     string;
  lineManagerName?:      string;
  lineManagerApprovedAt?:string;
  approvedByEmail?:      string;
  approvedByName?:       string;
  createdAt:             string;
  updatedAt:        string;
  approvedAt?:      string;
  completedAt?:     string;
  rejectionReason?:  string;
  notes?:            string;
  // Reassignment
  reassignedToType?: string;
  reassignedToName?: string;
  reassignedNotes?:  string;
  reassignedAt?:     string;
}

export interface CreateRequestDto {
  title:             string;
  description:       string;
  category:          RequestCategory;
  priority:          RequestPriority;
  location:          string;
  lineManagerEmail?: string;  // required when category requires approval
  lineManagerName?:  string;
}

export interface RequestListResponse {
  items:      ServiceRequest[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface RequestStats {
  total:           number;
  open:            number;
  pendingApproval: number;
  approved:        number;
  inProgress:      number;
  materialAwaited: number;
  completed:       number;
  rejected:        number;
  reassigned:      number;
}

// ── Office locations ───────────────────────────────────────────────────────────
// ── Audit Trail ───────────────────────────────────────────────────────────────

export interface AuditEntry {
  id:               string;
  entityType:       string;
  entityId:         string;
  refNumber:        string;
  action:           string;
  oldValue?:        string;
  newValue?:        string;
  details?:         string;
  performedByEmail: string;
  performedByName:  string;
  timestamp:        string;
}

export const AUDIT_ACTION_META: Record<string, { label: string; color: string }> = {
  Created:              { label: 'Created',                color: 'blue'    },
  StatusChanged:        { label: 'Status Changed',         color: 'gold'    },
  Approved:             { label: 'GS Approved',            color: 'green'   },
  Rejected:             { label: 'Rejected',               color: 'red'     },
  LineManagerApproved:  { label: 'Line Manager Approved',  color: 'cyan'    },
  LineManagerRejected:  { label: 'Line Manager Rejected',  color: 'orange'  },
  Assigned:             { label: 'Assigned',               color: 'purple'  },
  Reassigned:           { label: 'Reassigned',             color: 'magenta' },
  Completed:            { label: 'Completed',              color: 'green'   },
  Cancelled:            { label: 'Cancelled',              color: 'default' },
  Dispatched:           { label: 'Dispatched to Workshop', color: 'blue'    },
  ProgressLogged:       { label: 'Progress Logged',        color: 'lime'    },
};

// ── Diesel Tank Readings ───────────────────────────────────────────────────────

export interface DieselTankReading {
  id:                        string;
  location:                  string;
  tankIdentifier:            string;
  readingDate:               string;
  tankLevelLitres:           number;
  previousLevelLitres?:      number;
  consumptionLitres?:        number;
  costPerLitreNaira?:        number;
  totalConsumptionCostNaira?:number;
  notes?:                    string;
  loggedByEmail:             string;
  loggedByName:              string;
  createdAt:                 string;
}

// ── Generator Monitoring ──────────────────────────────────────────────────────

export type GeneratorDailyStatus = 'Running' | 'Standby' | 'UnderMaintenance' | 'Fault';

export const GENERATOR_DAILY_STATUS_META: Record<GeneratorDailyStatus, { label: string; color: string; badge: string }> = {
  Running:          { label: 'Running',           color: 'green',   badge: 'processing' },
  Standby:          { label: 'Standby',           color: 'blue',    badge: 'default'    },
  UnderMaintenance: { label: 'Under Maintenance', color: 'orange',  badge: 'warning'    },
  Fault:            { label: 'Fault',             color: 'red',     badge: 'error'      },
};

export interface GeneratorDailyReading {
  id:                     string;
  assetNo:                string;
  assetDescription:       string;
  location:               string;
  readingDate:            string;
  previousEngineReading:  number;
  currentEngineReading:   number;
  cumulativeRunHours:     number;
  runHoursToday:          number;
  generatorStatus:        GeneratorDailyStatus;
  previousFuelLevelLitres:number;
  fuelLevelLitres:        number;
  fuelAddedLitres?:       number;
  fuelRemovedLitres?:     number;
  fuelConsumedLitres?:    number;
  previousUtilityReading?:number;
  currentUtilityReading?: number;
  utilityAvailableHours?: number;
  previousGeneratorKw?:   number;
  currentGeneratorKw?:    number;
  generatorKwConsumed?:   number;
  serviceIntervalHours:   number;
  remainingServiceHours:  number;
  serviceCompleted:       boolean;
  lastServicedAtHours?:   number;
  serviceAlertActive:     boolean;
  hoursUntilNextService:  number;
  notes?:                 string;
  loggedByEmail:          string;
  loggedByName:           string;
  createdAt:              string;
}

export interface GeneratorSummary {
  location:             string;
  assetNo:              string;
  assetDescription:     string;
  latestCumulativeHours:number;
  hoursUntilNextService: number;
  serviceAlertActive:   boolean;
  latestFuelLevel:      number;
  latestStatus:         GeneratorDailyStatus;
  latestReadingDate:    string;
}

export interface PowerMeterReading {
  id:                           string;
  location:                     string;
  meterNumber:                  string;
  readingDate:                  string;
  previousMeterReading:         number;
  currentMeterReading:          number;
  meterReadingKwh:              number;
  unitsConsumedToday?:          number;
  utilityAvailableHours?:       number;
  costPerKwhNaira?:             number;
  totalElectricityCostNaira?:   number;
  notes?:                       string;
  loggedByEmail:                string;
  loggedByName:                 string;
  createdAt:                    string;
}

// ── Task Progress Logs ────────────────────────────────────────────────────────

export type ProgressStatus =
  | 'WorkCompleted'
  | 'WorkInProgress'
  | 'AwaitingMaterials'
  | 'AwaitingVendor'
  | 'AwaitingApproval'
  | 'PendingProcurement';

export const PROGRESS_STATUS_META: Record<ProgressStatus, { label: string; color: string }> = {
  WorkCompleted:      { label: 'Work Completed',       color: 'green'    },
  WorkInProgress:     { label: 'Work In Progress',     color: 'blue'     },
  AwaitingMaterials:  { label: 'Awaiting Materials',   color: 'gold'     },
  AwaitingVendor:     { label: 'Awaiting Vendor',      color: 'orange'   },
  AwaitingApproval:   { label: 'Awaiting Approval',    color: 'purple'   },
  PendingProcurement: { label: 'Pending Procurement',  color: 'volcano'  },
};

export interface TaskProgressLog {
  id:                string;
  module:            string;
  entityId:          string;
  refNumber:         string;
  taskTitle:         string;
  logDate:           string;
  activityPerformed: string;
  progressStatus:    ProgressStatus;
  materialsRequired?:string;
  nextAction?:       string;
  loggedByEmail:     string;
  loggedByName:      string;
  isProxy:           boolean;
  proxyForName?:     string;
  createdAt:         string;
}

export interface TechnicianSummary {
  email:            string;
  name:             string;
  totalAssigned:    number;
  inProgress:       number;
  completed:        number;
  pending:          number;
  todayLogs:        number;
  weekLogs:         number;
  monthLogs:        number;
  awaitingMaterials:number;
  awaitingVendor:   number;
}

// ── Notifications ──────────────────────────────────────────────────────────────
export interface AppNotification {
  id:         string;
  title:      string;
  message:    string;
  type:       string;
  module:     string;
  entityId?:  string;
  refNumber?: string;
  isRead:     boolean;
  createdAt:  string;
}

export interface NotificationsResponse {
  items:       AppNotification[];
  unreadCount: number;
  total:       number;
  page:        number;
  take:        number;
}

export const OFFICE_LOCATIONS = [
  // Offices
  'Lagos Office',
  'Port Harcourt Office',
  'Abuja Office',
  // Sites & Locations
  'Bonny',
  'DR',
  'DGS',
  'GML',
  'DMGS',
  'Woji',
  'Woji Logistic Tank',
  'Uyo',
  'Warri',
  // Residences
  'AK Lagos',
  'AK Uyo',
  'Tomy Residence',
  'Chairman Uyo',
  'Lagos Guest House',
  'AK Guest House Lagos',
  // Other
  'Other',
] as const;

export type OfficeLocation = typeof OFFICE_LOCATIONS[number];

// ── Reassignment types ─────────────────────────────────────────────────────────
export const REASSIGN_TYPES = [
  { value: 'Logistics',   label: 'Logistics'          },
  { value: 'Vendor',      label: 'Vendor / Outsource' },
  { value: 'Procurement', label: 'Procurement'        },
  { value: 'Internal',    label: 'Internal Transfer'  },
] as const;

// ── Category metadata (display labels + approval flag) ────────────────────────
export interface CategoryMeta {
  label:           string;
  requiresApproval: boolean;
  color:           string;
}

export const CATEGORY_META: Record<RequestCategory, CategoryMeta> = {
  Maintenance:        { label: 'Maintenance',         requiresApproval: false, color: 'blue'    },
  FaultyAsset:        { label: 'Faulty Asset',         requiresApproval: false, color: 'orange'  },
  FacilityComplaint:  { label: 'Facility Complaint',   requiresApproval: false, color: 'gold'    },
  OperationalSupport: { label: 'Operational Support',  requiresApproval: false, color: 'cyan'    },
  Accommodation:      { label: 'Accommodation',        requiresApproval: true,  color: 'purple'  },
  AssetDamage:        { label: 'Asset Damage',         requiresApproval: true,  color: 'red'     },
  WeekendAccess:      { label: 'Weekend Access',       requiresApproval: true,  color: 'volcano' },
  AfterHoursWork:     { label: 'After-Hours Work',     requiresApproval: true,  color: 'magenta' },
  StoreItems:         { label: 'Store Items',          requiresApproval: true,  color: 'lime'    },
  Diesel:             { label: 'Diesel Request',       requiresApproval: true,  color: 'geekblue'},
  Other:              { label: 'Other',                requiresApproval: true,  color: 'default' },
};

export const STATUS_META: Record<RequestStatus, { label: string; color: string }> = {
  Open:               { label: 'Open',                   color: 'default'    },
  PendingLineManager: { label: 'Pending Line Manager',   color: 'warning'    },
  PendingApproval:    { label: 'Pending GS Approval',    color: 'processing' },
  Approved:        { label: 'Approved',         color: 'success'    },
  Rejected:        { label: 'Rejected',         color: 'error'      },
  InProgress:      { label: 'In Progress',      color: 'processing' },
  MaterialAwaited: { label: 'Awaiting Spares',  color: 'gold'       },
  AwaitingFunds:   { label: 'Awaiting Funds',   color: 'orange'     },
  Completed:       { label: 'Completed',        color: 'success'    },
  Cancelled:       { label: 'Cancelled',        color: 'default'    },
  Reassigned:      { label: 'Reassigned',       color: 'purple'     },
};

export const PRIORITY_META: Record<RequestPriority, { label: string; color: string }> = {
  Low:    { label: 'Low',    color: 'default' },
  Normal: { label: 'Normal', color: 'blue'    },
  High:   { label: 'High',   color: 'orange'  },
  Urgent: { label: 'Urgent', color: 'red'     },
};

// ── Staff Activities ───────────────────────────────────────────────────────────

export type ActivityStatus   = 'Active' | 'Paused' | 'Completed';
export type ActivityCategory =
  | 'Maintenance' | 'Repair' | 'Inspection' | 'Cleaning'
  | 'Delivery' | 'Installation' | 'GeneratorWork'
  | 'Plumbing' | 'Electrical' | 'General';

export interface StaffActivity {
  id:                  string;
  staffEmail:          string;
  staffName:           string;
  activityDescription: string;
  location:            string;
  category:            ActivityCategory;
  status:              ActivityStatus;
  isProxy:             boolean;
  loggedByEmail:       string;
  loggedByName:        string;
  notes?:              string;
  startedAt:           string;
  updatedAt:           string;
  completedAt?:        string;
}

export interface ActivityListResponse {
  items:      StaffActivity[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface LogActivityRequest {
  staffEmail:          string;
  staffName:           string;
  activityDescription: string;
  category:            ActivityCategory;
  location?:           string;
  notes?:              string;
}

export const ACTIVITY_STATUS_META: Record<ActivityStatus, { label: string; color: string; badge: string }> = {
  Active:    { label: 'Active',    color: 'green',   badge: 'processing' },
  Paused:    { label: 'Paused',    color: 'orange',  badge: 'warning'    },
  Completed: { label: 'Completed', color: 'default', badge: 'default'    },
};

export const ACTIVITY_CATEGORY_META: Record<ActivityCategory, { label: string; color: string }> = {
  Maintenance:   { label: 'Maintenance',    color: 'blue'     },
  Repair:        { label: 'Repair',         color: 'orange'   },
  Inspection:    { label: 'Inspection',     color: 'cyan'     },
  Cleaning:      { label: 'Cleaning',       color: 'lime'     },
  Delivery:      { label: 'Delivery',       color: 'purple'   },
  Installation:  { label: 'Installation',   color: 'geekblue' },
  GeneratorWork: { label: 'Generator Work', color: 'volcano'  },
  Plumbing:      { label: 'Plumbing',       color: 'gold'     },
  Electrical:    { label: 'Electrical',     color: 'yellow'   },
  General:       { label: 'General',        color: 'default'  },
};

// ── Maintenance Schedules ──────────────────────────────────────────────────────

export type MaintenanceCategory =
  // Equipment
  | 'GeneratorService' | 'HVAC' | 'UPS' | 'Pumps'
  // Vehicle
  | 'VehicleServicing' | 'VehicleInspection'
  // Facility
  | 'Electrical' | 'Plumbing' | 'CivilWorks' | 'FireSafety'
  | 'Fumigation' | 'WasteDisposal' | 'TankWashing' | 'General';

export interface MaintenanceSchedule {
  id:                   string;
  taskName:             string;
  description:          string;
  category:             MaintenanceCategory;
  location:             string;
  frequencyLabel:       string;
  frequencyDays:        number;
  nextDueAt:            string;
  lastCompletedAt?:     string;
  isOverdue:            boolean;
  assignedToEmail?:     string;
  assignedToName?:      string;
  lastCompletedByEmail?: string;
  lastCompletedByName?:  string;
  lastCompletionNotes?:  string;
  isActive:             boolean;
  createdAt:            string;
  updatedAt:            string;
  // Reminder / escalation tracking
  escalationLevel:      number;   // 0 = none, 1 = supervisor, 2 = manager
  lastReminderSentAt?:  string;
  lastEscalationSentAt?: string;
}

export interface ScheduleListResponse {
  items:        MaintenanceSchedule[];
  totalCount:   number;
  overdueCount: number;
  page:         number;
  pageSize:     number;
}

export interface MaintenanceStats {
  total:     number;
  overdue:   number;
  dueSoon:   number;
  completed: number;
  active:    number;
}

export interface CreateScheduleRequest {
  taskName:        string;
  description?:    string;
  category:        MaintenanceCategory;
  location?:       string;
  frequencyLabel:  string;
  frequencyDays:   number;
  nextDueAt:       string;
  assignedToEmail?: string;
  assignedToName?:  string;
}

export const MAINTENANCE_CATEGORY_META: Record<MaintenanceCategory, { label: string; color: string; group: 'Equipment' | 'Vehicle' | 'Facility' }> = {
  // Equipment
  GeneratorService:  { label: 'Generator Service', color: 'volcano', group: 'Equipment' },
  HVAC:              { label: 'Air Conditioning',  color: 'blue',    group: 'Equipment' },
  UPS:               { label: 'UPS Systems',       color: 'geekblue',group: 'Equipment' },
  Pumps:             { label: 'Pumps',             color: 'cyan',    group: 'Equipment' },
  // Vehicle
  VehicleServicing:  { label: 'Vehicle Servicing', color: 'purple',  group: 'Vehicle'   },
  VehicleInspection: { label: 'Vehicle Inspection',color: 'magenta', group: 'Vehicle'   },
  // Facility
  Electrical:        { label: 'Electrical Works',  color: 'gold',    group: 'Facility'  },
  Plumbing:          { label: 'Plumbing',          color: 'lime',    group: 'Facility'  },
  CivilWorks:        { label: 'Civil Works',       color: 'brown' as any, group: 'Facility' },
  FireSafety:        { label: 'Fire Safety',       color: 'red',     group: 'Facility'  },
  Fumigation:        { label: 'Fumigation',        color: 'purple',  group: 'Facility'  },
  WasteDisposal:     { label: 'Waste Disposal',    color: 'green',   group: 'Facility'  },
  TankWashing:       { label: 'Tank Washing',      color: 'cyan',    group: 'Facility'  },
  General:           { label: 'General',           color: 'default', group: 'Facility'  },
};

export const MAINTENANCE_GROUPS = ['Equipment', 'Vehicle', 'Facility'] as const;
export type MaintenanceGroup = typeof MAINTENANCE_GROUPS[number];

// ── Fuel & Power ───────────────────────────────────────────────────────────────

export type GeneratorLogStatus = 'Running' | 'Stopped';
export type GeneratorRunReason = 'PowerOutage' | 'ScheduledTest' | 'Maintenance' | 'LoadShedding' | 'Other';
export type DieselRecordType   = 'Purchase' | 'Dispensed' | 'Transfer';

export interface GeneratorLog {
  id:              string;
  location:        string;
  startTime:       string;
  endTime?:        string;
  runtimeHours?:   number;
  fuelLevelBefore?: number;
  fuelLevelAfter?:  number;
  fuelConsumed?:    number;
  runReason:       GeneratorRunReason;
  status:          GeneratorLogStatus;
  outageCause?:    string;
  notes?:          string;
  loggedByEmail:   string;
  loggedByName:    string;
  createdAt:       string;
}

export interface GeneratorLogListResponse {
  items:      GeneratorLog[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface GeneratorStats {
  totalRuntimeHoursThisMonth:  number;
  totalFuelConsumedThisMonth:  number;
  outagesThisMonth:            number;
  currentlyRunning:            number;
  totalRuntimeHoursAllTime:    number;
}

export interface DieselRecord {
  id:               string;
  recordDate:       string;
  recordType:       DieselRecordType;
  quantityLitres:   number;
  unitCostNaira:    number;
  totalCostNaira:   number;
  supplier?:        string;
  destination?:     string;
  requestedByEmail: string;
  requestedByName:  string;
  approvedByEmail?: string;
  approvedByName?:  string;
  approvedAt?:      string;
  notes?:           string;
  createdAt:        string;
}

export interface DieselRecordListResponse {
  items:      DieselRecord[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface DieselStats {
  totalPurchasedLitresThisMonth: number;
  totalDispensedLitresThisMonth: number;
  totalSpendThisMonth:           number;
  currentStockLitres:            number;
  totalPurchasedLitresAllTime:   number;
}

export interface FuelPowerSummary {
  generator: GeneratorStats;
  diesel:    DieselStats;
}

export const GENERATOR_RUN_REASON_META: Record<GeneratorRunReason, { label: string; color: string }> = {
  PowerOutage:   { label: 'Power Outage',    color: 'red'      },
  ScheduledTest: { label: 'Scheduled Test',  color: 'blue'     },
  Maintenance:   { label: 'Maintenance',     color: 'orange'   },
  LoadShedding:  { label: 'Load Shedding',   color: 'volcano'  },
  Other:         { label: 'Other',           color: 'default'  },
};

export const DIESEL_RECORD_TYPE_META: Record<DieselRecordType, { label: string; color: string }> = {
  Purchase:  { label: 'Purchase',             color: 'green'   },
  Dispensed: { label: 'Internal Consumption', color: 'blue'    },
  Transfer:  { label: 'Transfer',             color: 'purple'  },
};

// ── Vehicle Maintenance ────────────────────────────────────────────────────────

/** Complete Desicon Group vehicle fleet (from MRSF Vehicle Asset Register) */
export const VEHICLE_LIST: Array<{ regNo: string; description: string }> = [
  { regNo: 'PF 4079 SPY', description: 'PRADO' },
  { regNo: 'PHC 185 AM', description: 'NISSAN PICKUP' },
  { regNo: 'PHC 178 AM', description: 'NISSAN NAVARA' },
  { regNo: 'KRD 341 CE', description: 'PRADO' },
  { regNo: 'KRD 339 CE', description: 'PRADO' },
  { regNo: 'KRD 338 CE', description: 'PRADO' },
  { regNo: 'PHC 177 AM', description: 'TOYOTA PRADO' },
  { regNo: 'GGU 693 TX', description: 'TOYOTA HILUX' },
  { regNo: 'GGU 692 TX', description: 'TOYOTA HILUX' },
  { regNo: 'PHC 179 AM', description: 'TOYOTA HILUX' },
  { regNo: 'GGU 695 TX', description: 'TOYOTA HILUX' },
  { regNo: 'GGU 696 TX', description: 'TOYOTA HILUX' },
  { regNo: 'CX 211 RBC', description: 'TOYOTA CAMRY' },
  { regNo: 'GGE 70 AY', description: 'LANDCRUISER' },
  { regNo: 'LSD 65 BP', description: 'HYUNDAI CAR' },
  { regNo: 'AGL 105 BU', description: 'TOYOTA HIACE BUS' },
  { regNo: 'SMK 126 CE', description: 'LAND CRUISER' },
  { regNo: 'KRD 407 CG / PF 2414 SPY', description: 'TOYOTA HILUX' },
  { regNo: 'KRD 406 CG / PF 2415 SPY', description: 'TOYOTA HILUX' },
  { regNo: 'KTU 905 CK', description: 'TOYOTA HIACE BUS' },
  { regNo: 'JJJ 599 CM', description: 'LEXUS LX 570 SUV' },
  { regNo: 'JJJ 597 CM', description: 'LEXUS LX 570 SUV' },
  { regNo: 'AKD 90 CP', description: 'TOYOTA PRADO VX' },
  { regNo: 'SMK 179 CW', description: 'TOYOTA CAMRY XLE' },
  { regNo: 'APP 785 CU / PF 253 SPY', description: 'LEXUS LX570, B6 ARMOURED' },
  { regNo: 'FKJ 877 DB', description: 'TOYOTA LANDCRUISER' },
  { regNo: 'FKJ 876 DB', description: 'TOYOTA LANDCRUISER' },
  { regNo: 'LSR 261 CX', description: 'TOYOTA PRADO VX' },
  { regNo: 'RUM 513 CB', description: 'TOYOTA LAND CRUISER PRADO TXL' },
  { regNo: 'SMK 51 DM', description: 'HYUNDAI IX 35 ELEGANCE' },
  { regNo: 'SMK 48 DM', description: 'HYUNDAI SANTA FE IX 45' },
  { regNo: 'FKJ 862 DJ', description: 'HYUNDAI IX 35 ELEGANCE' },
  { regNo: 'FKJ 988 DJ', description: 'HYUNDAI IX 35 ELEGANCE' },
  { regNo: 'GGE 223 DK', description: 'HYUNDAI IX 35 ELEGANCE' },
  { regNo: 'SMK 50 DM', description: 'HYUNDAI SANTA FE IX 45 ELEGANCE' },
  { regNo: 'APP 40 DQ', description: 'KIA SPORTAGE BLACK PEARL' },
  { regNo: 'APP 42 DQ', description: 'KIA CERATO' },
  { regNo: 'APP 41 DQ', description: 'LEXUS GX' },
  { regNo: 'APP 43 DQ', description: 'LEXUS GX 470 JEEP' },
  { regNo: 'PHC 896 FW', description: 'NISSAN NAVARA PICKUP' },
  { regNo: 'PHC 895 FW', description: 'NISSAN NAVARA PICKUP' },
  { regNo: 'PHC 118 NT', description: 'NISSAN PICKUP NP300' },
  { regNo: 'LND 583 ET', description: 'TOYOTA FORTUNER' },
  { regNo: 'APP 388 ET', description: 'TOYOTA FORTUNER' },
  { regNo: 'NCH 53 ST', description: 'MITSUBISHI OUTLANDER' },
  { regNo: 'NCH 52 ST', description: 'MITSUBISHI OUTLANDER' },
  { regNo: 'AGL 272 EU', description: 'TOYOTA HILUX PICKUP' },
  { regNo: 'AGL 273 EU', description: 'TOYOTA HILUX PICKUP' },
  { regNo: 'AGL 274 EU', description: 'TOYOTA HILUX PICKUP' },
  { regNo: 'AGL 275 EU', description: 'TOYOTA HILUX PICKUP' },
  { regNo: 'PHC 82 AJ', description: 'TOYOTA HILUX PICKUP' },
  { regNo: 'EPE-776-EW', description: 'NISSAN URVAN 15 SEATER BUS' },
  { regNo: 'KTU 570 EU', description: 'HYUNDAI TUCSON EVOLUTION' },
  { regNo: 'KSF 375 EY', description: 'NISSAN PICKUP NP300' },
  { regNo: 'KSF 376 EY', description: 'NISSAN PICKUP NP300' },
  { regNo: 'NCH 54 ST', description: 'FOTON VIEW C1 MHR 15-SEATER BUS' },
  { regNo: 'BER 500 MU', description: 'HYUNDAI MINI TRUCK' },
  { regNo: 'BDG 285 FA', description: 'TOYOTA RAV4' },
  { regNo: 'BDG 286 FA', description: 'TOYOTA RAV4' },
  { regNo: 'LSR 74 FC', description: 'TOYOTA LAND CRUISER' },
  { regNo: 'YAB 47 NQ', description: 'TOYOTA LANDCRUISER' },
  { regNo: 'APP 231 FQ', description: 'LEXUS GX470 JEEP' },
  { regNo: 'SKN 317 AU', description: 'TOYOTA HIACE STANDARD ROOF AMBULANCE' },
  { regNo: 'BDG 159 CL', description: 'TOYOTA CAMRY' },
  { regNo: 'AKD 407 BD', description: 'TOYOTA PRADO' },
  { regNo: 'AJ 464 AJ', description: 'TOYOTA PRADO' },
  { regNo: 'AAA 07 UB', description: 'RANGE ROVER' },
  { regNo: 'FST 06 AZ', description: 'TOYOTA PRADO' },
  { regNo: 'AKD 490 SU', description: 'TOYOTA PRADO' },
  { regNo: 'KSF 761 DV', description: 'LEXUS' },
  { regNo: 'DE 51 CON', description: 'MERCEDES G-WAGON' },
  { regNo: 'RBC 899 HN', description: 'TOYOTA HILUX' },
  { regNo: 'EPE 520 CH', description: 'TOYOTA PRADO' },
  { regNo: 'AKD 380 EJ', description: 'WRANGLER JEEP' },
  { regNo: '47', description: 'BMW 6 SERIES' },
  { regNo: 'DBU 519 AA', description: 'TOYOTA HILUX' },
  { regNo: 'DBU 710 AA', description: 'TOYOTA HIACE BUS (DIESEL ENGINE)' },
  { regNo: 'AFM 673 GL', description: 'TOYOTA FORTUNER' },
  { regNo: 'AFM 301 GL', description: 'TOYOTA URBAN CRUISER' },
  { regNo: 'AFM 674 GL', description: 'TOYOTA URBAN CRUISER' },
  { regNo: 'AFM 302 GL', description: 'TOYOTA HIACE BUS (DIESEL ENGINE)' },
  { regNo: 'AFM 304 GL', description: 'TOYOTA HILUX' },
  { regNo: 'AFM 303 GL', description: 'TOYOTA HILUX' },
  { regNo: 'AFM 675 GL', description: 'TOYOTA HILUX' },
];

/** Asset / fleet number for each vehicle registration (from the fleet asset register). */
export const VEHICLE_ASSET_NO: Record<string, string> = {
  'PF 4079 SPY': '5550000190', 'PHC 185 AM': '5550000223', 'PHC 178 AM': '5550000224',
  'KRD 341 CE': '5550000280', 'KRD 339 CE': '5550000281', 'KRD 338 CE': '5550000346',
  'PHC 177 AM': '5550000384', 'GGU 693 TX': '5550000398', 'GGU 692 TX': '5550000399',
  'PHC 179 AM': '5550000618', 'GGU 695 TX': '5550000619', 'GGU 696 TX': '5550000620',
  'CX 211 RBC': '5550000641', 'GGE 70 AY': '5550000735', 'LSD 65 BP': '5550000906',
  'AGL 105 BU': '5550000912', 'SMK 126 CE': '5550000950', 'KRD 407 CG / PF 2414 SPY': '5550000956',
  'KRD 406 CG / PF 2415 SPY': '5550000957', 'KTU 905 CK': '5550001005', 'JJJ 599 CM': '5550001008',
  'JJJ 597 CM': '5550001010', 'AKD 90 CP': '5550001231', 'SMK 179 CW': '5550001258',
  'APP 785 CU / PF 253 SPY': '5550001280', 'FKJ 877 DB': '5550001331', 'FKJ 876 DB': '5550001332',
  'LSR 261 CX': '5550001368', 'RUM 513 CB': '5550001528', 'SMK 51 DM': '5550001616',
  'SMK 48 DM': '5550001617', 'FKJ 862 DJ': '5550001620', 'FKJ 988 DJ': '5550001621',
  'GGE 223 DK': '5550001639', 'SMK 50 DM': '5550001817', 'APP 40 DQ': '5550001910',
  'APP 42 DQ': '5550001911', 'APP 41 DQ': '5550001912', 'APP 43 DQ': '5550001913',
  'PHC 896 FW': '5550002023', 'PHC 895 FW': '5550002024', 'PHC 118 NT': '5550002132',
  'LND 583 ET': '5550002641', 'APP 388 ET': '5550002654', 'NCH 53 ST': '5550002662',
  'NCH 52 ST': '5550002663', 'AGL 272 EU': '5550002664', 'AGL 273 EU': '5550002665',
  'AGL 274 EU': '5550002666', 'AGL 275 EU': '5550002667', 'PHC 82 AJ': '5550002686',
  'EPE-776-EW': '5550002888', 'KTU 570 EU': '5550002902', 'KSF 375 EY': '5550002911',
  'KSF 376 EY': '5550002912', 'NCH 54 ST': '5550002930', 'BER 500 MU': '5550002965',
  'BDG 285 FA': '5550002995', 'BDG 286 FA': '5550002996', 'LSR 74 FC': '5550003025',
  'YAB 47 NQ': '5550003146', 'APP 231 FQ': '5550003161', 'SKN 317 AU': '5550003637',
  'BDG 159 CL': 'PRIVATE (GML1)', 'AKD 407 BD': 'PRIVATE (AK1)', 'AJ 464 AJ': 'PRIVATE (AK2)',
  'AAA 07 UB': 'PRIVATE (DGS)', 'FST 06 AZ': 'PRIVATE (GML2)', 'AKD 490 SU': 'PRIVATE (CHAIRMAN)',
  'KSF 761 DV': 'PRIVATE (AK3)', 'DE 51 CON': 'PRIVATE (DGS2)', 'RBC 899 HN': 'PRIVATE (AK4)',
  'EPE 520 CH': 'PRIVATE (AK5)', 'AKD 380 EJ': 'PRIVATE (ANIEK U)', '47': 'PRIVATE (AK6)',
  'DBU 519 AA': '27440', 'DBU 710 AA': '27438', 'AFM 673 GL': 'FA000054',
  'AFM 301 GL': 'FA000059', 'AFM 674 GL': 'FA000060', 'AFM 302 GL': 'FA000055',
  'AFM 304 GL': 'FA000056', 'AFM 303 GL': 'FA000057', 'AFM 675 GL': 'FA000058',
};

/** Complete Desicon Group generator fleet (from Daily Routine Report register) */
export const GENERATOR_LIST: Array<{ assetNo: string; description: string; location: string }> = [
  { assetNo: '02135', description: 'PHC OFFICE CAT 350KVA GENERATOR',       location: 'Port Harcourt Office' },
  { assetNo: '03189', description: 'PHC OFFICE CUMMINS 275KVA GENERATOR',   location: 'Port Harcourt Office' },
  { assetNo: '01819', description: 'PHC OFFICE MIKANO 200KVA GENERATOR',    location: 'Port Harcourt Office' },
  { assetNo: '03188', description: 'DR CUMMINS 275KVA GENERATOR',           location: 'DR' },
  { assetNo: '24031', description: 'DR FG WILSON 150KVA GENERATOR',         location: 'DR' },
  { assetNo: '00794', description: 'DGS PERKINS 50KVA GENERATOR',           location: 'DGS' },
  { assetNo: '03092', description: 'DGS MIKANO 50KVA GENERATOR',            location: 'DGS' },
  { assetNo: '01346', description: 'GML PERKINS 50KVA GENERATOR',           location: 'GML' },
  { assetNo: 'GML-M',  description: 'GML M SALEH 50KVA GENERATOR',          location: 'GML' },
  { assetNo: '00017', description: 'WOJI YARD FG WILSON 40KVA GENERATOR',   location: 'Woji' },
  { assetNo: 'LGOS1', description: 'LAGOS OFFICE PERKINS 100KVA GENERATOR', location: 'Lagos Office' },
  { assetNo: 'LGOS2', description: 'LAGOS OFFICE CUMMINS 135KVA GENERATOR', location: 'Lagos Office' },
  { assetNo: 'AKLG1', description: 'AK LAGOS 80KVA GENERATOR',              location: 'AK Lagos' },
  { assetNo: 'AKLG2', description: 'AK LAGOS CAT 100KVA GENERATOR',         location: 'AK Lagos' },
  { assetNo: 'AKLG3', description: 'AK LAGOS 20KVA GENERATOR',              location: 'AK Lagos' },
  { assetNo: 'MDYO1', description: 'AK UYO MARAPCO 100KVA GENERATOR',       location: 'AK Uyo' },
  { assetNo: 'MDYO65',description: 'AK UYO PERKINS 65KVA GENERATOR',        location: 'AK Uyo' },
  { assetNo: 'CHRM47',description: "CHAIRMAN'S PERKINS 47KVA GENERATOR",    location: 'Chairman Uyo' },
  { assetNo: 'CHRM40',description: "CHAIRMAN'S PERKINS 40KVA GENERATOR",    location: 'Chairman Uyo' },
  { assetNo: 'CHRM50',description: "CHAIRMAN'S MIKANO 50KVA GENERATOR",     location: 'Chairman Uyo' },
  { assetNo: 'TOMY1', description: 'TOMY PERKINS 80KVA GENERATOR',          location: 'Tomy Residence' },
  { assetNo: 'TOMY2', description: 'TOMY PERKINS 40KVA GENERATOR',          location: 'Tomy Residence' },
  { assetNo: 'BNNY1', description: 'BONNY PERKINS 60KVA GENERATOR',         location: 'Bonny' },
];

export type VehicleMaintenanceStatus =
  | 'Pending' | 'Approved' | 'InWorkshop'
  | 'AwaitingParts' | 'AwaitingFunds'
  | 'Completed' | 'Rejected';

export type VehicleMaintenanceType =
  | 'RoutineService' | 'MinorRepair' | 'MajorRepair';

export interface VehicleMaintenance {
  id:               string;
  requestNumber:    string;
  vehicleRegNo:     string;
  vehicleType:      string;
  maintenanceType:  VehicleMaintenanceType;
  description:      string;
  priority:         RequestPriority;
  status:           VehicleMaintenanceStatus;
  currentLocation:  string;
  odometerReading?: string;
  requestedByEmail: string;
  requestedByName:  string;
  approvedByEmail?: string;
  approvedByName?:  string;
  approvedAt?:      string;
  rejectionReason?: string;
  // workshop
  workshopName?:            string;
  workshopLocation?:        string;
  dateDeliveredToWorkshop?: string;
  sentToWorkshopAt?:        string;
  // assessment
  faultIdentified?:  string;
  proposedSolution?: string;
  resolutionType?:   string;   // Internal | Outsourced
  // parts
  partsRequired:     boolean;
  partsSource?:      string;   // StoreInventory | NewPurchase
  procurementMethod?:string;   // PO | CashAdvance
  partsSuppliedBy?:  string;
  sparesCostNaira?:  number;
  // completion
  workDone?:    string;
  actionedBy?:  string;
  completedAt?: string;
  // handover
  handoverConfirmed: boolean;
  dateHandedOver?:   string;
  handedOverBy?:     string;
  notes?:            string;
  createdAt:         string;
  updatedAt:         string;
  daysOpen:          number;
  daysInWorkshop?:   number;
  assetNo?:          string;
  amountNaira?:      number;
  runningHours?:       number;
  nextServiceHour?:    number;
  notificationStatus?: string;
  finalAmountNaira?:   number;
  dateOfRequest?:      string;
}

export interface VehicleMaintenanceStats {
  pending:            number;
  approved:           number;
  inWorkshop:         number;
  awaitingParts:      number;
  awaitingFunds:      number;
  completedThisMonth: number;
  rejected:           number;
  longStanding:       number;
}

export interface VehicleMaintenanceListResponse {
  items:      VehicleMaintenance[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export const VM_STATUS_META: Record<VehicleMaintenanceStatus, { label: string; color: string; badge: string }> = {
  Pending:       { label: 'Pending',        color: 'orange',  badge: 'warning'    },
  Approved:      { label: 'Approved',       color: 'blue',    badge: 'processing' },
  InWorkshop:    { label: 'In Workshop',    color: 'purple',  badge: 'processing' },
  AwaitingParts: { label: 'Awaiting Parts', color: 'gold',    badge: 'warning'    },
  AwaitingFunds: { label: 'Awaiting Funds', color: 'volcano', badge: 'warning'    },
  Completed:     { label: 'Completed',      color: 'green',   badge: 'success'    },
  Rejected:      { label: 'Rejected',       color: 'red',     badge: 'error'      },
};

export const VM_TYPE_META: Record<VehicleMaintenanceType, { label: string; color: string }> = {
  RoutineService: { label: 'Routine Service & Maintenance', color: 'blue'    },
  MinorRepair:    { label: 'Minor Repair & Maintenance',    color: 'orange'  },
  MajorRepair:    { label: 'Major Repair & Maintenance',    color: 'red'     },
};

// ── Equipment & Facility Maintenance ──────────────────────────────────────────

export type MaintenanceRequestStatus =
  | 'Pending' | 'Approved' | 'Ongoing'
  | 'AwaitingSpares' | 'AwaitingFunds'
  | 'Completed' | 'Rejected';

export const MR_STATUS_META: Record<MaintenanceRequestStatus, { label: string; color: string; badge: string }> = {
  Pending:       { label: 'Pending',          color: 'orange',  badge: 'warning'    },
  Approved:      { label: 'Approved',         color: 'blue',    badge: 'processing' },
  Ongoing:       { label: 'Ongoing',          color: 'purple',  badge: 'processing' },
  AwaitingSpares:{ label: 'Awaiting Spares',  color: 'gold',    badge: 'warning'    },
  AwaitingFunds: { label: 'Awaiting Funds',   color: '#fa541c', badge: 'warning'    },
  Completed:     { label: 'Completed',        color: 'green',   badge: 'success'    },
  Rejected:      { label: 'Rejected',         color: 'red',     badge: 'error'      },
};

// Equipment
export type EquipmentMaintenanceType =
  | 'GeneratorService' | 'GeneratorRepair'
  | 'ACService' | 'ACRepair'
  | 'UPSMaintenance' | 'PumpService' | 'Electrical' | 'Other';

export const EQUIPMENT_TYPE_META: Record<EquipmentMaintenanceType, { label: string; color: string }> = {
  GeneratorService: { label: 'Generator Service (250hr)',  color: 'volcano'  },
  GeneratorRepair:  { label: 'Generator Repair',          color: 'red'      },
  ACService:        { label: 'A/C Service',               color: 'blue'     },
  ACRepair:         { label: 'A/C Repair',                color: 'geekblue' },
  UPSMaintenance:   { label: 'UPS / Inverter',            color: 'purple'   },
  PumpService:      { label: 'Pump Service',              color: 'cyan'     },
  Electrical:       { label: 'Electrical Equipment',      color: 'gold'     },
  Other:            { label: 'Other',                     color: 'default'  },
};

export interface EquipmentMaintenance {
  id:               string;
  requestNumber:    string;
  assetNo:          string;
  assetDescription: string;
  maintenanceType:  EquipmentMaintenanceType;
  endUser:          string;
  location:         string;
  runningHours?:    number;
  nextServiceHour?: number;
  description:      string;
  priority:         RequestPriority;
  status:           MaintenanceRequestStatus;
  requestedByEmail: string;
  requestedByName:  string;
  approvedByEmail?: string;
  approvedByName?:  string;
  approvedAt?:      string;
  rejectionReason?: string;
  // assessment
  faultIdentified?:  string;
  proposedSolution?: string;
  resolutionType?:   string;
  // parts
  partsRequired:     boolean;
  partsSource?:      string;
  procurementMethod?:string;
  sparesCostNaira?:  number;
  // completion
  workDone?:     string;
  actionedBy?:   string;
  completedAt?:  string;
  // handover
  handoverConfirmed: boolean;
  dateHandedOver?:   string;
  handedOverBy?:     string;
  notes?:            string;
  createdAt:         string;
  updatedAt:         string;
  daysOpen:          number;
  // register fields (July 2026)
  dateOfRequest?:          string;
  requestor?:              string;
  notificationStatus?:     string;
  odometerReading?:        string;
  partsSuppliedBy?:        string;
  dateTakenToWorkshop?:    string;
  justificationEvaluation?:string;
  daysTakenToComplete?:    number;
  amountNaira?:            number;
  finalAmountNaira?:       number;
}

export interface EquipmentMaintenanceStats {
  pending:            number;
  approved:           number;
  ongoing:            number;
  awaitingSpares:     number;
  awaitingFunds:      number;
  completedThisMonth: number;
  rejected:           number;
}

export interface EquipmentMaintenanceListResponse {
  items:      EquipmentMaintenance[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

// Facility
export type FacilityMaintenanceType =
  | 'Electrical' | 'Plumbing' | 'CivilWorks' | 'Painting'
  | 'TankWashing' | 'FireSafety' | 'Fumigation' | 'Carpentry'
  | 'ACService' | 'SepticTank' | 'Glasswork' | 'General';

export const FACILITY_TYPE_META: Record<FacilityMaintenanceType, { label: string; color: string }> = {
  Electrical:  { label: 'Electrical Works', color: 'gold'    },
  Plumbing:    { label: 'Plumbing',         color: 'cyan'    },
  CivilWorks:  { label: 'Civil Works',      color: 'brown' as any },
  Painting:    { label: 'Painting / Repainting', color: 'lime' },
  TankWashing: { label: 'Tank Washing',     color: 'blue'    },
  FireSafety:  { label: 'Fire Safety',      color: 'red'     },
  Fumigation:  { label: 'Fumigation',       color: 'purple'  },
  Carpentry:   { label: 'Carpentry',        color: 'orange'  },
  ACService:   { label: 'A/C Service / Repair', color: 'geekblue' },
  SepticTank:  { label: 'Septic Tank',      color: 'volcano' },
  Glasswork:   { label: 'Glasswork',        color: 'cyan'    },
  General:     { label: 'General',          color: 'default' },
};

export interface FacilityMaintenance {
  id:               string;
  requestNumber:    string;
  maintenanceType:  FacilityMaintenanceType;
  description:      string;
  location:         string;
  endUser:          string;
  roomFlat?:        string;
  priority:         RequestPriority;
  status:           MaintenanceRequestStatus;
  requestedByEmail: string;
  requestedByName:  string;
  approvedByEmail?: string;
  approvedByName?:  string;
  approvedAt?:      string;
  rejectionReason?: string;
  // assessment
  faultIdentified?:  string;
  proposedSolution?: string;
  resolutionType?:   string;
  // parts
  partsRequired:     boolean;
  partsSource?:      string;
  procurementMethod?:string;
  sparesCostNaira?:  number;
  // completion
  workDone?:    string;
  actionedBy?:  string;
  completedAt?: string;
  // handover
  handoverConfirmed: boolean;
  dateHandedOver?:   string;
  handedOverBy?:     string;
  notes?:            string;
  createdAt:         string;
  updatedAt:         string;
  daysOpen:          number;
  // register fields (July 2026)
  dateOfRequest?:          string;
  requestor?:              string;
  notificationStatus?:     string;
  assetNo?:                string;
  odometerReading?:        string;
  partsSuppliedBy?:        string;
  dateTakenToWorkshop?:    string;
  justificationEvaluation?:string;
  daysTakenToComplete?:    number;
  amountNaira?:            number;
  finalAmountNaira?:       number;
}

export interface FacilityMaintenanceStats {
  pending:            number;
  approved:           number;
  ongoing:            number;
  awaitingSpares:     number;
  awaitingFunds:      number;
  completedThisMonth: number;
  rejected:           number;
}

export interface FacilityMaintenanceListResponse {
  items:      FacilityMaintenance[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

// ── Daily Parameter Log ────────────────────────────────────────────────────────

export type GeneratorStatusType = 'Running' | 'Standby' | 'Fault' | 'Off';
export type WaterSourceType     = 'Municipal' | 'Borehole' | 'Both' | 'None';
export type WaterStatusType     = 'Adequate' | 'Low' | 'Critical' | 'Refilled';
export type SecurityStatusType  = 'Normal' | 'Incident Reported';

export interface DailyParameterLog {
  id:                    string;
  logDate:               string;   // "YYYY-MM-DD"
  location:              string;
  // Power
  nepaHoursAvailable?:   number;
  generatorHoursRun?:    number;
  dieselConsumedLitres?: number;
  dieselBalanceLitres?:  number;
  generatorStatus?:      GeneratorStatusType;
  generatorRunHourMeter?:number;
  // Water
  waterSource?:          WaterSourceType;
  waterTankLevelPercent?:number;
  waterStatus?:          WaterStatusType;
  // Staff
  staffPresent?:         number;
  expectedStaff?:        number;
  visitorCount?:         number;
  // Facility
  cleaningDone:          boolean;
  wasteDisposed:         boolean;
  securityStatus?:       SecurityStatusType;
  // Observations
  maintenanceIssues?:    string;
  actionsTaken?:         string;
  pendingActions?:       string;
  generalRemarks?:       string;
  // Logger
  loggedByEmail:         string;
  loggedByName:          string;
  createdAt:             string;
  updatedAt:             string;
}

export interface DailyParameterLogListResponse {
  items:      DailyParameterLog[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface DailyParameterLogStats {
  logsThisMonth:              number;
  avgNepaHoursThisMonth?:     number;
  avgGeneratorHoursThisMonth?:number;
  totalDieselThisMonth?:      number;
  locationsLogged:            number;
}

export interface CreateDailyParameterLogPayload {
  logDate:               string;
  location:              string;
  nepaHoursAvailable?:   number;
  generatorHoursRun?:    number;
  dieselConsumedLitres?: number;
  dieselBalanceLitres?:  number;
  generatorStatus?:      string;
  generatorRunHourMeter?:number;
  waterSource?:          string;
  waterTankLevelPercent?:number;
  waterStatus?:          string;
  staffPresent?:         number;
  expectedStaff?:        number;
  visitorCount?:         number;
  cleaningDone:          boolean;
  wasteDisposed:         boolean;
  securityStatus?:       string;
  maintenanceIssues?:    string;
  actionsTaken?:         string;
  pendingActions?:       string;
  generalRemarks?:       string;
}

export interface UpdateDailyParameterLogPayload extends Partial<Omit<CreateDailyParameterLogPayload, 'logDate' | 'location'>> {}

// ── Feeding & Accommodation (guest house) ───────────────────────────────────────

export const MEAL_PLAN_OPTIONS = ['None', 'Breakfast', 'Half Board', 'Full Board'] as const;
export const ACCOMMODATION_STATUSES = ['Reserved', 'CheckedIn', 'CheckedOut'] as const;

export const ACCOMMODATION_STATUS_META: Record<string, { label: string; color: string }> = {
  Reserved:   { label: 'Reserved',    color: 'blue'    },
  CheckedIn:  { label: 'Checked In',  color: 'green'   },
  CheckedOut: { label: 'Checked Out', color: 'default' },
};

export interface AccommodationLog {
  id:                     string;
  reference:              string;
  guestName:              string;
  department?:            string;
  guestHouse:             string;
  purpose?:               string;
  checkInDate:            string;
  checkOutDate?:          string;
  nights?:                number;
  mealPlan?:              string;
  numberOfMeals?:         number;
  feedingCostNaira?:      number;
  accommodationCostNaira?: number;
  totalCostNaira?:        number;
  status:                 string;
  notes?:                 string;
  loggedByEmail:          string;
  loggedByName:           string;
  createdAt:              string;
  updatedAt:              string;
}

export interface AccommodationListResponse {
  items:      AccommodationLog[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface AccommodationStats {
  guestsThisMonth:            number;
  currentlyCheckedIn:         number;
  feedingCostThisMonth:       number;
  accommodationCostThisMonth: number;
  totalCostThisMonth:         number;
}

export interface CreateAccommodationPayload {
  guestName:               string;
  guestHouse:              string;
  checkInDate:             string;
  department?:             string;
  purpose?:                string;
  checkOutDate?:           string;
  nights?:                 number;
  mealPlan?:               string;
  numberOfMeals?:          number;
  feedingCostNaira?:       number;
  accommodationCostNaira?: number;
  status?:                 string;
  notes?:                  string;
}

// ── User Management ────────────────────────────────────────────────────────────

export const ALL_ROLES = [
  'SystemAdmin',
  'DepartmentManager',
  'Supervisor',
  'Technician',
  'Driver',
  'Requester',
  'StoreOfficer',
] as const;

export type AppUserRole = typeof ALL_ROLES[number];

export const ROLE_META: Record<AppUserRole, { label: string; color: string }> = {
  SystemAdmin:       { label: 'System Admin',       color: 'red'      },
  DepartmentManager: { label: 'Dept. Manager',      color: 'purple'   },
  Supervisor:        { label: 'Supervisor',          color: 'blue'     },
  Technician:        { label: 'Technician',          color: 'cyan'     },
  Driver:            { label: 'Driver',              color: 'geekblue' },
  Requester:         { label: 'Requester',           color: 'default'  },
  StoreOfficer:      { label: 'Store Officer',       color: 'orange'   },
};

export interface AppUserRecord {
  id:              string;
  email:           string;
  fullName:        string;
  role:            AppUserRole;
  department:      string;
  isActive:        boolean;
  lastLoginAt?:    string;
  createdByEmail?: string;
  createdAt:       string;
  updatedAt:       string;
}

export interface UserListResponse {
  items:      AppUserRecord[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}

export interface UserSummary {
  total:    number;
  active:   number;
  inactive: number;
  byRole:   { role: string; count: number }[];
}

export interface CreateUserPayload {
  email:      string;
  fullName:   string;
  role:       string;
  department: string;
  password:   string;
}

export interface UpdateUserPayload {
  fullName?:   string;
  role?:       string;
  department?: string;
}

export interface ResetPasswordResponse {
  email:             string;
  temporaryPassword: string;
  message:           string;
}

// ── Store / Inventory ─────────────────────────────────────────────────────────

export const STORE_CATEGORIES = [
  'Generator Parts',
  'AC Parts',
  'Electrical Items',
  'Plumbing Items',
  'Cleaning Supplies',
  'Lubricants & Oils',
  'Vehicle Parts',
  'Safety Items',
  'Office Supplies',
  'General Store',
] as const;

export type StoreCategory = typeof STORE_CATEGORIES[number];

export const STORE_UNITS = [
  'Pieces','Litres','Metres','Kg','Rolls',
  'Packets','Pairs','Sets','Boxes','Cartons','Gallons','Bags','Drums',
] as const;

export type StoreUnit = typeof STORE_UNITS[number];

export const STORE_MOVEMENT_TYPES = ['Receipt','Issue','Adjustment','Return'] as const;
export type StoreMovementType = typeof STORE_MOVEMENT_TYPES[number];

export const STORE_REQUISITION_STATUSES = ['Pending','Approved','Issued','Rejected'] as const;
export type StoreRequisitionStatus = typeof STORE_REQUISITION_STATUSES[number];

export interface StoreItem {
  id:              string;
  itemCode:        string;
  name:            string;
  category:        string;
  unit:            string;
  quantityInStock: number;
  reorderLevel:    number;
  isLowStock:      boolean;
  unitCostNaira:   number;
  totalValueNaira: number;
  description?:    string;
  storeLocation?:  string;
  supplier?:       string;
  isActive:        boolean;
  createdByEmail:  string;
  createdAt:       string;
  updatedAt:       string;
}

export interface StoreItemListResponse {
  items:               StoreItem[];
  total:               number;
  page:                number;
  pageSize:            number;
  totalPages:          number;
  lowStockCount:       number;
  totalStoreValueNaira:number;
}

export interface StoreMovement {
  id:             string;
  itemCode:       string;
  itemName:       string;
  movementType:   StoreMovementType;
  quantityBefore: number;
  quantityChange: number;
  quantityAfter:  number;
  reference?:     string;
  notes?:         string;
  movedByEmail:   string;
  movedByName:    string;
  createdAt:      string;
}

// Requisitions
export interface StoreRequisitionItem {
  id:                string;
  storeItemId:       string;
  itemCode:          string;
  itemName:          string;
  unit:              string;
  quantityRequested: number;
  quantityIssued:    number;
  unitCostNaira:     number;
  totalCost:         number;
  currentStock:      number;
}

export interface StoreRequisition {
  id:                string;
  requisitionNumber: string;
  requestedByEmail:  string;
  requestedByName:   string;
  department:        string;
  purpose:           string;
  linkedReference?:  string;
  status:            StoreRequisitionStatus;
  approvedByName?:   string;
  approvedAt?:       string;
  rejectedByEmail?:  string;
  rejectionReason?:  string;
  rejectedAt?:       string;
  issuedByName?:     string;
  issuedAt?:         string;
  notes?:            string;
  createdAt:         string;
  updatedAt:         string;
  items:             StoreRequisitionItem[];
  totalCostNaira:    number;
}

export interface StoreRequisitionListResponse {
  items:         StoreRequisition[];
  total:         number;
  page:          number;
  pageSize:      number;
  totalPages:    number;
  pendingCount:  number;
  approvedCount: number;
}

// Payloads
export interface CreateStoreItemPayload {
  name:            string;
  category:        string;
  unit:            string;
  quantityInStock: number;
  reorderLevel:    number;
  unitCostNaira:   number;
  description?:    string;
  storeLocation?:  string;
  supplier?:       string;
}

export interface UpdateStoreItemPayload {
  name:           string;
  category:       string;
  unit:           string;
  reorderLevel:   number;
  unitCostNaira:  number;
  description?:   string;
  storeLocation?: string;
  supplier?:      string;
  isActive:       boolean;
}

export interface RestockPayload {
  quantity:      number;
  unitCostNaira: number;
  reference?:    string;
  notes?:        string;
}

export interface AdjustStockPayload {
  newQuantity: number;
  reason:      string;
}

export interface CreateRequisitionLineItem {
  storeItemId:       string;
  quantityRequested: number;
}

export interface CreateStoreRequisitionPayload {
  purpose:         string;
  linkedReference?: string;
  notes?:          string;
  items:           CreateRequisitionLineItem[];
}

export interface IssueLineItem {
  storeRequisitionItemId: string;
  quantityIssued:         number;
}

export interface IssueRequisitionPayload {
  items:  IssueLineItem[];
  notes?: string;
}

// ── Diesel Requisitions ───────────────────────────────────────────────────────

export const DIESEL_EQUIPMENT_TYPES = ['Generator', 'Vehicle', 'Fuel Store', 'Other'] as const;
export type DieselEquipmentType = typeof DIESEL_EQUIPMENT_TYPES[number];

export const DIESEL_REQUISITION_STATUSES = ['Pending', 'Approved', 'Dispensed', 'Rejected'] as const;
export type DieselRequisitionStatus = typeof DIESEL_REQUISITION_STATUSES[number];

export interface DieselRequisition {
  id:                       string;
  requisitionNumber:        string;
  purpose:                  string;
  equipmentType:            DieselEquipmentType;
  equipmentReference?:      string;
  location:                 string;
  quantityRequestedLitres:  number;
  requestedByEmail:         string;
  requestedByName:          string;
  department:               string;
  status:                   DieselRequisitionStatus;
  // Approval
  approvedByName?:          string;
  approvedAt?:              string;
  // Rejection
  rejectedByEmail?:         string;
  rejectionReason?:         string;
  rejectedAt?:              string;
  // Dispense
  dispensedByName?:         string;
  dispensedAt?:             string;
  quantityDispensedLitres?: number;
  tankLevelBeforeLitres?:   number;
  tankLevelAfterLitres?:    number;
  unitCostPerLitreNaira?:   number;
  totalCostNaira?:          number;
  linkedDieselRecordId?:    string;
  notes?:                   string;
  createdAt:                string;
  updatedAt:                string;
}

export interface DieselRequisitionListResponse {
  items:                       DieselRequisition[];
  total:                       number;
  page:                        number;
  pageSize:                    number;
  totalPages:                  number;
  pendingCount:                number;
  approvedCount:               number;
  totalDispensedLitresThisMonth: number;
  totalCostThisMonth:          number;
}

export interface DieselRequisitionStats {
  totalAllTime:       number;
  pendingCount:       number;
  approvedCount:      number;
  dispensedCount:     number;
  rejectedCount:      number;
  litresThisMonth:    number;
  costThisMonth:      number;
  litresThisYear:     number;
  costThisYear:       number;
  byEquipmentType:    { type: string; count: number; litres: number }[];
  monthlyTrend:       { month: string; litres: number; cost: number }[];
}

// Payloads
export interface CreateDieselRequisitionPayload {
  purpose:                 string;
  equipmentType:           DieselEquipmentType;
  equipmentReference?:     string;
  location:                string;
  quantityRequestedLitres: number;
  notes?:                  string;
}

export interface DispenseDieselPayload {
  quantityDispensedLitres: number;
  tankLevelBeforeLitres:   number;
  unitCostPerLitreNaira:   number;
  notes?:                  string;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Electricity Management (PHED + Prepaid)
// ═══════════════════════════════════════════════════════════════════════════════

export const ELECTRICITY_LOCATIONS = [
  'WOJI', 'DGS', 'OFFICE', 'DR', 'UYO – Chairman', 'UYO – MD',
] as const;

export const ELECTRICITY_TYPES = ['PHED', 'Prepaid'] as const;
export type ElectricityType = typeof ELECTRICITY_TYPES[number];

export type ElectricityStatus = 'Active' | 'LowBalance' | 'Depleted';

export const ELECTRICITY_STATUS_META: Record<ElectricityStatus, { label: string; color: string }> = {
  Active:     { label: 'Active',      color: 'green'  },
  LowBalance: { label: 'Low Balance', color: 'orange' },
  Depleted:   { label: 'Depleted',    color: 'red'    },
};

export interface ElectricityPurchase {
  id:                     string;
  purchaseType:           ElectricityType;
  location:               string;
  purchaseDate:           string;
  vendor?:                string;
  amountNaira:            number;
  unitsKwh:               number;
  paymentReference?:      string;
  tokenNumber?:           string;
  meterReadingKwh?:       number;
  receiptAttachment?:     string;
  lowBalanceThresholdKwh: number;
  balanceAfterKwh:        number;
  status:                 ElectricityStatus;
  notes?:                 string;
  loggedByEmail:          string;
  loggedByName:           string;
  createdAt:              string;
}

export interface ElectricityBalance {
  location:               string;
  balanceKwh:             number;
  lowBalanceThresholdKwh: number;
  status:                 ElectricityStatus;
  totalSpendNaira:        number;
  totalUnitsPurchased:    number;
  lastPurchaseDate?:      string;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  DStv Subscription Management
// ═══════════════════════════════════════════════════════════════════════════════

export const DSTV_PACKAGES = [
  'Premium', 'Premium + Extraview', 'Compact Plus', 'Compact', 'Confam', 'Yanga', 'Padi', 'Great Wall Standalone',
] as const;

export type DstvStatus = 'Active' | 'ExpiringSoon' | 'Expired';

export const DSTV_STATUS_META: Record<DstvStatus, { label: string; color: string }> = {
  Active:       { label: 'Active',        color: 'green'  },
  ExpiringSoon: { label: 'Expiring Soon', color: 'orange' },
  Expired:      { label: 'Expired',       color: 'red'    },
};

export interface DstvSubscription {
  id:                string;
  decoderNumber:     string;
  location:          string;
  package:           string;
  startDate:         string;
  durationMonths:    number;
  expiryDate:        string;
  daysToExpiry:      number;
  amountNaira:       number;
  paymentMethod?:    string;
  vendor?:           string;
  receiptAttachment?:string;
  status:            DstvStatus;
  notes?:            string;
  loggedByEmail:     string;
  loggedByName:      string;
  createdAt:         string;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Vehicle Registry & Statutory Documents
// ═══════════════════════════════════════════════════════════════════════════════

export const VEHICLE_OPERATIONAL_STATUSES = ['Active', 'InWorkshop', 'Grounded', 'Disposed'] as const;
export type VehicleOperationalStatus = typeof VEHICLE_OPERATIONAL_STATUSES[number];

export const VEHICLE_OPERATIONAL_STATUS_META: Record<VehicleOperationalStatus, { label: string; color: string }> = {
  Active:     { label: 'Active',      color: 'green'  },
  InWorkshop: { label: 'In Workshop', color: 'blue'   },
  Grounded:   { label: 'Grounded',    color: 'orange' },
  Disposed:   { label: 'Disposed',    color: 'default'},
};

export const VEHICLE_DOCUMENT_TYPES = [
  'VehicleLicence', 'RoadWorthiness', 'Insurance', 'HackneyPermit', 'HeavyDutyPermit', 'ProofOfOwnership',
] as const;
export type VehicleDocumentType = typeof VEHICLE_DOCUMENT_TYPES[number];

export const VEHICLE_DOCUMENT_TYPE_META: Record<VehicleDocumentType, { label: string }> = {
  VehicleLicence:   { label: 'Vehicle Licence'          },
  RoadWorthiness:   { label: 'Road Worthiness'          },
  Insurance:        { label: 'Insurance'                },
  HackneyPermit:    { label: 'Hackney Carriage Permit'  },
  HeavyDutyPermit:  { label: 'Heavy Duty Permit'        },
  ProofOfOwnership: { label: 'Proof of Ownership'       },
};

export type VehicleDocumentStatus = 'Valid' | 'Expiring' | 'Expired';

export const VEHICLE_DOCUMENT_STATUS_META: Record<VehicleDocumentStatus, { label: string; color: string }> = {
  Valid:    { label: 'Valid',    color: 'green'  },
  Expiring: { label: 'Expiring', color: 'orange' },
  Expired:  { label: 'Expired',  color: 'red'    },
};

export interface VehicleRegistryRecord {
  id:                    string;
  fleetNumber:           string;
  registrationNumber:    string;
  vehicleType:           string;
  makeModel?:            string;
  yearOfManufacture?:    number;
  engineNumber?:         string;
  chassisNumber?:        string;
  colour?:               string;
  assignedLocation?:     string;
  assignedDriver?:       string;
  acquisitionDate?:      string;
  operationalStatus:     VehicleOperationalStatus;
  notes?:                string;
  documentCount:         number;
  expiringDocumentCount: number;
  expiredDocumentCount:  number;
  loggedByEmail:         string;
  loggedByName:          string;
  createdAt:             string;
}

export interface VehicleDocument {
  id:                string;
  vehicleId:         string;
  vehicleRegNo:      string;
  documentType:      VehicleDocumentType;
  issueDate:         string;
  expiryDate:        string;
  daysToExpiry:      number;
  issuingAuthority?: string;
  renewalCostNaira?: number;
  receiptAttachment?:string;
  status:            VehicleDocumentStatus;
  notes?:            string;
  loggedByEmail:     string;
  loggedByName:      string;
  createdAt:         string;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Diesel Supply & Distribution
// ═══════════════════════════════════════════════════════════════════════════════

export const DIESEL_DISTRIBUTION_TYPES = ['Vehicle', 'Location'] as const;
export type DieselDistributionType = typeof DIESEL_DISTRIBUTION_TYPES[number];

export interface DieselSupply {
  id:                        string;
  supplyReference:           string;
  supplyDate:                string;
  vendor:                    string;
  invoiceNumber?:            string;
  quantityLitres:            number;
  unitPriceNaira:            number;
  totalCostNaira:            number;
  storageLocation?:          string;
  deliveryDocuments?:        string;
  receivingOfficer?:         string;
  quantityRemainingLitres:   number;
  quantityDistributedLitres: number;
  notes?:                    string;
  loggedByEmail:             string;
  loggedByName:              string;
  createdAt:                 string;
}

export interface DieselDistribution {
  id:                    string;
  distributionReference: string;
  distributionType:      DieselDistributionType;
  supplyType:            string;
  bulkSupplyId:          string;
  bulkSupplyReference:   string;
  distributionDate:      string;
  quantityLitres:        number;
  purpose?:              string;
  vehicleRegNo?:         string;
  driver?:               string;
  odometerReading?:      string;
  destinationLocation?:  string;
  issuingOfficer?:       string;
  receivingOfficer?:     string;
  recipientAcknowledged: boolean;
  notes?:                string;
  loggedByEmail:         string;
  loggedByName:          string;
  createdAt:             string;
}

export interface DieselStockSummary {
  totalSuppliedLitres:     number;
  totalDistributedLitres:  number;
  availableBalanceLitres:  number;
  totalPurchaseValueNaira: number;
  activeSupplyBatches:     number;
}
