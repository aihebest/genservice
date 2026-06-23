import { useState } from 'react';
import { Tabs, Typography, Row, Col } from 'antd';
import { ToolOutlined, HomeOutlined, CalendarOutlined } from '@ant-design/icons';
import EquipmentTab from './components/EquipmentTab';
import FacilityTab  from './components/FacilityTab';

const { Title, Text } = Typography;

export default function MaintenancePage() {
  const [activeTab, setActiveTab] = useState('equipment');

  return (
    <div>
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Maintenance Register</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            Equipment, Facility, and Scheduled maintenance tracking — E/26/001 · F/26/001
          </Text>
        </Col>
      </Row>

      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        type="card"
        size="middle"
        style={{ marginBottom: 0 }}
        items={[
          {
            key:      'equipment',
            label:    <span><ToolOutlined style={{ marginRight: 6 }} />Equipment Maintenance</span>,
            children: <EquipmentTab />,
          },
          {
            key:      'facility',
            label:    <span><HomeOutlined style={{ marginRight: 6 }} />Facility Maintenance</span>,
            children: <FacilityTab />,
          },
          {
            key:      'scheduler',
            label:    <span><CalendarOutlined style={{ marginRight: 6 }} />Maintenance Scheduler</span>,
            children: <SchedulerContent />,
          },
        ]}
      />
    </div>
  );
}

// ── Scheduler Tab (enhanced with escalation engine) ──────────────────────────

import { useCallback } from 'react';
import {
  Alert, Button, Card, Descriptions, Divider, Drawer, Dropdown,
  Form, InputNumber, Modal, Select, Space,
  Table, Tag, Timeline, Tooltip, message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  BellOutlined, EditOutlined,
  PlusOutlined, ReloadOutlined, WarningOutlined,
  ClockCircleOutlined, CheckCircleOutlined,
  ThunderboltOutlined, DownloadOutlined,
} from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { exportMaintenanceSchedules } from '../../api/export.api';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { maintenanceApi } from '../../api/maintenance.api';
import { MAINTENANCE_CATEGORY_META } from '../../types';
import type { MaintenanceSchedule, MaintenanceCategory } from '../../types';
import { useAuthStore } from '../../store/authStore';
import NewScheduleModal from './components/NewScheduleModal';

dayjs.extend(relativeTime);

// ── Escalation badge ──────────────────────────────────────────────────────────
function EscalationBadge({ level }: { level: number }) {
  if (level === 0) return null;
  if (level === 1) return <Tag color="orange" style={{ fontSize: 11 }}>⚠️ Supervisor</Tag>;
  return <Tag color="red" style={{ fontSize: 11 }}>🆘 Manager</Tag>;
}

