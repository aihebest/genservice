import { useState, useCallback } from 'react';
import {
  Alert, Badge, Button, Card, Col, Modal, Row, Select, Space,
  Table, Tag, Tooltip, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, ReloadOutlined, WarningOutlined,
  ClockCircleOutlined, CheckCircleOutlined, ToolOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { maintenanceApi } from '../../api/maintenance.api';
import { MAINTENANCE_CATEGORY_META } from '../../types';
import type { MaintenanceSchedule, MaintenanceCategory } from '../../types';
import { useAuthStore } from '../../store/authStore';
import NewScheduleModal from './components/NewScheduleModal';

dayjs.extend(relativeTime);

const { Title, Text } = Typography;

function buildColumns(
  canManage: boolean,
  onComplete: (id: string, name: string) => void,
): ColumnsType<MaintenanceSchedule> {
  return [
    {
      title: 'Task',
      dataIndex: 'taskName',
      key: 'taskName',
      ellipsis: true,
      render: (v: string, r: MaintenanceSchedule) => (
        <div>
          <Text strong style={{ fontSize: 13 }}>{v}</Text>
          {r.isOverdue && (
            <Tag color="red" style={{ marginLeft: 8, fontSize: 11 }}>OVERDUE</Tag>
          )}
        </div>
      ),
    },
    {
      title: 'Category',
      dataIndex: 'category',
      key: 'category',
      width: 150,
      render: (v: MaintenanceCategory) => {
        const m = MAINTENANCE_CATEGORY_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Location',
      dataIndex: 'location',
      key: 'location',
      width: 180,
      ellipsis: true,
      render: (v: string) => <Text type="secondary" style={{ fontSize: 12 }}>{v || '—'}</Text>,
    },
    {
      title: 'Frequency',
      dataIndex: 'frequencyLabel',
      key: 'frequencyLabel',
      width: 110,
      render: (v: string) => <Tag>{v}</Tag>,
    },
    {
      title: 'Next Due',
      dataIndex: 'nextDueAt',
      key: 'nextDueAt',
      width: 130,
      sorter: (a, b) => new Date(a.nextDueAt).getTime() - new Date(b.nextDueAt).getTime(),
      render: (v: string, r: MaintenanceSchedule) => {
        const d = dayjs(v);
        const daysLeft = d.diff(dayjs(), 'day');
        const color = r.isOverdue ? 'red' : daysLeft <= 7 ? 'orange' : 'default';
        return (
          <Tooltip title={d.format('D MMM YYYY')}>
            <Text style={{ color: r.isOverdue ? '#ff4d4f' : daysLeft <= 7 ? '#fa8c16' : undefined, fontSize: 12 }}>
              {r.isOverdue ? `${Math.abs(daysLeft)}d overdue` : daysLeft === 0 ? 'Due today' : `${daysLeft}d`}
            </Text>
          </Tooltip>
        );
      },
    },
    {
      title: 'Last Completed',
      dataIndex: 'lastCompletedAt',
      key: 'lastCompletedAt',
      width: 130,
      render: (v?: string) => v
        ? (
          <Tooltip title={dayjs(v).format('D MMM YYYY')}>
            <Text type="secondary" style={{ fontSize: 12 }}>{dayjs(v).fromNow()}</Text>
          </Tooltip>
        )
        : <Text type="secondary" style={{ fontSize: 12 }}>Never</Text>,
    },
    {
      title: 'Assigned To',
      dataIndex: 'assignedToName',
      key: 'assignedToName',
      width: 145,
      ellipsis: true,
      render: (v?: string) => v
        ? <Text style={{ fontSize: 12 }}>{v}</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>Unassigned</Text>,
    },
    ...(canManage ? [{
      title: '',
      key: 'actions',
      width: 95,
      render: (_: unknown, r: MaintenanceSchedule) => (
        <Button
          size="small"
          type="primary"
          icon={<CheckCircleOutlined />}
          onClick={() => onComplete(r.id, r.taskName)}
        >
          Done
        </Button>
      ),
    }] : []),
  ];
}

export default function MaintenancePage() {
  const user      = useAuthStore(s => s.user);
  const role      = user?.role;
  const qc        = useQueryClient();
  const canManage = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';

  const [category,     setCategory]     = useState<string | undefined>();
  const [overdueOnly,  setOverdueOnly]  = useState(false);
  const [page,         setPage]         = useState(1);
  const [modalOpen,    setModalOpen]    = useState(false);
  const [completeId,   setCompleteId]   = useState<string | null>(null);
  const [completeName, setCompleteName] = useState('');

  const refresh = useCallback(() => {
    qc.invalidateQueries({ queryKey: ['maintenance'] });
  }, [qc]);

  const { data, isFetching } = useQuery({
    queryKey: ['maintenance', 'list', category, overdueOnly, page],
    queryFn:  () => maintenanceApi.list({
      category:    category    || undefined,
      overdueOnly: overdueOnly || undefined,
      page,
      pageSize: 15,
    }),
    refetchInterval: 60_000,
  });

  const { data: stats } = useQuery({
    queryKey: ['maintenance', 'stats'],
    queryFn:  maintenanceApi.stats,
    refetchInterval: 60_000,
  });

  const handleComplete = (id: string, name: string) => {
    setCompleteId(id);
    setCompleteName(name);
  };

  const confirmComplete = async () => {
    if (!completeId || !user) return;
    await maintenanceApi.complete(completeId, user.email, user.fullName);
    setCompleteId(null);
    refresh();
  };

  const columns = buildColumns(canManage, handleComplete);

  return (
    <div>
      {/* ── Header ─────────────────────────────────────────────── */}
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Maintenance Scheduler</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            Recurring maintenance tasks, due dates, and completion tracking
          </Text>
        </Col>
        <Col>
          <Space>
            <Tooltip title="Refresh">
              <Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} />
            </Tooltip>
            {canManage && (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>
                New Schedule
              </Button>
            )}
          </Space>
        </Col>
      </Row>

      {/* ── Overdue alert ───────────────────────────────────────── */}
      {(stats?.overdue ?? 0) > 0 && (
        <Alert
          type="error"
          showIcon
          icon={<WarningOutlined />}
          message={`${stats!.overdue} maintenance ${stats!.overdue === 1 ? 'task is' : 'tasks are'} overdue and require immediate attention.`}
          style={{ marginBottom: 16 }}
          action={
            <Button size="small" danger onClick={() => setOverdueOnly(true)}>
              View Overdue
            </Button>
          }
        />
      )}

      {/* ── Stats cards ────────────────────────────────────────── */}
      <Row gutter={16} style={{ marginBottom: 20 }}>
        {[
          { label: 'Total Active',  value: stats?.total,     icon: <ToolOutlined />,         bg: '#f0f5ff', color: '#1677ff' },
          { label: 'Overdue',       value: stats?.overdue,   icon: <WarningOutlined />,      bg: '#fff1f0', color: '#ff4d4f' },
          { label: 'Due This Week', value: stats?.dueSoon,   icon: <ClockCircleOutlined />,  bg: '#fff7e6', color: '#fa8c16' },
          { label: 'Done This Month', value: stats?.completed, icon: <CheckCircleOutlined />, bg: '#f6ffed', color: '#52c41a' },
        ].map(s => (
          <Col xs={12} sm={6} key={s.label}>
            <Card size="small">
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div style={{
                  width: 36, height: 36, borderRadius: 8,
                  background: s.bg, display: 'flex', alignItems: 'center', justifyContent: 'center',
                  color: s.color, fontSize: 18,
                }}>
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

      {/* ── Table ──────────────────────────────────────────────── */}
      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '16px 24px', borderBottom: '1px solid #f0f0f0' }}>
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
              {overdueOnly ? 'Showing Overdue Only' : 'Filter: Overdue Only'}
            </Button>
          </Space>
        </div>

        <Table<MaintenanceSchedule>
          columns={columns}
          dataSource={data?.items ?? []}
          rowKey="id"
          loading={isFetching}
          pagination={{
            current:  page,
            pageSize: 15,
            total:    data?.totalCount ?? 0,
            onChange: p => setPage(p),
            showTotal: (total, [from, to]) => `${from}–${to} of ${total} schedules`,
            showSizeChanger: false,
          }}
          rowClassName={r => r.isOverdue ? 'ant-table-row-overdue' : ''}
          size="middle"
          style={{ padding: '0 8px' }}
          scroll={{ x: 900 }}
        />
      </Card>

      {/* ── New Schedule Modal ──────────────────────────────────── */}
      <NewScheduleModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onCreated={refresh}
      />

      {/* ── Complete Confirmation ───────────────────────────────── */}
      <Modal
        title="Mark as Completed"
        open={!!completeId}
        onOk={confirmComplete}
        onCancel={() => setCompleteId(null)}
        okText="Confirm Completion"
        okButtonProps={{ type: 'primary' }}
      >
        <p>
          Mark <strong>{completeName}</strong> as completed?
          The next due date will be automatically calculated based on the frequency.
        </p>
      </Modal>
    </div>
  );
}
