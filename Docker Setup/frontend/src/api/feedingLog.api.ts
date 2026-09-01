import { apiClient } from './client';

export interface MealRate {
  itemKey:        string;
  label:          string;
  unitPriceNaira: number;
  sortOrder:      number;
}

export interface FeedingLogEntry {
  id:               string;
  entryDate:        string;
  staffName:        string;
  projectCostCode?: string;
  breakfast:        number;
  softDrink:        number;
  water:            number;
  juice:            number;
  lunch:            number;
  dinner:           number;
  tea:              number;
  snacks:           number;
  totalItems:       number;
  totalCostNaira:   number;
  notes?:           string;
  loggedByName:     string;
  createdAt:        string;
  lastEditedByName?:string;
  lastEditedAt?:    string;
}

export interface FeedingLogListResponse {
  items:          FeedingLogEntry[];
  totalCount:     number;
  page:           number;
  pageSize:       number;
  totalCostNaira: number;
}

export interface FeedingSummaryRow {
  key:            string;
  breakfast:      number;
  softDrink:      number;
  water:          number;
  juice:          number;
  lunch:          number;
  dinner:         number;
  tea:            number;
  snacks:         number;
  totalCostNaira: number;
}

export interface FeedingSummaryResponse {
  byCostCentre: FeedingSummaryRow[];
  byHeadCount:  FeedingSummaryRow[];
  grandTotal:   FeedingSummaryRow;
  periodLabel:  string;
}

export interface FeedingStats {
  entriesThisMonth:  number;
  staffFedThisMonth: number;
  mealsThisMonth:    number;
  costThisMonth:     number;
}

export interface FeedingLogQueryParams {
  from?:     string;
  to?:       string;
  costCode?: string;
  search?:   string;
  page?:     number;
  pageSize?: number;
}

export interface FeedingLogPayload {
  entryDate:        string;
  staffName:        string;
  projectCostCode?: string;
  breakfast?:       number;
  softDrink?:       number;
  water?:           number;
  juice?:           number;
  lunch?:           number;
  dinner?:          number;
  tea?:             number;
  snacks?:          number;
  notes?:           string;
}

const clean = (p: FeedingLogQueryParams) => {
  const o: Record<string, string | number> = {};
  Object.entries(p).forEach(([k, v]) => { if (v !== undefined && v !== '' && v !== null) o[k] = v as string | number; });
  return o;
};

export const feedingLogApi = {
  list: (params: FeedingLogQueryParams = {}) =>
    apiClient.get<FeedingLogListResponse>('/feeding-log', { params: clean(params) }).then(r => r.data),

  summary: (params: FeedingLogQueryParams = {}) =>
    apiClient.get<FeedingSummaryResponse>('/feeding-log/summary', { params: clean(params) }).then(r => r.data),

  stats: () => apiClient.get<FeedingStats>('/feeding-log/stats').then(r => r.data),

  rates: () => apiClient.get<MealRate[]>('/feeding-log/rates').then(r => r.data),

  updateRates: (rates: { itemKey: string; unitPriceNaira: number }[]) =>
    apiClient.put<MealRate[]>('/feeding-log/rates', { rates }).then(r => r.data),

  create: (data: FeedingLogPayload) =>
    apiClient.post<FeedingLogEntry>('/feeding-log', data).then(r => r.data),

  update: (id: string, data: FeedingLogPayload) =>
    apiClient.put<FeedingLogEntry>(`/feeding-log/${id}`, data).then(r => r.data),

  remove: (id: string) => apiClient.delete(`/feeding-log/${id}`),
};

/** Downloads the Excel export (detail + both summaries), mirroring their old sheet. */
export async function downloadFeedingLogExport(params: FeedingLogQueryParams = {}): Promise<void> {
  const res = await apiClient.get('/feeding-log/export', {
    params: clean(params), responseType: 'blob', timeout: 60_000,
  });
  const cd = res.headers['content-disposition'] as string | undefined;
  let filename = 'Feeding_Log.xlsx';
  if (cd) { const m = cd.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/); if (m?.[1]) filename = m[1].replace(/['"]/g, ''); }
  const url = URL.createObjectURL(new Blob([res.data as BlobPart]));
  const link = document.createElement('a');
  link.href = url; link.download = filename;
  document.body.appendChild(link); link.click(); document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
