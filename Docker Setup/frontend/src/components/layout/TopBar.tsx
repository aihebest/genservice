import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Avatar, Badge, Button, Divider, Dropdown, Empty, List,
  Space, Tag, Tooltip, Typography,
} from 'antd';
import type { MenuProps } from 'antd';
import {
  BellOutlined, LogoutOutlined, UserOutlined,
  CheckOutlined, MailOutlined, ToolOutlined,
  CarOutlined, HomeOutlined, SettingOutlined,
  ShopOutlined, ThunderboltOutlined, FireOutlined,
  WarningOutlined, CalendarOutlined, ClockCircleOutlined,
  FileProtectOutlined, RightOutlined, MenuOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { useAuthStore } from '../../store/authStore';
import { notificationsApi } from '../../api/notifications.api';
import type { AppNotification } from '../../types';

dayjs.extend(relativeTime);

const { Text } = Typography;

// ── Role colours ──────────────────────────────────────────────────────────────
const ROLE_COLOURS: Record<string, string> = {
  SystemAdmin:       'red',
  DepartmentManager: 'purple',
  Supervisor:        'blue',
  Technician:        'cyan',
  Driver:            'green',
  Requester:         'default',
  StoreOfficer:      'orange',
};

// ── Module → icon + route ─────────────────────────────────────────────────────
const MODULE_META: Record<string, { icon: React.ReactNode; route: string; colour: string }> = {
  Requests:           { icon: <MailOutlined />,         route: '/requests',    colour: '#1677ff' },
  Equipment:          { icon: <ToolOutlined />,          route: '/maintenance', colour: '#fa8c16' },
  Facility:           { icon: <HomeOutlined />,          route: '/maintenance', colour: '#52c41a' },
  Vehicle:            { icon: <CarOutlined />,           route: '/fleet',       colour: '#722ed1' },
  Maintenance:        { icon: <CalendarOutlined />,      route: '/maintenance', colour: '#13c2c2' },
  Store:              { icon: <ShopOutlined />,          route: '/store',       colour: '#eb2f96' },
  Fuel:               { icon: <FireOutlined />,          route: '/fuel',        colour: '#ff7a45' },
  DieselRequisition:  { icon: <FileProtectOutlined />,   route: '/fuel',        colour: '#d4b106' },
};

// ── Notification type → visual metadata ───────────────────────────────────────
type NotifLevel = 'info' | 'warning' | 'critical';

interface TypeMeta {
  icon:   React.ReactNode;
  level:  NotifLevel;
  label:  string;
}

const TYPE_META: Record<string, TypeMeta> = {
  // Requests
  RequestSubmitted:    { icon: <MailOutlined />,          level: 'info',     label: 'New Request'      },
  LineManagerApproved: { icon: <CheckOutlined />,          level: 'info',     label: 'Approved'         },
  LineManagerRejected: { icon: <WarningOutlined />,        level: 'warning',  label: 'Rejected'         },
  GsApproved:          { icon: <CheckOutlined />,          level: 'info',     label: 'Approved'         },
  GsRejected:          { icon: <WarningOutlined />,        level: 'warning',  label: 'Rejected'         },
  RequestAssigned:     { icon: <ToolOutlined />,           level: 'info',     label: 'Assigned'         },
  RequestCompleted:    { icon: <CheckOutlined />,          level: 'info',     label: 'Completed'        },
  MaintenancePending:  { icon: <ClockCircleOutlined />,    level: 'info',     label: 'New Maintenance'  },
  VehicleLongStanding: { icon: <CarOutlined />,            level: 'warning',  label: 'Long-Standing'    },
  // Maintenance reminders
  MaintenanceDueSoon:             { icon: <ClockCircleOutlined />,  level: 'info',     label: 'Due Soon'         },
  MaintenanceDueUrgent:           { icon: <WarningOutlined />,      level: 'warning',  label: 'Due Tomorrow'     },
  MaintenanceOverdue:             { icon: <WarningOutlined />,      level: 'warning',  label: 'Overdue'          },
  MaintenanceEscalationSupervisor:{ icon: <ThunderboltOutlined />,  level: 'critical', label: 'Escalated'        },
  MaintenanceEscalationManager:   { icon: <ThunderboltOutlined />,  level: 'critical', label: '🆘 Escalated'    },
};

function getTypeMeta(type: string): TypeMeta {
  return TYPE_META[type] ?? { icon: <SettingOutlined />, level: 'info', label: type };
}

// ── Level → row colours ───────────────────────────────────────────────────────
const LEVEL_STYLE: Record<NotifLevel, { bg: string; border: string }> = {
  info:     { bg: '#f0f5ff', border: '#1677ff' },
  warning:  { bg: '#fffbe6', border: '#faad14' },
  critical: { bg: '#fff1f0', border: '#ff4d4f' },
};

// ── ENV badge ─────────────────────────────────────────────────────────────────
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

// ── Notification Bell ─────────────────────────────────────────────────────────
function NotificationBell() {
  const [open, setOpen]       = useState(false);
  const qc                    = useQueryClient();
  const navigate              = useNavigate();

  const { data } = useQuery({
    queryKey: ['notifications', 'bell'],
    queryFn:  () => notificationsApi.list({ take: 15 }),
    refetchInterval: 30_000,
  });

  const unread = data?.unreadCount ?? 0;

  const readMut = useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onSuccess:  () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  });

  const allReadMut = useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess:  () => qc.invalidateQueries({ queryKey: ['notifications'] }),
  });

  const handleClick = (n: AppNotification) => {
    // Mark read
    if (!n.isRead) readMut.mutate(n.id);

    // Navigate to the relevant page
    const meta = MODULE_META[n.module];
    if (meta) navigate(meta.route);

    setOpen(false);
  };

  const handleViewAll = () => {
    setOpen(false);
    navigate('/notifications');
  };

  const items = data?.items ?? [];

  const dropdownContent = (
    <div style={{
      width: 380,
      background: '#fff',
      borderRadius: 10,
      boxShadow: '0 8px 32px rgba(0,0,0,0.14)',
      overflow: 'hidden',
    }}>
      {/* ── Header ──────────────────────────────────────────── */}
      <div style={{
        padding: '13px 16px',
        borderBottom: '1px solid #f0f0f0',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        background: '#fafafa',
      }}>
        <Space size={8}>
          <Text strong style={{ fontSize: 14 }}>Notifications</Text>
          {unread > 0 && (
            <Badge count={unread} size="small" color="#1677ff" />
          )}
        </Space>
        {unread > 0 && (
          <Button
            type="link"
            size="small"
            icon={<CheckOutlined />}
            loading={allReadMut.isPending}
            onClick={() => allReadMut.mutate()}
            style={{ padding: 0, fontSize: 12, color: '#888' }}
          >
            Mark all read
          </Button>
        )}
      </div>

      {/* ── List ────────────────────────────────────────────── */}
      <div style={{ maxHeight: 420, overflowY: 'auto' }}>
        {items.length === 0 ? (
          <Empty
            description="You're all caught up"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            style={{ padding: '36px 0' }}
          />
        ) : (
          <List
            dataSource={items}
            split={false}
            renderItem={(n: AppNotification) => {
              const typeMeta   = getTypeMeta(n.type);
              const modMeta    = MODULE_META[n.module];
              const levelStyle = n.isRead
                ? { bg: '#fff', border: 'transparent' }
                : LEVEL_STYLE[typeMeta.level];

              return (
                <List.Item
                  key={n.id}
                  onClick={() => handleClick(n)}
                  style={{
                    padding:          '10px 16px',
                    cursor:           'pointer',
                    background:       levelStyle.bg,
                    borderLeft:       `3px solid ${n.isRead ? 'transparent' : levelStyle.border}`,
                    borderBottom:     '1px solid #f5f5f5',
                    transition:       'background 0.15s',
                    alignItems:       'flex-start',
                  }}
                >
                  {/* Module icon */}
                  <div style={{
                    width: 34, height: 34, borderRadius: 8, flexShrink: 0,
                    background: modMeta ? `${modMeta.colour}18` : '#f5f5f5',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 16,
                    color: modMeta?.colour ?? '#888',
                    marginRight: 10, marginTop: 1,
                  }}>
                    {modMeta?.icon ?? typeMeta.icon}
                  </div>

                  <div style={{ flex: 1, minWidth: 0 }}>
                    {/* Title row */}
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 6 }}>
                      <Text
                        style={{ fontSize: 13, fontWeight: n.isRead ? 400 : 600, lineHeight: 1.35 }}
                        ellipsis={{ tooltip: n.title }}
                      >
                        {n.title}
                      </Text>
                      {!n.isRead && (
                        <div style={{
                          width: 7, height: 7, borderRadius: '50%',
                          background: levelStyle.border, flexShrink: 0, marginTop: 4,
                        }} />
                      )}
                    </div>

                    {/* Message */}
                    <Text
                      type="secondary"
                      style={{ fontSize: 12, display: 'block', lineHeight: 1.4, marginTop: 2 }}
                      ellipsis={{ tooltip: n.message }}
                    >
                      {n.message}
                    </Text>

                    {/* Footer: time + ref + type label */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 4 }}>
                      <Text type="secondary" style={{ fontSize: 11 }}>
                        {dayjs(n.createdAt).fromNow()}
                      </Text>
                      {n.refNumber && (
                        <Tag style={{ fontSize: 10, lineHeight: '16px', padding: '0 4px' }}>
                          {n.refNumber}
                        </Tag>
                      )}
                      <Tag
                        color={
                          typeMeta.level === 'critical' ? 'red'
                          : typeMeta.level === 'warning' ? 'orange'
                          : 'blue'
                        }
                        style={{ fontSize: 10, lineHeight: '16px', padding: '0 4px' }}
                      >
                        {typeMeta.label}
                      </Tag>
                    </div>
                  </div>
                </List.Item>
              );
            }}
          />
        )}
      </div>

      {/* ── Footer ──────────────────────────────────────────── */}
      <Divider style={{ margin: 0 }} />
      <div
        onClick={handleViewAll}
        style={{
          padding: '10px 16px',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          gap: 6,
          cursor: 'pointer',
          color: '#1677ff',
          fontSize: 13,
          fontWeight: 500,
          transition: 'background 0.15s',
        }}
        onMouseEnter={e => (e.currentTarget.style.background = '#f0f5ff')}
        onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
      >
        View all notifications <RightOutlined style={{ fontSize: 11 }} />
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
        <Badge
          count={unread}
          size="small"
          overflowCount={99}
          color={unread > 0 ? '#1677ff' : undefined}
        >
          <BellOutlined
            style={{
              fontSize: 19,
              cursor: 'pointer',
              color: unread > 0 ? '#1677ff' : '#595959',
              transition: 'color 0.2s',
            }}
            onClick={() => setOpen(!open)}
          />
        </Badge>
      </Tooltip>
    </Dropdown>
  );
}

// ── Main TopBar ───────────────────────────────────────────────────────────────
interface TopBarProps {
  onMenuClick?: () => void;
}

export default function TopBar({ onMenuClick }: TopBarProps) {
  const { user, logout } = useAuthStore();

  const userMenuItems: MenuProps['items'] = [
    { key: 'profile', icon: <UserOutlined />, label: 'My Profile',  disabled: true },
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
      {/* Hamburger — only visible on mobile (≤768px) */}
      {onMenuClick && (
        <Button
          type="text"
          icon={<MenuOutlined />}
          onClick={onMenuClick}
          style={{
            display: 'block',
            fontSize: 18,
            flexShrink: 0,
          }}
          className="mobile-menu-btn"
        />
      )}
      <div style={{ flex: 1 }}>
        <Text strong style={{ fontSize: 16, color: '#141414' }}>
          General Service Management
        </Text>
      </div>

      <Space size={20} align="center">
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
