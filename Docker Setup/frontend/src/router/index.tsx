import React from 'react';
import { createBrowserRouter, Navigate } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import AppShell from '../components/layout/AppShell';
import LoginPage from '../pages/auth/LoginPage';
import DashboardPage from '../pages/dashboard/DashboardPage';
import RequestsPage from '../pages/requests/RequestsPage';
import FleetPage from '../pages/fleet/FleetPage';
import ActivitiesPage from '../pages/activities/ActivitiesPage';
import MaintenancePage from '../pages/maintenance/MaintenancePage';
import FuelPage from '../pages/fuel/FuelPage';
import ReportsPage from '../pages/reports/ReportsPage';
import DailyLogPage from '../pages/daily-log/DailyLogPage';
import FeedingAccommodationPage from '../pages/accommodation/FeedingAccommodationPage';
import UserManagementPage from '../pages/users/UserManagementPage';
import StoreManagementPage from '../pages/store/StoreManagementPage';
import NotificationsPage from '../pages/notifications/NotificationsPage';
import ElectricityPage from '../pages/electricity/ElectricityPage';
import DstvPage from '../pages/dstv/DstvPage';
import VehicleRegistryPage from '../pages/vehicle-registry/VehicleRegistryPage';
import DieselSupplyPage from '../pages/diesel-supply/DieselSupplyPage';

// ── Auth guard ────────────────────────────────────────────────────────────────
function RequireAuth({ children }: { children: React.ReactNode }) {
  const isAuth = useAuthStore((s) => s.isAuth);
  if (!isAuth) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

// ── Router ────────────────────────────────────────────────────────────────────
export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      { index: true,           element: <Navigate to="/dashboard" replace /> },
      { path: 'dashboard',     element: <DashboardPage /> },
      { path: 'requests',      element: <RequestsPage /> },
      { path: 'fleet',         element: <FleetPage /> },
      { path: 'activities',    element: <ActivitiesPage /> },
      { path: 'maintenance',   element: <MaintenancePage /> },
      { path: 'fuel',          element: <FuelPage /> },
      { path: 'electricity',   element: <ElectricityPage /> },
      { path: 'dstv',          element: <DstvPage /> },
      { path: 'vehicle-registry', element: <VehicleRegistryPage /> },
      { path: 'diesel-supply', element: <DieselSupplyPage /> },
      { path: 'daily-log',     element: <DailyLogPage /> },
      { path: 'feeding-accommodation', element: <FeedingAccommodationPage /> },
      { path: 'users',         element: <UserManagementPage /> },
      { path: 'store',         element: <StoreManagementPage /> },
      { path: 'reports',       element: <ReportsPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
    ],
  },
  {
    path: '*',
    element: <Navigate to="/dashboard" replace />,
  },
]);
