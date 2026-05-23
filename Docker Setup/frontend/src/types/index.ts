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
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';

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
  assignedToName?:  string;
  approvedByEmail?: string;
  approvedByName?:  string;
  createdAt:        string;
  updatedAt:        string;
  approvedAt?:      string;
  completedAt?:     string;
  rejectionReason?: string;
  notes?:           string;
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
  total:          number;
  open:           number;
  pendingApproval:number;
  approved:       number;
  inProgress:     number;
  completed:      number;
  rejected:       number;
}

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
  Open:            { label: 'Open',             color: 'default'    },
  PendingApproval: { label: 'Pending Approval', color: 'warning'    },
  Approved:        { label: 'Approved',         color: 'success'    },
  Rejected:        { label: 'Rejected',         color: 'error'      },
  InProgress:      { label: 'In Progress',      color: 'processing' },
  Completed:       { label: 'Completed',        color: 'success'    },
  Cancelled:       { label: 'Cancelled',        color: 'default'    },
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
  | 'Fumigation' | 'WasteDisposal' | 'TankWashing' | 'GeneratorService'
  | 'FireSafety' | 'HVAC' | 'Plumbing' | 'Electrical' | 'General';

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

export const MAINTENANCE_CATEGORY_META: Record<MaintenanceCategory, { label: string; color: string }> = {
  Fumigation:      { label: 'Fumigation',       color: 'purple'   },
  WasteDisposal:   { label: 'Waste Disposal',   color: 'lime'     },
  TankWashing:     { label: 'Tank Washing',     color: 'cyan'     },
  GeneratorService:{ label: 'Generator Service',color: 'volcano'  },
  FireSafety:      { label: 'Fire Safety',      color: 'red'      },
  HVAC:            { label: 'HVAC',             color: 'blue'     },
  Plumbing:        { label: 'Plumbing',         color: 'gold'     },
  Electrical:      { label: 'Electrical',       color: 'yellow'   },
  General:         { label: 'General',          color: 'default'  },
};

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
  Purchase:  { label: 'Purchase',   color: 'green'   },
  Dispensed: { label: 'Dispensed',  color: 'blue'    },
  Transfer:  { label: 'Transfer',   color: 'purple'  },
};
