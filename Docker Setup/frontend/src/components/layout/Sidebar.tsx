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
  FileTextOutlined,
  UserOutlined,
  ShopOutlined,
  BellOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '../../store/authStore';
import type { UserRole } from '../../types';

interface NavItem {
  key:   string;
  label: string;
  icon:  React.ReactNode;
  roles: UserRole[] | 'all';
}

const NAV_ITEMS: NavItem[] = [
  { key: '/dashboard',  label: 'Dashboard',                   icon: <DashboardOutlined />,    roles: 'all' },
  { key: '/requests',   label: 'Requests',                    icon: <FormOutlined />,         roles: 'all' },
  { key: '/fleet',      label: 'Vehicle Maintenance Register', icon: <CarOutlined />,          roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Technician', 'Driver'] },
  { key: '/activities', label: 'Team Activities',             icon: <TeamOutlined />,         roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Technician'] },
  { key: '/maintenance',label: 'Maintenance',                 icon: <ToolOutlined />,         roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Technician'] },
  { key: '/fuel',       label: 'Fuel & Power',                icon: <ThunderboltOutlined />,  roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Driver'] },
  { key: '/daily-log',  label: 'Daily Parameter Log',         icon: <FileTextOutlined />,     roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'Technician', 'Driver'] },
  { key: '/store',      label: 'Store Management',            icon: <ShopOutlined />,         roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'StoreOfficer', 'Technician', 'Driver', 'Requester'] },
  { key: '/users',      label: 'User Management',             icon: <UserOutlined />,         roles: ['SystemAdmin', 'DepartmentManager'] },
  { key: '/reports',    label: 'Reports',                     icon: <BarChartOutlined />,     roles: ['SystemAdmin', 'DepartmentManager', 'Supervisor'] },
  { key: '/notifications', label: 'Notifications',           icon: <BellOutlined />,         roles: 'all' },
];

interface SidebarProps {
  collapsed: boolean;
  onItemClick?: () => void;
}

export default function Sidebar({ collapsed, onItemClick }: SidebarProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const role     = useAuthStore((s) => s.user?.role);

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

  const selectedKey = '/' + (location.pathname.split('/')[1] || 'dashboard');

  const handleClick: MenuProps['onClick'] = ({ key }) => {
    navigate(key);
    onItemClick?.();
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
