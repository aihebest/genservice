import { useState } from 'react';
import {
  Avatar, Badge, Button, Dropdown, Empty, List,
  Space, Tag, Tooltip, Typography,
} from 'antd';
import type { MenuProps } from 'antd';
import {
  BellOutlined, LogoutOutlined, UserOutlined,
  CheckOutlined, MailOutlined, ToolOutlined,
  CarOutlined, HomeOutlined, SettingOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { useAuthStore } from '../../store/authStore';
import { notificationsApi } from '../../api/notifications.api';
import type { AppNotification } from '../../types';

dayjs.extend(relativeTime);

const { Text } = Typography;

const ROLE_COLOURS: Record<string, string> = {
  SystemAdmin:       'red',
  DepartmentManager: 'purple',
  Supervisor:        'blue',
  Technician:        'cyan',
  Driver:            'green',
  Requester:         'default',
  StoreOfficer:      'orange',
};

const MODULE_ICON: Record<string, React.ReactNode> = {
  Requests:  <MailOutlined  style={{ color: '#1677ff' }} />,
  Equipment: <ToolOutlined  style={{ color: '#fa8c16' }} />,
  Facility:  <HomeOutlined  style={{ color: '#52c41a' }} />,
  Vehicle:   <CarOutlined   style={{ color: '#722ed1' }} />,
};

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

// ── Notification dropdown ─────────────────────────────────────────────────────
function NotificationBell() {
  const [open, setOpen] = useState(false);
  const qc = useQueryClient();

  const { data } = useQuery({
    queryKey: ['notifications'],
    queryFn:  () => notificationsApi.list(false, 15),
    refetchInterval: 30_000,
  });

  const unread = data?.unreadCount ?? 0;

  const handleMarkRead = async (id: string) => {
    await notificationsApi.markRead(id);
    qc.invalidateQueries({ queryKey: ['notifications'] });
  };

  const handleMarkAll = async () => {
    await notificationsApi.markAllRead();
    qc.invalidateQueries({ queryKey: ['notifications'] });
  };

  const dropdownContent = (
    <div style={{
      width: 360, background: '#fff', borderRadius: 8,
      boxShadow: '0 6px 24px rgba(0,0,0,0.12)', overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding: '12px 16px', borderBottom: '1px solid #f0f0f0',
        display: 'flex', justifyContent: 'space-between', alignItems: 'center',
      }}>
        <Text strong>Notifications {unread > 0 && <Badge count={unread} size="small" style={{ marginLeft: 6 }} />}</Text>
        {unread > 0 && (
          <Button type="link" size="small" icon={<CheckOutlined />} onClick={handleMarkAll}
            style={{ padding: 0, fontSize: 12 }}>
            Mark all read
          </Button>
        )}
      </div>

      {/* List */}
      <div style={{ maxHeight: 400, overflowY: 'auto' }}>
        {(!data?.items || data.items.length === 0) ? (
          <Empty description="No notifications" image={Empty.PRESENTED_IMAGE_SIMPLE}
            style={{ padding: '32px 0' }} />
        ) : (
          <List
            dataSource={data.items}
            renderItem={(n: AppNotification) => (
              <List.Item
                key={n.id}
                onClick={() => !n.isRead && handleMarkRead(n.id)}
                style={{
                  padding: '10px 16px',
                  cursor: n.isRead ? 'default' : 'pointer',
                  background: n.isRead ? '#fff' : '#f0f5ff',
                  borderBottom: '1px solid #f5f5f5',
                  transition: 'background 0.2s',
                }}
              >
                <List.Item.Meta
                  avatar={
                    <div style={{ marginTop: 2 }}>
                      {MODULE_ICON[n.module] ?? <SettingOutlined style={{ color: '#888' }} />}
                    </div>
                  }
                  title={
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                      <Text style={{ fontSize: 13, fontWeight: n.isRead ? 400 : 600 }} ellipsis>
                        {n.title}
                      </Text>
                      {!n.isRead && (
                        <div style={{ width: 7, height: 7, borderRadius: '50%',
                          background: '#1677ff', flexShrink: 0, marginTop: 5 }} />
                      )}
                    </div>
                  }
                  description={
                    <div>
                      <Text type="secondary" style={{ fontSize: 12, display: 'block' }} ellipsis>
                        {n.message}
                      </Text>
                      <Text type="secondary" style={{ fontSize: 11 }}>
                        {dayjs(n.createdAt).fromNow()}
                        {n.refNumber && <Tag style={{ marginLeft: 6, fontSize: 10 }}>{n.refNumber}</Tag>}
                      </Text>
                    </div>
                  }
                />
              </List.Item>
            )}
          />
        )}
      </div>
    </div>
  );

  return (
    <Dropdown
      open={open}
      onOpenChange={setOpen}
      dropdownRender={() => dropdownContent}
      trigger={['click']}
      placement="bottomRight"
    >
      <Tooltip title="Notifications">
        <Badge count={unread} size="small" overflowCount={99}>
          <BellOutlined
            style={{ fontSize: 18, cursor: 'pointer', color: '#595959' }}
            onClick={() => setOpen(!open)}
          />
        </Badge>
      </Tooltip>
    </Dropdown>
  );
}

// ── Main TopBar ───────────────────────────────────────────────────────────────
export default function TopBar() {
  const { user, logout } = useAuthStore();

  const userMenuItems: MenuProps['items'] = [
    { key: 'profile', icon: <UserOutlined />, label: 'My Profile', disabled: true },
    { type: 'divider' },
    { key: 'logout',  icon: <LogoutOutlined />, label: 'Sign Out', danger: true },
  ];

  const handleMenuClick: MenuProps['onClick'] = ({ key }) => {
    if (key === 'logout') { logout(); window.location.href = '/login'; }
  };

  const initials = user?.fullName
    ? user.fullName.split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase()
    : '?';

  return (
    <div style={{ display: 'flex', alignItems: 'center', width: '100%', gap: 12 }}>
      <div style={{ flex: 1 }}>
        <Text strong style={{ fontSize: 16, color: '#141414' }}>
          General Service Management
        </Text>
      </div>

      <Space size={16} align="center">
        <EnvBadge />
        <NotificationBell />

        <Dropdown
          menu={{ items: userMenuItems, onClick: handleMenuClick }}
          placement="bottomRight" arrow trigger={['click']}
        >
          <Space style={{ cursor: 'pointer' }} size={8}>
            <Avatar size={34} style={{ background: '#1677ff', fontWeight: 700, fontSize: 13 }}>
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
