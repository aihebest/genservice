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
  id:             string;
  taskName:       string;
  category:       string;
  location:       string;
  isOverdue:      boolean;
  daysUntilDue:   number;
  frequencyLabel: string;
  assignedToName?: string;
  nextDueAt:      string;
}

export interface DashboardSummary {
  // KPIs
  requestsOpen:            number;
  requestsPendingApproval: number;
  requestsInProgress:      number;
  requestsCompletedToday:  number;
  requestsTotal:           number;
  staffActiveNow:          number;
  maintenanceOverdue:      number;
  maintenanceDueSoon:      number;
  // Panels
  recentRequests:      DashboardRequestItem[];
  pendingApprovals:    DashboardRequestItem[];
  activeStaff:         DashboardActivityItem[];
  upcomingMaintenance: DashboardMaintenanceItem[];
}

export const dashboardApi = {
  summary: () =>
    apiClient.get<DashboardSummary>('/dashboard/summary').then(r => r.data),
};