// ── Column builder ────────────────────────────────────────────────────────────
function buildColumns(
  canManage:    boolean,
  onComplete:   (r: MaintenanceSchedule) => void,
  onEdit:       (r: MaintenanceSchedule) => void,
  onSnooze:     (r: MaintenanceSchedule) => void,
): ColumnsType<MaintenanceSchedule> {
  return [
    {
      title: 'Task',
      dataIndex: 'taskName',
      ellipsis: true,
      render: (v: string, r: MaintenanceSchedule) => (
        <div>
          <Text strong style={{ fontSize: 13 }}>{v}</Text>
          {r.isOverdue && <Tag color="red" style={{ marginLeft: 6, fontSize: 11 }}>OVERDUE</Tag>}
          <div style={{ marginTop: 2 }}>
            <EscalationBadge level={r.escalationLevel} />
          </div>
        </div>
      ),
    },
    {
      title: 'Category',
      dataIndex: 'category',
      width: 150,
      render: (v: MaintenanceCategory) => {
        const m = MAINTENANCE_CATEGORY_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Location',
      dataIndex: 'location',
      width: 160,
      ellipsis: true,
      render: (v: string) => <Text type="secondary" style={{ fontSize: 12 }}>{v || '—'}</Text>,
    },
    {
      title: 'Freq.',
      dataIndex: 'frequencyLabel',
      width: 95,
      render: (v: string) => <Tag>{v}</Tag>,
    },
    {
      title: 'Next Due',
      dataIndex: 'nextDueAt',
      width: 115,
      sorter: (a, b) => new Date(a.nextDueAt).getTime() - new Date(b.nextDueAt).getTime(),
      render: (v: string, r: MaintenanceSchedule) => {
        const d = dayjs(v);
        const diff = d.diff(dayjs(), 'day');
        const isOverdue = r.isOverdue;
        return (
          <Tooltip title={d.format('D MMM YYYY')}>
            <Text style={{
              color:      isOverdue ? '#ff4d4f' : diff <= 7 ? '#fa8c16' : undefined,
              fontSize:   12,
              fontWeight: isOverdue ? 600 : undefined,
            }}>
              {isOverdue
                ? `${Math.abs(diff)}d overdue`
                : diff === 0
                  ? 'Due today'
                  : `in ${diff}d`}
            </Text>
          </Tooltip>
        );
      },
    },
    {
      title: 'Assigned To',
      dataIndex: 'assignedToName',
      width: 130,
      ellipsis: true,
      render: (v?: string) => v
        ? <Text style={{ fontSize: 12 }}>{v}</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>Unassigned</Text>,
    },
    {
      title: 'Last Reminder',
      dataIndex: 'lastReminderSentAt',
      width: 120,
      render: (v?: string) => v
        ? <Tooltip title={dayjs(v).format('D MMM YYYY HH:mm')}><Text type="secondary" style={{ fontSize: 11 }}>{dayjs(v).fromNow()}</Text></Tooltip>
        : <Text type="secondary" style={{ fontSize: 11 }}>—</Text>,
    },
    ...(canManage ? [{
      title: 'Actions',
      key:   'actions',
      width: 170,
      fixed: 'right' as const,
      render: (_: unknown, r: MaintenanceSchedule) => (
        <Space size={4}>
          <Tooltip title="Mark as Done">
            <Button
              size="small"
              type="primary"
              icon={<CheckCircleOutlined />}
              style={{ background: '#52c41a', borderColor: '#52c41a' }}
              onClick={() => onComplete(r)}
            >
              Done
            </Button>
          </Tooltip>
          <Tooltip title="Edit Schedule">
            <Button size="small" icon={<EditOutlined />} onClick={() => onEdit(r)} />
          </Tooltip>
          {r.isOverdue && (
            <Tooltip title="Snooze Reminders">
              <Button
                size="small"
                icon={<BellOutlined />}
                onClick={() => onSnooze(r)}
              />
            </Tooltip>
          )}
        </Space>
      ),
    }] : []),
  ];
}

// ── SchedulerContent ──────────────────────────────────────────────────────────
function SchedulerContent() {
  const user      = useAuthStore(s => s.user);
  const role      = user?.role;
  const qc        = useQueryClient();
  const canManage = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';

  const [category,     setCategory]     = useState<string | undefined>();
  const [overdueOnly,  setOverdueOnly]  = useState(false);
  const [page,         setPage]         = useState(1);
  const [newModalOpen, setNewModalOpen] = useState(false);

  // Complete
  const [completeTarget, setCompleteTarget] = useState<MaintenanceSchedule | null>(null);
  const [completeNotes,  setCompleteNotes]  = useState('');

  // Edit
  const [editTarget, setEditTarget] = useState<MaintenanceSchedule | null>(null);
  const [editForm]                  = Form.useForm();

  // Snooze
  const [snoozeTarget, setSnoozeTarget] = useState<MaintenanceSchedule | null>(null);
  const [snoozeHours,  setSnoozeHours]  = useState(24);

  const refresh = useCallback(() => {
    qc.invalidateQueries({ queryKey: ['maintenance'] });
  }, [qc]);

  // ── Queries ────────────────────────────────────────────────────────────────
  const { data, isFetching } = useQuery({
    queryKey: ['maintenance', 'list', category, overdueOnly, page],
    queryFn:  () => maintenanceApi.list({ category, overdueOnly: overdueOnly || undefined, page, pageSize: 15 }),
    refetchInterval: 60_000,
  });

  const { data: stats } = useQuery({
    queryKey: ['maintenance', 'stats'],
    queryFn:  maintenanceApi.stats,
    refetchInterval: 60_000,
  });

  const { data: upcoming } = useQuery({
    queryKey: ['maintenance', 'upcoming'],
    queryFn:  () => maintenanceApi.upcoming(14),
    refetchInterval: 60_000,
  });

  const { data: escalations } = useQuery({
    queryKey: ['maintenance', 'escalations'],
    queryFn:  maintenanceApi.escalations,
    enabled:  canManage,
    refetchInterval: 60_000,
  });

  // ── Mutations ──────────────────────────────────────────────────────────────
  const completeMut = useMutation({
    mutationFn: ({ id, notes }: { id: string; notes: string }) =>
      maintenanceApi.complete(id, user!.email, user!.fullName, notes),
    onSuccess: () => { message.success('Task marked complete. Next due date recalculated.'); setCompleteTarget(null); setCompleteNotes(''); refresh(); },
    onError:   () => message.error('Failed to mark complete.'),
  });

  const editMut = useMutation({
    mutationFn: ({ id, patch }: { id: string; patch: Record<string, unknown> }) =>
      maintenanceApi.update(id, patch as Parameters<typeof maintenanceApi.update>[1]),
    onSuccess: () => { message.success('Schedule updated.'); setEditTarget(null); editForm.resetFields(); refresh(); },
    onError:   () => message.error('Failed to update schedule.'),
  });

  const snoozeMut = useMutation({
    mutationFn: ({ id, hours }: { id: string; hours: number }) =>
      maintenanceApi.snoozeReminder(id, hours),
    onSuccess: () => { message.success(`Reminders snoozed for ${snoozeHours} hours.`); setSnoozeTarget(null); refresh(); },
    onError:   () => message.error('Failed to snooze.'),
  });

  const columns = buildColumns(canManage, setCompleteTarget, (r) => {
    setEditTarget(r);
    editForm.setFieldsValue({
      taskName:       r.taskName,
      description:    r.description,
      category:       r.category,
      location:       r.location,
      frequencyLabel: r.frequencyLabel,
      frequencyDays:  r.frequencyDays,
    });
  }, setSnoozeTarget);

  return (
    <div>
      {/* ── Header ─────────────────────────────────────────────── */}
      <Row justify="space-between" align="middle" style={{ marginBottom: 16 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Maintenance Scheduler</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            Recurring tasks · Automated reminders · Escalation engine
          </Text>
        </Col>
        <Col>
          <Space>
            <Tooltip title="Refresh">
              <Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} />
            </Tooltip>
            {canManage && (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setNewModalOpen(true)}>
                New Schedule
              </Button>
            )}
          </Space>
        </Col>
      </Row>

      {/* ── Escalation alert — manager-level (level 2) ─────────── */}
      {canManage && (escalations?.filter(e => e.escalationLevel >= 2).length ?? 0) > 0 && (
        <Alert
          type="error"
          showIcon
          icon={<ThunderboltOutlined />}
          message={`${escalations!.filter(e => e.escalationLevel >= 2).length} task(s) escalated to Department Manager — requires immediate action`}
          style={{ marginBottom: 12 }}
          description={
            <ul style={{ margin: '4px 0 0', paddingLeft: 16 }}>
              {escalations!.filter(e => e.escalationLevel >= 2).map(e => (
                <li key={e.id}><strong>{e.taskName}</strong> — {e.location} ({Math.abs(dayjs(e.nextDueAt).diff(dayjs(), 'day'))}d overdue)</li>
              ))}
            </ul>
          }
        />
      )}

      {/* ── Overdue alert ───────────────────────────────────────── */}
      {(stats?.overdue ?? 0) > 0 && (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined />}
          message={`${stats!.overdue} maintenance ${stats!.overdue === 1 ? 'task is' : 'tasks are'} overdue`}
          style={{ marginBottom: 12 }}
          action={
            <Button size="small" danger onClick={() => setOverdueOnly(true)}>
              View Overdue
            </Button>
          }
        />
      )}

      {/* ── Stats cards ────────────────────────────────────────── */}
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        {[
          { label: 'Total Active',   value: stats?.total,     icon: <ToolOutlined />,         bg: '#f0f5ff', color: '#1677ff' },
          { label: 'Overdue',        value: stats?.overdue,   icon: <WarningOutlined />,      bg: '#fff1f0', color: '#ff4d4f' },
          { label: 'Due This Week',  value: stats?.dueSoon,   icon: <ClockCircleOutlined />,  bg: '#fff7e6', color: '#fa8c16' },
          { label: 'Done This Month',value: stats?.completed, icon: <CheckCircleOutlined />,  bg: '#f6ffed', color: '#52c41a' },
        ].map(s => (
          <Col xs={12} sm={6} key={s.label}>
            <Card size="small">
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div style={{ width: 36, height: 36, borderRadius: 8, background: s.bg, display: 'flex', alignItems: 'center', justifyContent: 'center', color: s.color, fontSize: 18 }}>
                  {s.icon}
                </div>
                <div>
                  <div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1 }}>{s.value ?? '—'}</div>
                  <div style={{ fontSize: 12, color: '#8c8c8c', marginTop: 2 }}>{s.label}</div>
                </div>
              </div>
            </Card>
          </Col>
        ))}
      </Row>

      {/* ── Upcoming 14-day timeline ────────────────────────────── */}
      {(upcoming?.length ?? 0) > 0 && (
        <Card
          size="small"
          title={<Space><ClockCircleOutlined style={{ color: '#fa8c16' }} />Upcoming (next 14 days)</Space>}
          style={{ marginBottom: 16 }}
          styles={{ body: { padding: '12px 20px' } }}
        >
          <Timeline
            mode="left"
            items={upcoming!.slice(0, 6).map(s => {
              const d        = dayjs(s.nextDueAt);
              const daysLeft = d.diff(dayjs(), 'day');
              const meta     = MAINTENANCE_CATEGORY_META[s.category];
              return {
                color: daysLeft <= 2 ? 'red' : daysLeft <= 5 ? 'orange' : 'blue',
                label: d.format('D MMM'),
                children: (
                  <div>
                    <Text strong style={{ fontSize: 12 }}>{s.taskName}</Text>
                    <Tag color={meta?.color} style={{ marginLeft: 6, fontSize: 10 }}>{meta?.label ?? s.category}</Tag>
                    {s.location && <Text type="secondary" style={{ fontSize: 11, display: 'block' }}>{s.location}</Text>}
                    {s.assignedToName && <Text type="secondary" style={{ fontSize: 11 }}>→ {s.assignedToName}</Text>}
                  </div>
                ),
              };
            })}
          />
        </Card>
      )}

      {/* ── Main schedule table ─────────────────────────────────── */}
      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '12px 20px', borderBottom: '1px solid #f0f0f0', display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
          <Space wrap>
            <Select
              placeholder="Category"
              allowClear
              style={{ width: 180 }}
              value={category}
              onChange={v => { setCategory(v); setPage(1); }}
              options={Object.entries(MAINTENANCE_CATEGORY_META).map(([k, m]) => ({
                value: k, label: m.label,
              }))}
            />
            <Button
              type={overdueOnly ? 'primary' : 'default'}
              danger={overdueOnly}
              onClick={() => { setOverdueOnly(!overdueOnly); setPage(1); }}
            >
              {overdueOnly ? 'Showing Overdue Only' : 'Overdue Only'}
            </Button>
          </Space>
          <Dropdown
            menu={{
              items: [
                { key: 'excel-all',      label: 'Excel — All schedules'  },
                { key: 'excel-overdue',  label: 'Excel — Overdue only'   },
                { key: 'excel-active',   label: 'Excel — Active only'    },
                { type: 'divider' },
                { key: 'pdf-all',        label: 'PDF — All schedules'    },
                { key: 'pdf-overdue',    label: 'PDF — Overdue only'     },
              ] as MenuProps['items'],
              onClick: ({ key }) => {
                const [fmt, statusKey] = key.split('-') as ['excel' | 'pdf', string];
                const status = statusKey === 'all' ? 'all'
                  : statusKey === 'overdue' ? 'overdue'
                  : 'active';
                exportMaintenanceSchedules({ format: fmt, category, status: status as any });
              },
            }}
          >
            <Button icon={<DownloadOutlined />}>Export</Button>
          </Dropdown>
        </div>

        <Table<MaintenanceSchedule>
          columns={columns}
          dataSource={data?.items ?? []}
          rowKey="id"
          loading={isFetching}
          pagination={{
            current:   page,
            pageSize:  15,
            total:     data?.totalCount ?? 0,
            onChange:  p => setPage(p),
            showTotal: (total, [from, to]) => `${from}–${to} of ${total} schedules`,
            showSizeChanger: false,
          }}
          rowClassName={r =>
            r.escalationLevel >= 2
              ? 'ant-table-row-manager-escalation'
              : r.escalationLevel === 1
                ? 'ant-table-row-supervisor-escalation'
                : r.isOverdue
                  ? 'ant-table-row-overdue'
                  : ''
          }
          size="middle"
          style={{ padding: '0 8px' }}
          scroll={{ x: 1000 }}
        />
      </Card>

      {/* ── New Schedule Modal ──────────────────────────────────── */}
      <NewScheduleModal
        open={newModalOpen}
        onClose={() => setNewModalOpen(false)}
        onCreated={refresh}
      />

      {/* ── Complete Modal ──────────────────────────────────────── */}
      <Modal
        title="Mark as Completed"
        open={!!completeTarget}
        onOk={() => completeMut.mutate({ id: completeTarget!.id, notes: completeNotes })}
        onCancel={() => { setCompleteTarget(null); setCompleteNotes(''); }}
        okText="Confirm Completion"
        okButtonProps={{ style: { background: '#52c41a', borderColor: '#52c41a' } }}
        confirmLoading={completeMut.isPending}
      >
        {completeTarget && (
          <>
            <p>Mark <strong>{completeTarget.taskName}</strong> as completed?</p>
            {completeTarget.isOverdue && (
              <Alert type="warning" showIcon message="This task is overdue. Completing it will reset the escalation state and advance the next due date." style={{ marginBottom: 12 }} />
            )}
            <Descriptions column={1} size="small" bordered style={{ marginBottom: 12 }}>
              <Descriptions.Item label="Location">{completeTarget.location || '—'}</Descriptions.Item>
              <Descriptions.Item label="Frequency">{completeTarget.frequencyLabel} ({completeTarget.frequencyDays} days)</Descriptions.Item>
              <Descriptions.Item label="Next Due">
                {dayjs().add(completeTarget.frequencyDays, 'day').format('D MMM YYYY')} (auto-calculated)
              </Descriptions.Item>
            </Descriptions>
            <Form layout="vertical">
              <Form.Item label="Completion Notes (optional)">
                <Form.Item name="notes" noStyle>
                  <textarea
                    rows={2}
                    style={{ width: '100%', padding: 8, border: '1px solid #d9d9d9', borderRadius: 6 }}
                    placeholder="Any notes about this completion…"
                    value={completeNotes}
                    onChange={e => setCompleteNotes(e.target.value)}
                  />
                </Form.Item>
              </Form.Item>
            </Form>
          </>
        )}
      </Modal>

      {/* ── Edit Schedule Drawer ────────────────────────────────── */}
      <Drawer
        title={`Edit: ${editTarget?.taskName ?? ''}`}
        width={480}
        open={!!editTarget}
        onClose={() => { setEditTarget(null); editForm.resetFields(); }}
        footer={
          <Space style={{ float: 'right' }}>
            <Button onClick={() => { setEditTarget(null); editForm.resetFields(); }}>Cancel</Button>
            <Button
              type="primary"
              loading={editMut.isPending}
              onClick={() =>
                editForm.validateFields().then(vals =>
                  editMut.mutate({ id: editTarget!.id, patch: vals })
                )
              }
            >
              Save Changes
            </Button>
          </Space>
        }
      >
        <Form form={editForm} layout="vertical">
          <Form.Item name="taskName" label="Task Name" rules={[{ required: true }]}>
            <input style={{ width: '100%', padding: '4px 8px', border: '1px solid #d9d9d9', borderRadius: 6 }} />
          </Form.Item>
          <Form.Item name="category" label="Category" rules={[{ required: true }]}>
            <Select options={Object.entries(MAINTENANCE_CATEGORY_META).map(([k, m]) => ({ value: k, label: m.label }))} />
          </Form.Item>
          <Form.Item name="location" label="Location">
            <input style={{ width: '100%', padding: '4px 8px', border: '1px solid #d9d9d9', borderRadius: 6 }} />
          </Form.Item>
          <Form.Item name="frequencyLabel" label="Frequency">
            <Select options={['Weekly','Monthly','Quarterly','Annually','Custom'].map(v => ({ value: v, label: v }))} />
          </Form.Item>
          <Form.Item name="frequencyDays" label="Frequency (days)">
            <InputNumber min={1} max={365} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <textarea rows={3} style={{ width: '100%', padding: 8, border: '1px solid #d9d9d9', borderRadius: 6 }} />
          </Form.Item>
        </Form>
      </Drawer>

      {/* ── Snooze Reminder Modal ───────────────────────────────── */}
      <Modal
        title={`Snooze Reminders — ${snoozeTarget?.taskName ?? ''}`}
        open={!!snoozeTarget}
        onOk={() => snoozeMut.mutate({ id: snoozeTarget!.id, hours: snoozeHours })}
        onCancel={() => setSnoozeTarget(null)}
        okText={`Snooze for ${snoozeHours}h`}
        confirmLoading={snoozeMut.isPending}
      >
        {snoozeTarget && (
          <>
            <Alert
              type="info"
              showIcon
              message="Snoozing delays the next automatic reminder from the system. The task will still appear as overdue."
              style={{ marginBottom: 16 }}
            />
            <Divider />
            <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
              <Text>Snooze for:</Text>
              <Select
                value={snoozeHours}
                onChange={setSnoozeHours}
                style={{ width: 160 }}
                options={[
                  { value: 24,  label: '24 hours (1 day)'  },
                  { value: 48,  label: '48 hours (2 days)' },
                  { value: 72,  label: '72 hours (3 days)' },
                  { value: 168, label: '7 days'            },
                ]}
              />
            </div>
          </>
        )}
      </Modal>
    </div>
  );
}
