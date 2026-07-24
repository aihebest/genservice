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

  electricity:   (period: ReportPeriod = '30d') =>
    apiClient.get<Record<string, unknown>>('/reports/electricity', { params: { period } }).then(r => r.data),

  dstv:          () =>
    apiClient.get<Record<string, unknown>>('/reports/dstv').then(r => r.data),

  vehicleDocuments: () =>
    apiClient.get<Record<string, unknown>>('/reports/vehicle-documents').then(r => r.data),

  dieselSupply:  (period: ReportPeriod = '30d') =>
    apiClient.get<Record<string, unknown>>('/reports/diesel-supply', { params: { period } }).then(r => r.data),

  explorer: (params: ExplorerParams) =>
    apiClient.get<ExplorerResponse>('/reports/explorer', { params: cleanExplorerParams(params) }).then(r => r.data),
};

// ── Report Explorer ─────────────────────────────────────────────────────────────

export interface ExplorerColumn { key: string; label: string; kind: string; }

export interface ExplorerResponse {
  dataset:          string;
  columns:          ExplorerColumn[];
  amountKey:        string | null;
  rows:             Record<string, unknown>[];
  totalCount:       number;
  totalAmountNaira: number;
  page:             number;
  pageSize:         number;
}

export interface ExplorerParams {
  dataset:    string;
  from?:      string;
  to?:        string;
  location?:  string;
  status?:    string;
  type?:      string;
  minAmount?: number;
  maxAmount?: number;
  search?:    string;
  page?:      number;
  pageSize?:  number;
}

function cleanExplorerParams(p: ExplorerParams): Record<string, string | number> {
  const out: Record<string, string | number> = {};
  Object.entries(p).forEach(([k, v]) => { if (v !== undefined && v !== '' && v !== null) out[k] = v as string | number; });
  return out;
}

export async function downloadExplorerExport(params: ExplorerParams): Promise<void> {
  const res = await apiClient.get('/reports/explorer/export', {
    params: cleanExplorerParams(params), responseType: 'blob', timeout: 60_000,
  });
  const cd = res.headers['content-disposition'] as string | undefined;
  let filename = `Report_${params.dataset}.xlsx`;
  if (cd) { const m = cd.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/); if (m?.[1]) filename = m[1].replace(/['"]/g, ''); }
  const url = URL.createObjectURL(new Blob([res.data as BlobPart]));
  const link = document.createElement('a');
  link.href = url; link.download = filename;
  document.body.appendChild(link); link.click(); document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
