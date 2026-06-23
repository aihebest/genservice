import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Alert, Badge, Button, Card, Checkbox, Divider, Empty,
  Popconfirm, Radio, Select, Space, Tag, Tooltip, Typography,
} from 'antd';
import {
  BellOutlined, CalendarOutlined, CarOutlined, CheckOutlined,
  ClockCircleOutlined, DeleteOutlined, FileProtectOutlined,
  FireOutlined, HomeOutlined, MailOutlined, ReloadOutlined,
  SettingOutlined, ShopOutlined, ThunderboltOutlined,
  ToolOutlined, WarningOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { notificationsApi } from '../../api/notifications.api';
import type { AppNotification } from '../../types';

dayjs.extend(relativeTime);

const { Title, Text } = Typography;

// ── Module meta ───────────────────────────────────────────────────────────────
const MODULE_META: Record<string, { icon: React.ReactNode; route: string; colour: string; label: string }> = {
  Requests:          { icon: <MailOutlined />,         route: '/requests',    colour: '#1677ff', label: 'Requests'          },
  Equipment:         { icon: <ToolOutlined />,          route: '/maintenance', colour: '#fa8c16', label: 'Equipment'         },
  Facility:          { icon: <HomeOutlined />,          route: '/maintenance', colour: '#52c41a', label: 'Facility'          },
  Vehicle:           { icon: <CarOutlined />,           route: '/fleet',       colour: '#722ed1', label: 'Vehicle'           },
  Maintenance:       { icon: <CalendarOutlined />,      route: '/maintenance', colour: '#13c2c2', label: 'Maintenance'       },
  Store:             { icon: <ShopOutlined />,          route: '/store',       colour: '#eb2f96', label: 'Store'             },
  Fuel:              { icon: <FireOutlined />,          route: '/fuel',        colour: '#ff7a45', label: 'Fuel'              },
  DieselRequisition: { icon: <FileProtectOutlined />,   route: '/fuel',        colour: '#d4b106', label: 'Diesel Req.'      },
};

type NotifLevel = 'info' | 'warning' | 'critical';

interface TypeMeta { icon: React.ReactNode; level: NotifLevel; label: string; }

const TYPE_META: Record<string, TypeMeta> = {
  RequestSubmitted:             { icon: <MailOutlined />,          level: 'info',     label: 'New Request'       },
  LineManagerApproved:          { icon: <CheckOutlined />,          level: 'info',     label: 'Approved'          },
  LineManagerRejected:          { icon: <WarningOutlined />,        level: 'warning',  label: 'Rejected'          },
  GsApproved:                   { icon: <CheckOutlined />,          level: 'info',     label: 'Approved'          },
  GsRejected:                   { icon: <WarningOutlined />,        level: 'warning',  label: 'Rejected'          },
  RequestAssigned:              { icon: <ToolOutlined />,           level: 'info',     label: 'Assigned'          },
  RequestCompleted:             { icon: <CheckOutlined />,          level: 'info',     label: 'Completed'         },
  MaintenancePending:           { icon: <ClockCircleOutlined />,    level: 'info',     label: 'New Maintenance'   },
  VehicleLongStanding:          { icon: <CarOutlined />,            level: 'warning',  label: 'Long-Standing'     },
  MaintenanceDueSoon:           { icon: <ClockCircleOutlined />,    level: 'info',     label: 'Due Soon'          },
  MaintenanceDueUrgent:         { icon: <WarningOutlined />,        level: 'warning',  label: 'Due Tomorrow'      },
  MaintenanceOverdue:           { icon: <WarningOutlined />,        level: 'warning',  label: 'Overdue'           },
  MaintenanceEscalationSupervisor: { icon: <ThunderboltOutlined />, level: 'critical', label: 'Escalated'         },
  MaintenanceEscalationManager:    { icon: <ThunderboltOutlined />, level: 'critical', label: '🆘 Escalated'     },
};

function getTypeMeta(type: string): TypeMeta {
  return TYPE_META[type] ?? { icon: <SettingOutlined />, level: 'info', label: type };
}

const LEVEL_STYLE: Record<NotifLevel, { bg: string; border: string; tagColour: string }> = {
  info:     { bg: '#f0f5ff', border: '#1677ff', tagColour: 'blue'   },
  warning:  { bg: '#fffbe6', border: '#faad14', tagColour: 'orange' },
  critical: { bg: '#fff1f0', border: '#ff4d4f', tagColour: 'red'    },
};

const PAGE_SIZE = 20;

// ── Notification card ─────────────────────────────────────────────────────────
function NotifCard({
  n,
  selected,
  onSelect,
  onMarkRead,
  onMarkUnread,
  onDelete,
  onNavigate,
}: {
  n: AppNotification;
  selected: boolean;
  onSelect: (id: string, checked: boolean) => void;
  onMarkRead:    (id: string) => void;
  onMarkUnread:  (id: string) => void;
  onDelete:      (id: string) => void;
  onNavigate:    (n: AppNotification) => void;
}) {
  const typeMeta   = getTypeMeta(n.type);
  const modMeta    = MODULE_META[n.module];
  const levelStyle = n.isRead ? { bg: '#fff', border: '#f0f0f0', tagColour: 'default' as const } : LEVEL_STYLE[typeMeta.level];

  return (
    <div
      style={{
        display:      'flex',
        alignItems:   'flex-start',
        gap:          12,
        padding:      '14px 16px',
        background:   levelStyle.bg,
        borderLeft:   `4px solid ${n.isRead ? 'transparent' : levelStyle.border}`,
        borderBottom: '1px solid #f0f0f0',
        transition:   'background 0.15s',
      }}
    >
      {/* Checkbox */}
      <Checkbox
        checked={selected}
        onChange={e => onSelect(n.id, e.target.checked)}
        style={{ marginTop: 3 }}
      />

      {/* Module icon */}
      <div
        style={{
          width: 40, height: 40, borderRadius: 10, flexShrink: 0,
          background: modMeta ? `${modMeta.colour}18` : '#f5f5f5',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 18, color: modMeta?.colour ?? '#888',
        }}
      >
        {modMeta?.icon ?? typeMeta.icon}
      </div>

      {/* Body */}
      <div style={{ flex: 1, minWidth: 0, cursor: 'pointer' }} onClick={() => onNavigate(n)}>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
          <Text strong={!n.isRead} style={{ fontSize: 14 }}>
            {n.title}
          </Text>
          <Text type="secondary" style={{ fontSize: 12, flexShrink: 0 }}>
            {dayjs(n.createdAt).fromNow()}
          </Text>
        </div>
        <Text type="secondary" style={{ fontSize: 13, display: 'block', marginTop: 2 }}>
          {n.message}
        </Text>
        <Space size={4} style={{ marginTop: 6 }}>
          <Tag
            color={levelStyle.tagColour}
            style={{ fontSize: 11, lineHeight: '18px' }}
          >
            {typeMeta.label}
          </Tag>
          {modMeta && (
            <Tag
              style={{
                fontSize: 11, lineHeight: '18px',
                color: modMeta.colour, borderColor: `${modMeta.colour}44`,
                background: `${modMeta.colour}10`,
              }}
            >
              {modMeta.label}
            </Tag>
          )}
          {n.refNumber && (
            <Tag style={{ fontSize: 11, lineHeight: '18px' }}>{n.refNumber}</Tag>
          )}
          {!n.isRead && (
            <Badge status="processing" color={levelStyle.border} />
          )}
        </Space>
      </div>

      {/* Actions */}
      <Space size={4} style={{ flexShrink: 0 }}>
        {n.isRead ? (
          <Tooltip title="Mark unread">
            <Button
              size="small" type="text" icon={<MailOutlined />}
              onClick={() => onMarkUnread(n.id)}
            />
          </Tooltip>
        ) : (
          <Tooltip title="Mark read">
            <Button
              size="small" type="text" icon={<CheckOutlined />}
              style={{ color: '#52c41a' }}
              onClick={() => onMarkRead(n.id)}
            />
          </Tooltip>
        )}
        <Popconfirm
          title="Delete this notification?"
          onConfirm={() => onDelete(n.id)}
          okText="Delete" okButtonProps={{ danger: true }}
        >
          <Tooltip title="Delete">
            <Button
              size="small" type="text" icon={<DeleteOutlined />}
              style={{ color: '#ff4d4f' }}
            />
          </Tooltip>
        </Popconfirm>
      </Space>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function NotificationsPage() {
  const qc       = useQueryClient();
  const navigate = useNavigate();

  const [page,        setPage]        = useState(1);
  const [readFilter,  setReadFilter]  = useState<'all' | 'unread' | 'read'>('all');
  const [moduleFilter, setModuleFilter] = useState<string | undefined>(undefined);
  const [selected,    setSelected]    = useState<Set<string>>(new Set());

  const { data, isFetching } = useQuery({
    queryKey: ['notifications', 'page', page, readFilter, moduleFilter],
    queryFn: () => notificationsApi.list({
      take:       PAGE_SIZE,
      page,
      unreadOnly: readFilter === 'unread' || undefined,
      module:     moduleFilter,
    }),
    placeholderData: prev => prev,
  });

  const { data: moduleList } = useQuery({
    queryKey: ['notifications', 'modules'],
    queryFn:  () => notificationsApi.modules(),
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ['notifications'] });

  const readMut = useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onSuccess: invalidate,
  });
  const unreadMut = useMutation({
    mutationFn: (id: string) => notificationsApi.markUnread(id),
    onSuccess: invalidate,
  });
  const deleteMut = useMutation({
    mutationFn: (id: string) => notificationsApi.delete(id),
    onSuccess: invalidate,
  });
  const allReadMut = useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess: invalidate,
  });
  const clearReadMut = useMutation({
    mutationFn: () => notificationsApi.clearRead(),
    onSuccess: invalidate,
  });

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const unreadCount = data?.unreadCount ?? 0;

  const allSelected = items.length > 0 && items.every(n => selected.has(n.id));
  const someSelected = selected.size > 0;

  const toggleSelect = (id: string, checked: boolean) => {
    setSelected(prev => {
      const next = new Set(prev);
      checked ? next.add(id) : next.delete(id);
      return next;
    });
  };

  const toggleAll = (checked: boolean) => {
    if (checked) setSelected(new Set(items.map(n => n.id)));
    else         setSelected(new Set());
  };

  const handleNavigate = (n: AppNotification) => {
    readMut.mutate(n.id);
    const modMeta = MODULE_META[n.module];
    if (modMeta) navigate(modMeta.route);
  };

  const handleBulkRead = async () => {
    await Promise.all([...selected].map(id => notificationsApi.markRead(id)));
    setSelected(new Set());
    invalidate();
  };

  const handleBulkDelete = async () => {
    await Promise.all([...selected].map(id => notificationsApi.delete(id)));
    setSelected(new Set());
    invalidate();
  };

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <div style={{ maxWidth: 860, margin: '0 auto', paddingBottom: 40 }}>
      {/* ── Page header ───────────────────────────────────────────── */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 20 }}>
        <div>
          <Title level={4} style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
            <BellOutlined style={{ color: '#1677ff' }} />
            Notifications
            {unreadCount > 0 && (
              <Badge count={unreadCount} style={{ backgroundColor: '#1677ff', fontSize: 11 }} />
            )}
          </Title>
          <Text type="secondary" style={{ fontSize: 13, marginTop: 2, display: 'block' }}>
            All system alerts, approvals, and maintenance reminders
          </Text>
        </div>

        <Space>
          <Button
            icon={<ReloadOutlined />}
            loading={isFetching}
            onClick={invalidate}
          >
            Refresh
          </Button>
          {unreadCount > 0 && (
            <Button
              icon={<CheckOutlined />}
              loading={allReadMut.isPending}
              onClick={() => allReadMut.mutate()}
            >
              Mark all read
            </Button>
          )}
          <Popconfirm
            title={`Delete all read notifications?`}
            description="This cannot be undone."
            onConfirm={() => clearReadMut.mutate()}
            okText="Clear" okButtonProps={{ danger: true }}
          >
            <Button icon={<DeleteOutlined />} danger loading={clearReadMut.isPending}>
              Clear read
            </Button>
          </Popconfirm>
        </Space>
      </div>

      {/* ── Filters ───────────────────────────────────────────────── */}
      <Card bodyStyle={{ padding: '12px 16px' }} style={{ marginBottom: 12, borderRadius: 10 }}>
        <Space wrap>
          <Text type="secondary" style={{ fontSize: 13 }}>Show:</Text>
          <Radio.Group
            value={readFilter}
            onChange={e => { setReadFilter(e.target.value); setPage(1); }}
            optionType="button"
            buttonStyle="solid"
            size="small"
          >
            <Radio.Button value="all">All</Radio.Button>
            <Radio.Button value="unread">
              Unread {unreadCount > 0 ? `(${unreadCount})` : ''}
            </Radio.Button>
            <Radio.Button value="read">Read</Radio.Button>
          </Radio.Group>

          <Divider type="vertical" />

          <Select
            allowClear
            placeholder="All modules"
            size="small"
            style={{ width: 160 }}
            value={moduleFilter}
            onChange={v => { setModuleFilter(v); setPage(1); }}
            options={(moduleList ?? []).map(m => ({
              value: m,
              label: (
                <Space size={4}>
                  {MODULE_META[m]?.icon ?? <SettingOutlined />}
                  {MODULE_META[m]?.label ?? m}
                </Space>
              ),
            }))}
          />
        </Space>
      </Card>

      {/* ── Bulk action bar (appears when items are selected) ───── */}
      {someSelected && (
        <Alert
          type="info"
          showIcon={false}
          style={{ marginBottom: 8, borderRadius: 8, padding: '8px 16px' }}
          message={
            <Space>
              <Text strong>{selected.size} selected</Text>
              <Button size="small" icon={<CheckOutlined />} onClick={handleBulkRead}>
                Mark read
              </Button>
              <Popconfirm
                title={`Delete ${selected.size} notification(s)?`}
                onConfirm={handleBulkDelete}
                okText="Delete" okButtonProps={{ danger: true }}
              >
                <Button size="small" icon={<DeleteOutlined />} danger>
                  Delete
                </Button>
              </Popconfirm>
              <Button size="small" type="link" onClick={() => setSelected(new Set())}>
                Cancel
              </Button>
            </Space>
          }
        />
      )}

      {/* ── Table header row ──────────────────────────────────────── */}
      <Card
        bodyStyle={{ padding: 0 }}
        style={{ borderRadius: 10, overflow: 'hidden' }}
      >
        {/* Select-all row */}
        <div style={{
          padding: '10px 16px',
          borderBottom: '1px solid #f0f0f0',
          background: '#fafafa',
          display: 'flex',
          alignItems: 'center',
          gap: 12,
        }}>
          <Checkbox
            checked={allSelected}
            indeterminate={someSelected && !allSelected}
            onChange={e => toggleAll(e.target.checked)}
          />
          <Text type="secondary" style={{ fontSize: 12 }}>
            {total > 0
              ? `Showing ${(page - 1) * PAGE_SIZE + 1}–${Math.min(page * PAGE_SIZE, total)} of ${total} notification${total !== 1 ? 's' : ''}`
              : 'No notifications'}
          </Text>
        </div>

        {/* Notification list */}
        {items.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              readFilter === 'unread' ? "You're all caught up — no unread notifications!"
              : readFilter === 'read'  ? 'No read notifications found'
              : 'No notifications yet'
            }
            style={{ padding: '48px 0' }}
          />
        ) : (
          items.map(n => (
            <NotifCard
              key={n.id}
              n={n}
              selected={selected.has(n.id)}
              onSelect={toggleSelect}
              onMarkRead={id => readMut.mutate(id)}
              onMarkUnread={id => unreadMut.mutate(id)}
              onDelete={id => deleteMut.mutate(id)}
              onNavigate={handleNavigate}
            />
          ))
        )}

        {/* Pagination footer */}
        {totalPages > 1 && (
          <div style={{
            padding: '12px 16px',
            borderTop: '1px solid #f0f0f0',
            display: 'flex',
            justifyContent: 'center',
            gap: 8,
          }}>
            <Button
              size="small"
              disabled={page === 1}
              onClick={() => setPage(p => p - 1)}
            >
              ← Previous
            </Button>
            <Text type="secondary" style={{ lineHeight: '24px', fontSize: 13 }}>
              Page {page} of {totalPages}
            </Text>
            <Button
              size="small"
              disabled={page >= totalPages}
              onClick={() => setPage(p => p + 1)}
            >
              Next →
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}
