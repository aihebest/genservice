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
  title:       string;
  description: string;
  category:    RequestCategory;
  priority:    RequestPriority;
  location:    string;
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
  id:                   string;
  assetNo:              string;
  assetDescription:     string;
  location:             string;
  readingDate:          string;
  cumulativeRunHours:   number;
  runHoursToday:        number;
  generatorStatus:      GeneratorDailyStatus;
  fuelLevelLitres:      number;
  fuelConsumedLitres?:  number;
  utilityAvailableHours?:number;
  serviceIntervalHours: number;
  lastServicedAtHours?: number;
  serviceAlertActive:   boolean;
  hoursUntilNextService:number;
  notes?:               string;
  loggedByEmail:        string;
  loggedByName:         string;
  createdAt:            string;
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

export type VehicleMaintenanceStatus =
  | 'Pending' | 'Approved' | 'InWorkshop' | 'Completed' | 'Rejected';

export type VehicleMaintenanceType =
  | 'Servicing' | 'Repair' | 'Inspection' | 'Bodywork' | 'TyreChange' | 'Battery' | 'Other';

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
  requestedByEmail: string;
  requestedByName:  string;
  approvedByEmail?: string;
  approvedByName?:  string;
  approvedAt?:      string;
  rejectionReason?: string;
  workshopName?:    string;
  workshopLocation?:string;
  sentToWorkshopAt?:string;
  completedAt?:     string;
  notes?:           string;
  createdAt:        string;
  updatedAt:        string;
  daysOpen:         number;
  daysInWorkshop?:  number;
}

export interface VehicleMaintenanceStats {
  pending:            number;
  approved:           number;
  inWorkshop:         number;
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
  Pending:    { label: 'Pending',     color: 'orange',     badge: 'warning'    },
  Approved:   { label: 'Approved',    color: 'blue',       badge: 'processing' },
  InWorkshop: { label: 'In Workshop', color: 'purple',     badge: 'processing' },
  Completed:  { label: 'Completed',   color: 'green',      badge: 'success'    },
  Rejected:   { label: 'Rejected',    color: 'red',        badge: 'error'      },
};

export const VM_TYPE_META: Record<VehicleMaintenanceType, { label: string; color: string }> = {
  Servicing:  { label: 'Servicing',         color: 'blue'     },
  Repair:     { label: 'Repair',            color: 'orange'   },
  Inspection: { label: 'Inspection',        color: 'cyan'     },
  Bodywork:   { label: 'Bodywork',          color: 'purple'   },
  TyreChange: { label: 'Tyre Change',       color: 'volcano'  },
  Battery:    { label: 'Battery',           color: 'gold'     },
  Other:      { label: 'Other',             color: 'default'  },
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
  | 'GeneratorService' | 'ACService' | 'UPSMaintenance' | 'PumpService' | 'Electrical' | 'Other';

export const EQUIPMENT_TYPE_META: Record<EquipmentMaintenanceType, { label: string; color: string }> = {
  GeneratorService: { label: 'Generator Service', color: 'volcano'  },
  ACService:        { label: 'Air Conditioning',  color: 'blue'     },
  UPSMaintenance:   { label: 'UPS Maintenance',   color: 'geekblue' },
  PumpService:      { label: 'Pump Service',      color: 'cyan'     },
  Electrical:       { label: 'Electrical',        color: 'gold'     },
  Other:            { label: 'Other',             color: 'default'  },
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
  workDone?:        string;
  actionedBy?:      string;
  notes?:           string;
  completedAt?:     string;
  createdAt:        string;
  updatedAt:        string;
  daysOpen:         number;
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
  | 'TankWashing' | 'FireSafety' | 'Fumigation' | 'Carpentry' | 'General';

export const FACILITY_TYPE_META: Record<FacilityMaintenanceType, { label: string; color: string }> = {
  Electrical:  { label: 'Electrical Works', color: 'gold'    },
  Plumbing:    { label: 'Plumbing',         color: 'cyan'    },
  CivilWorks:  { label: 'Civil Works',      color: 'brown' as any },
  Painting:    { label: 'Painting',         color: 'lime'    },
  TankWashing: { label: 'Tank Washing',     color: 'blue'    },
  FireSafety:  { label: 'Fire Safety',      color: 'red'     },
  Fumigation:  { label: 'Fumigation',       color: 'purple'  },
  Carpentry:   { label: 'Carpentry',        color: 'orange'  },
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
  workDone?:        string;
  actionedBy?:      string;
  notes?:           string;
  completedAt?:     string;
  createdAt:        string;
  updatedAt:        string;
  daysOpen:         number;
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
