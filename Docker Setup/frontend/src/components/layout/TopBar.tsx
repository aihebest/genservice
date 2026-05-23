import { Avatar, Badge, Dropdown, Space, Tag, Typography } from 'antd';
import type { MenuProps } from 'antd';
import {
  BellOutlined,
  LogoutOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '../../store/authStore';

const { Text } = Typography;

// ── Role colour map ────────────────────────────────────────────────────────
const ROLE_COLOURS: Record<string, string> = {
  SystemAdmin:       'red',
  DepartmentManager: 'purple',
  Supervisor:        'blue',
  Technician:        'cyan',
  Driver:            'green',
  Requester:         'default',
  StoreOfficer:      'orange',
};

// ── Env badge (reads VITE_ENV_LABEL) ──────────────────────────────────────
const ENV_LABEL = import.meta.env.VITE_ENV_LABEL as string | undefined;

function EnvBadge() {
  if (!ENV_LABEL || ENV_LABEL === 'production') return null;
  const colour = ENV_LABEL === 'dev' ? 'volcano' : ENV_LABEL === 'demo' ? 'gold' : 'geekblue';
  return (
    <Tag color={colour} style={{ marginRight: 8, textTransform: 'uppercase', fontWeight: 600 }}>
      {ENV_LABEL}
    </Tag>
  );
}

// ── Component ──────────────────────────────────────────────────────────────
export default function TopBar() {
  const { user, logout } = useAuthStore();

  const userMenuItems: MenuProps['items'] = [
    {
      key:     'profile',
      icon:    <UserOutlined />,
      label:   'My Profile',
      disabled: true,
    },
    { type: 'divider' },
    {
      key:     'logout',
      icon:    <LogoutOutlined />,
      label:   'Sign Out',
      danger:  true,
    },
  ];

  const handleMenuClick: MenuProps['onClick'] = ({ key }) => {
    if (key === 'logout') {
      logout();
      window.location.href = '/login';
    }
  };

  const initials = user?.fullName
    ? user.fullName.split(' ').map((w) => w[0]).join('').slice(0, 2).toUpperCase()
    : '?';

  return (
    <div style={{ display: 'flex', alignItems: 'center', width: '100%', gap: 12 }}>
      {/* ── Left: page title placeholder ── */}
      <div style={{ flex: 1 }}>
        <Text strong style={{ fontSize: 16, color: '#141414' }}>
          General Service Management
        </Text>
      </div>

      {/* ── Right: env badge, notifications, user ── */}
      <Space size={16} align="center">
        <EnvBadge />

        {/* Notifications bell (static for now) */}
        <Badge count={3} size="small">
          <BellOutlined style={{ fontSize: 18, cursor: 'pointer', color: '#595959' }} />
        </Badge>

        {/* User avatar + dropdown */}
        <Dropdown
          menu={{ items: userMenuItems, onClick: handleMenuClick }}
          placement="bottomRight"
          arrow
          trigger={['click']}
        >
          <Space style={{ cursor: 'pointer' }} size={8}>
            <Avatar
              size={34}
              style={{ background: '#1677ff', fontWeight: 700, fontSize: 13 }}
            >
              {initials}
            </Avatar>
            <span style={{ display: 'flex', flexDirection: 'column', lineHeight: 1.2 }}>
              <Text strong style={{ fontSize: 13 }}>{user?.fullName ?? 'User'}</Text>
              <Tag
                color={ROLE_COLOURS[user?.role ?? ''] ?? 'default'}
                style={{ marginTop: 2, fontSize: 10, lineHeight: '16px', padding: '0 4px' }}
              >
                {user?.role ?? ''}
              </Tag>
            </span>
          </Space>
        </Dropdown>
      </Space>
    </div>
  );
}
