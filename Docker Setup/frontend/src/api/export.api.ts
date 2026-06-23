/**
 * Export API — downloads Excel or PDF files from the backend ExportController.
 * Uses the same axios client (auth + base URL) but with responseType: 'blob'.
 */
import { apiClient } from './client';
import { message } from 'antd';

// ── Core download helper ──────────────────────────────────────────────────────

export async function downloadExport(
  endpoint: string,
  params: Record<string, string | boolean | number | undefined>,
  fallbackFilename: string
): Promise<void> {
  try {
    // Strip undefined values so they don't become "undefined" strings in query
    const cleanParams = Object.fromEntries(
      Object.entries(params).filter(([, v]) => v !== undefined && v !== '')
    );

    const response = await apiClient.get(`/export/${endpoint}`, {
      params:       cleanParams,
      responseType: 'blob',
      timeout:      60_000, // larger timeout for big exports
    });

    // Pick filename from Content-Disposition if available
    const cd = response.headers['content-disposition'] as string | undefined;
    let filename = fallbackFilename;
    if (cd) {
      const match = cd.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
      if (match?.[1]) filename = match[1].replace(/['"]/g, '');
    }

    // Trigger browser download
    const url  = URL.createObjectURL(new Blob([response.data as BlobPart]));
    const link = document.createElement('a');
    link.href     = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  } catch (err: any) {
    console.error('Export failed:', err);
    message.error('Export failed — please try again.');
  }
}

// ── Typed wrappers per report ─────────────────────────────────────────────────

export type ExportFormat = 'excel' | 'pdf';

/** Store inventory */
export const exportInventory = (opts: {
  format?:   ExportFormat;
  category?: string;
  lowStock?: boolean;
}) =>
  downloadExport(
    'inventory',
    { format: opts.format ?? 'excel', category: opts.category, lowStock: opts.lowStock },
    `Inventory_Report_${today()}.${ext(opts.format)}`
  );

/** Store stock movements */
export const exportStoreMovements = (opts: {
  itemCode?: string;
  type?:     string;
  from?:     string;
  to?:       string;
}) =>
  downloadExport(
    'store-movements',
    { format: 'excel', ...opts },
    `Stock_Movement_Log_${today()}.xlsx`
  );

/** Store requisitions */
export const exportRequisitions = (opts: {
  format?: ExportFormat;
  status?: string;
  from?:   string;
  to?:     string;
}) =>
  downloadExport(
    'requisitions',
    { format: opts.format ?? 'excel', status: opts.status, from: opts.from, to: opts.to },
    `Requisitions_Report_${today()}.${ext(opts.format)}`
  );

/** Vehicle maintenance register */
export const exportVehicleRegister = (opts: {
  format?: ExportFormat;
  regNo?:  string;
  status?: string;
  from?:   string;
  to?:     string;
}) =>
  downloadExport(
    'vehicle-register',
    { format: opts.format ?? 'excel', ...opts },
    `Vehicle_Maintenance_Register_${today()}.${ext(opts.format)}`
  );

/** Equipment maintenance register */
export const exportEquipmentMaintenance = (opts: {
  format?: ExportFormat;
  status?: string;
  from?:   string;
  to?:     string;
}) =>
  downloadExport(
    'equipment-maintenance',
    { format: opts.format ?? 'excel', status: opts.status, from: opts.from, to: opts.to },
    `Equipment_Maintenance_Register_${today()}.${ext(opts.format)}`
  );

/** Facility maintenance register */
export const exportFacilityMaintenance = (opts: {
  format?: ExportFormat;
  status?: string;
  from?:   string;
  to?:     string;
}) =>
  downloadExport(
    'facility-maintenance',
    { format: opts.format ?? 'excel', status: opts.status, from: opts.from, to: opts.to },
    `Facility_Maintenance_Register_${today()}.${ext(opts.format)}`
  );

/** Daily parameter log */
export const exportDailyLog = (opts: {
  format?:   ExportFormat;
  location?: string;
  from?:     string;
  to?:       string;
}) =>
  downloadExport(
    'daily-log',
    { format: opts.format ?? 'excel', ...opts },
    `Daily_Parameter_Log_${today()}.${ext(opts.format)}`
  );

/** Diesel requisitions */
export const exportDieselRequisitions = (opts: {
  format?: ExportFormat;
  status?: string;
  from?:   string;
  to?:     string;
}) =>
  downloadExport(
    'diesel-requisitions',
    { format: opts.format ?? 'excel', status: opts.status, from: opts.from, to: opts.to },
    `Diesel_Requisitions_${today()}.${ext(opts.format)}`
  );

/** Maintenance schedules / scheduler report */
export const exportMaintenanceSchedules = (opts: {
  format?:   ExportFormat;
  category?: string;
  status?:   'overdue' | 'due-soon' | 'active' | 'all';
}) =>
  downloadExport(
    'maintenance-schedules',
    { format: opts.format ?? 'excel', category: opts.category, status: opts.status },
    `Maintenance_Schedules_${today()}.${ext(opts.format)}`
  );

/** Generator daily log */
export const exportGeneratorLog = (opts: {
  location?: string;
  assetNo?:  string;
  from?:     string;
  to?:       string;
}) =>
  downloadExport(
    'generator-log',
    { format: 'excel', ...opts },
    `Generator_Daily_Log_${today()}.xlsx`
  );

// ── Utils ─────────────────────────────────────────────────────────────────────

const today = () => new Date().toISOString().slice(0, 10).replace(/-/g, '');
const ext   = (fmt?: ExportFormat) => (fmt === 'pdf' ? 'pdf' : 'xlsx');
