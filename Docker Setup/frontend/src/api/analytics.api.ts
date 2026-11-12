import { apiClient } from './client';

/**
 * Management dashboards modelled on the department's Excel reports.
 * (apiClient already carries /api/v1 — paths here must not repeat it.)
 */

export interface SplitRow { label: string; cost: number; percent: number; count: number; }
export interface MonthlyCost { month: string; cost: number; changePercent?: number | null; }
export interface AssetCost { label: string; cost: number; jobs: number; region: string; }
export interface OpenClosed { open: number; closed: number; }

export interface MaintenanceAnalytics {
  totals:  { overall: number; vehicle: number; equipment: number; facility: number };
  tracker: { vehicle: OpenClosed; equipment: OpenClosed; facility: OpenClosed };
  vehicleByRegion:    SplitRow[];
  equipmentByEndUser: SplitRow[];
  facilityByEndUser:  SplitRow[];
  overallByRegion:    SplitRow[];
  monthly: { vehicle: MonthlyCost[]; equipment: MonthlyCost[]; facility: MonthlyCost[] };
  byAsset: { vehicle: AssetCost[];   equipment: AssetCost[];   facility: AssetCost[] };
  /** Completed jobs with no cost recorded — the totals understate by these. */
  uncosted: number;
}

export interface EnergyPurchaseAnalytics {
  diesel: {
    purchasedCost: number; purchasedLitres: number; issuedCost: number; issuedLitres: number;
    byEndUser: { label: string; litres: number; cost: number; percentCost: number; percentLitres: number }[];
    monthly:   { month: string; endUser: string; litres: number; cost: number }[];
    monthlyPurchase: { month: string; litres: number; cost: number }[];
  };
  phed: {
    totalCost: number; totalKwh: number;
    byEndUser: { label: string; kwh: number; cost: number; percentCost: number; percentKwh: number }[];
    monthly:   { month: string; endUser: string; kwh: number; cost: number }[];
  };
}

export interface LocationConsumption {
  sites: string[];
  site:  string;
  month: string;
  totals: {
    runHours: number; dieselConsumed: number; currentLevel: number;
    phedKwh: number; phedKwhSource: 'meter' | 'purchases';
    lastDieselLitres?: number | null; lastDieselDate?: string | null; daysLogged: number;
  };
  last7Days: { date: string; day: string; litres: number }[];
  weekdayWeekend: { weekday: number; weekend: number };
  daily:   { date: string; litres: number; hours: number }[];
  monthly: { month: string; litres: number; hours: number }[];
}

type Range = { from?: string; to?: string };
const clean = (p: Record<string, string | undefined>) =>
  Object.fromEntries(Object.entries(p).filter(([, v]) => !!v));

export const analyticsApi = {
  maintenance: (r: Range = {}) =>
    apiClient.get<MaintenanceAnalytics>('/reports/analytics/maintenance', { params: clean(r) }).then(x => x.data),

  energyPurchase: (r: Range = {}) =>
    apiClient.get<EnergyPurchaseAnalytics>('/reports/analytics/energy-purchase', { params: clean(r) }).then(x => x.data),

  locationConsumption: (site?: string, month?: string) =>
    apiClient.get<LocationConsumption>('/reports/analytics/location-consumption', { params: clean({ site, month }) })
      .then(x => x.data),
};
