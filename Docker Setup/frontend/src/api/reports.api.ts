import { apiClient } from './client';

export type ReportPeriod = '7d' | '30d' | '90d';

export interface PeriodBreakdownItem { label: string; count: number; }
export interface TrendPoint          { date:  string; value: number; }

// ── Request Report ─────────────────────────────────────────────────────────────
export interface RequestReport {
  totalRequests:          number;
  openRequests:           number;
  completedRequests:      number;
  pendingApproval:        number;
  rejectedRequests:       number;
  completionRatePercent:  number;
  byCategory:             PeriodBreakdownItem[];
  byStatus:               PeriodBreakdownItem[];
  byPriority:             PeriodBreakdownItem[];
  submissionTrend:        TrendPoint[];
  topRequesters:          PeriodBreakdownItem[];
  periodLabel:            string;
}

// ── Maintenance Report ─────────────────────────────────────────────────────────
export interface MaintenanceCompletionItem {
  taskName:       string;
  category:       string;
  location:       string;
  completedAt:    string;
  completedByName?: string;
}
export interface MaintenanceReport {
  totalSchedules:        number;
  overdueCount:          number;
  completedThisPeriod:   number;
  dueSoon:               number;
  complianceRatePercent: number;
  byCategory:            PeriodBreakdownItem[];
  byFrequency:           PeriodBreakdownItem[];
  recentCompletions:     MaintenanceCompletionItem[];
  periodLabel:           string;
}

// ── Fuel & Power Report ────────────────────────────────────────────────────────
export interface GeneratorSessionItem {
  location:       string;
  runReason:      string;
  startTime:      string;
  runtimeHours?:  number;
  fuelConsumed?:  number;
  outageCause?:   string;
  status:         string;
}
export interface FuelPowerReport {
  totalRuntimeHours:          number;
  totalOutages:               number;
  totalFuelConsumedLitres:    number;
  avgOutageDurationHours:     number;
  currentlyRunning:           number;
  totalPurchasedLitres:       number;
  totalDispensedLitres:       number;
  totalSpendNaira:            number;
  currentStockEstimateLitres: number;
  outagesByReason:            PeriodBreakdownItem[];
  dieselByType:               PeriodBreakdownItem[];
  runtimeTrend:               TrendPoint[];
  dieselUsageTrend:           TrendPoint[];
  recentSessions:             GeneratorSessionItem[];
  periodLabel:                string;
}

export const reportsApi = {
  requests:      (period: ReportPeriod = '30d') =>
    apiClient.get<RequestReport>('/reports/requests', { params: { period } }).then(r => r.data),

  maintenance:   (period: ReportPeriod = '30d') =>
    apiClient.get<MaintenanceReport>('/reports/maintenance', { params: { period } }).then(r => r.data),

  fuel:          (period: ReportPeriod = '30d') =>
    apiClient.get<FuelPowerReport>('/reports/fuel', { params: { period } }).then(r => r.data),

  vehicle:       (period: ReportPeriod = '30d') =>
    apiClient.get<Record<string, unknown>>('/reports/vehicle', { params: { period } }).then(r => r.data),

  vehicleRegister: (regNo?: string) =>
    apiClient.get<Record<string, unknown>>('/reports/vehicle-register', { params: regNo ? { regNo } : undefined }).then(r => r.data),

  facility:      (period: ReportPeriod = '30d') =>
    apiClient.get<Record<string, unknown>>('/reports/facility', { params: { period } }).then(r => r.data),

  generator:     (period: ReportPeriod = '30d') =>
    apiClient.get<Record<string, unknown>>('/reports/generator', { params: { period } }).then(r => r.data),

  accommodation: (period: ReportPeriod = '30d') =>
    apiClient.get<Record<string, unknown>>('/reports/accommodation', { params: { period } }).then(r => r.data),
};
