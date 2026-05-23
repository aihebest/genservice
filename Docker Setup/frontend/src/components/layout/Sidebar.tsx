import { useMemo } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Menu } from 'antd';
import type { MenuProps } from 'antd';
import {
  DashboardOutlined,
  FormOutlined,
  CarOutlined,
  TeamOutlined,
  ToolOutlined,
  ThunderboltOutlined,
  BarChartOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '../../store/authStore';
import type { UserRole } from '../../types';

// ── Menu item definitions (with role whitelist) ────────────────────────────
interface NavItem {
  key:   string;
  label: string;
  icon:  React.ReactNode;
  roles: UserRole[] | 'all';
}

const NAV_ITEMS: NavItem[] = [
  {
    key:   '/dashboard',
    label: 'Dashboard',
    icon:  <DashboardOutlined />,
    roles: 'all',
  },
  {
    key:   '/requests',
    label: 'Requests',
    icon:  <FormOutlined />,
    roles: 'all',
  },
  {
    key:   '/fleet',
    label: 'Fleet & Vehicles',
    icon:  <CarOutlined />,
    roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Driver'],
  },
  {
    key:   '/activities',
    label: 'Team Activities',
    icon:  <TeamOutlined />,
    roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Technician'],
  },
  {
    key:   '/maintenance',
    label: 'Maintenance',
    icon:  <ToolOutlined />,
    roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Technician'],
  },
  {
    key:   '/fuel',
    label: 'Fuel & Power',
    icon:  <ThunderboltOutlined />,
    roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Driver'],
  },
  {
    key:   '/reports',
    label: 'Reports',
    icon:  <BarChartOutlined />,
    roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor'],
  },
];

// ── Component ──────────────────────────────────────────────────────────────
interface SidebarProps {
  collapsed: boolean;
}

export default function Sidebar({ collapsed }: SidebarProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const role     = useAuthStore((s) => s.user?.role);

  // Filter nav items by role
  const visibleItems: MenuProps['items'] = useMemo(() => {
    return NAV_ITEMS
      .filter((item) => {
        if (item.roles === 'all') return true;
        if (!role)               return false;
        return (item.roles as UserRole[]).includes(role);
      })
      .map((item) => ({
        key:   item.key,
        icon:  item.icon,
        label: item.label,
      }));
  }, [role]);

  // Active key — match first path segment
  const selectedKey = '/' + (location.pathname.split('/')[1] || 'dashboard');

  const handleClick: MenuProps['onClick'] = ({ key }) => {
    navigate(key);
  };

  return (
    <Menu
      theme="dark"
      mode="inline"
      selectedKeys={[selectedKey]}
      items={visibleItems}
      onClick={handleClick}
      inlineCollapsed={collapsed}
      style={{ borderInlineEnd: 'none', marginTop: 8 }}
    />
  );
}
