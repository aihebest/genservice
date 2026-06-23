import { apiClient } from './client';

// ── Types mirroring backend DashboardModels.cs ────────────────────────────────

export interface DashboardRequestItem {
  id:              string;
  ticketNumber:    string;
  title:           string;
  category:        string;
  status:          string;
  priority:        string;
  requestedByName: string;
  assignedToName?: string;
  createdAt:       string;
}

export interface DashboardActivityItem {
  id:                  string;
  staffName:           string;
  activityDescription: string;
  category:            string;
  location:            string;
  isProxy:             boolean;
  loggedByName:        string;
  startedAt:           string;
}

export interface DashboardMaintenanceItem {
  id:              string;
  taskName:        string;
  category:        string;
  location:        string;
  isOverdue:       boolean;
  daysUntilDue:    number;
  frequencyLabel:  string;
  assignedToName?: string;
  nextDueAt:       string;
  escalationLevel: number;  // 0=None, 1=Supervisor, 2=Manager
}

export interface DashboardStoreItem {
  id:             string;
  itemCode:       string;
  name:           string;
  category:       string;
  quantityInStock: number;
  reorderLevel:   number;
  unit:           string;
}

export interface DashboardDieselReqItem {
  id:                       string;
  requisitionNumber:        string;
  equipmentType:            string;
  purpose:                  string;
  quantityRequestedLitres:  number;
  quantityDispensedLitres?: number;
  status:                   string;
  requestedByName:          string;
  createdAt:                string;
}

export interface DashboardSummary {
  // ── Request KPIs
  requestsOpen:            number;
  requestsPendingApproval: number;
  requestsInProgress:      number;
  requestsCompletedToday:  number;
  requestsTotal:           number;

  // ── Staff KPI
  staffActiveNow: number;

  // ── Maintenance KPIs
  maintenanceOverdue:     number;
  maintenanceDueSoon:     number;
  maintenanceEscalations: number;

  // ── Store KPIs
  storeReqsPending:  number;
  storeLowStockCount: number;

  // ── Diesel / Fuel KPIs
  dieselReqsPending:              number;
  dieselDispensedThisMonthLitres: number;
  dieselTankLevelLitres:          number | null;

  // ── Panels
  recentRequests:          DashboardRequestItem[];
  pendingApprovals:        DashboardRequestItem[];
  activeStaff:             DashboardActivityItem[];
  upcomingMaintenance:     DashboardMaintenanceItem[];
  lowStockItems:           DashboardStoreItem[];
  recentDieselRequisitions: DashboardDieselReqItem[];
}

export const dashboardApi = {
  summary: () =>
    apiClient.get<DashboardSummary>('/dashboard/summary').then(r => r.data),
};
