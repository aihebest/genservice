import { useState, useCallback } from 'react';
import {
  Button, Card, Col, Progress, Row, Select, Space, Statistic,
  Table, Tag, Badge, Tabs, Tooltip, Typography,
} from 'antd';
import type { ColumnsType as AntColumnsType } from 'antd/es/table';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, ReloadOutlined, TeamOutlined, ThunderboltOutlined,
  TrophyOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { activitiesApi } from '../../api/activities.api';
import { taskProgressLogApi } from '../../api/taskProgressLog.api';
import {
  ACTIVITY_STATUS_META, ACTIVITY_CATEGORY_META,
} from '../../types';
import type { StaffActivity, ActivityStatus, TechnicianSummary } from '../../types';
import { useAuthStore } from '../../store/authStore';
import LogActivityModal from './components/LogActivityModal';
import ActivityFeed     from './components/ActivityFeed';

dayjs.extend(relativeTime);

const { Title, Text } = Typography;

const STATUS_TABS = [
  { key: '',          label: 'All'       },
  { key: 'Active',    label: 'Active'    },
  { key: 'Paused',    label: 'Paused'    },
  { key: 'Completed', label: 'Completed' },
];

function buildColumns(canManage: boolean, onComplete: (id: string) => void): ColumnsType<StaffActivity> {
  return [
    {
      title: 'Staff Member',
      dataIndex: 'staffName',
      key: 'staffName',
      width: 180,
      render: (name: string, r: StaffActivity) => (
        <div>
          <Text strong style={{ fontSize: 13 }}>{name}</Text>
          {r.isProxy && (
            <Tooltip title={`Logged by ${r.loggedByName}`}>
              <Tag color="geekblue" style={{ fontSize: 11, marginLeft: 6 }}>Proxy</Tag>
            </Tooltip>
          )}
        </div>
      ),
    },
    {
      title: 'Activity',
      dataIndex: 'activityDescription',
      key: 'activity',
      ellipsis: true,
      render: (v: string) => <Text style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'Category',
      dataIndex: 'category',
      key: 'category',
      width: 150,
      render: (v: string) => {
        const m = ACTIVITY_CATEGORY_META[v as keyof typeof ACTIVITY_CATEGORY_META];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 115,
      render: (v: ActivityStatus) => {
        const m = ACTIVITY_STATUS_META[v];
        return <Badge status={m?.badge as any} text={m?.label ?? v} />;
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
      title: 'Started',
      dataIndex: 'startedAt',
      key: 'startedAt',
      width: 115,
      render: (v: string) => (
        <Tooltip title={dayjs(v).format('D MMM YYYY, HH:mm')}>
          <Text type="secondary" style={{ fontSize: 12 }}>{dayjs(v).fromNow()}</Text>
        </Tooltip>
      ),
    },
    ...(canManage ? [{
      title: '',
      key: 'action',
      width: 85,
      render: (_: unknown, r: StaffActivity) =>
        r.status === 'Active' ? (
          <Button size="small" onClick={() => onComplete(r.id)}>Complete</Button>
        ) : null,
    }] : []),
  ];
}

export default function ActivitiesPage() {
  const user      = useAuthStore(s => s.user);
  const role      = user?.role;
  const qc        = useQueryClient();
  const canManage = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';

  const [activeStatus, setActiveStatus] = useState('Active');
  const [category,     setCategory]     = useState<string | undefined>();
  const [page,         setPage]         = useState(1);
  const [modalOpen,    setModalOpen]    = useState(false);
  const [activeTab,    setActiveTab]    = useState<'feed' | 'table' | 'performance'>('feed');

  const refresh = useCallback(() => {
    qc.invalidateQueries({ queryKey: ['activities'] });
  }, [qc]);

  // Live feed — always active-only
  const { data: feedData, isFetching: feedFetching } = useQuery({
    queryKey: ['activities', 'feed'],
    queryFn:  () => activitiesApi.getActive(),
    refetchInterval: 15_000,
  });

  // Table — filtered list
  const { data: listData, isFetching: listFetching } = useQuery({
    queryKey: ['activities', 'list', activeStatus, category, page],
    queryFn:  () => activitiesApi.list({
      status:   activeStatus || undefined,
      category: category     || undefined,
      page,
      pageSize: 15,
    }),
    enabled: activeTab === 'table',
  });

  const { data: perfData, isFetching: perfFetching } = useQuery({
    queryKey: ['technician-performance'],
    queryFn:  taskProgressLogApi.performance,
    enabled:  activeTab === 'performance',
    refetchInterval: 60_000,
  });

  const handleStatusUpdate = async (id: string, status: string) => {
    await activitiesApi.updateStatus(id, status);
    refresh();
  };

  const columns = buildColumns(canManage, id => handleStatusUpdate(id, 'Completed'));

  const activeCount = feedData?.filter(a => a.status === 'Active').length ?? 0;
  const pausedCount = feedData?.filter(a => a.status === 'Paused').length ?? 0;

  return (
    <div>
      {/* ── Header ─────────────────────────────────────────────── */}
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Team Activity Tracker</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            Real-time visibility into staff assignments and field operations
          </Text>
        </Col>
        <Col>
          <Space>
            <Tooltip title="Refresh">
              <Button icon={<ReloadOutlined />} onClick={refresh} loading={feedFetching} />
            </Tooltip>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setModalOpen(true)}
            >
              Log Activity
            </Button>
          </Space>
        </Col>
      </Row>

      {/* ── Summary cards ──────────────────────────────────────── */}
      <Row gutter={16} style={{ marginBottom: 20 }}>
        <Col xs={12} sm={6}>
          <Card size="small">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <div style={{
                width: 36, height: 36, borderRadius: 8,
                background: '#f6ffed', display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}>
                <ThunderboltOutlined style={{ color: '#52c41a', fontSize: 18 }} />
              </div>
              <div>
                <div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1 }}>{activeCount}</div>
                <div style={{ fontSize: 12, color: '#8c8c8c', marginTop: 2 }}>Active Now</div>
              </div>
            </div>
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <div style={{
                width: 36, height: 36, borderRadius: 8,
                background: '#fff7e6', display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}>
                <TeamOutlined style={{ color: '#fa8c16', fontSize: 18 }} />
              </div>
              <div>
                <div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1 }}>{pausedCount}</div>
                <div style={{ fontSize: 12, color: '#8c8c8c', marginTop: 2 }}>Paused</div>
              </div>
            </div>
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card size="small">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <div style={{
                width: 36, height: 36, borderRadius: 8,
                background: '#f0f5ff', display: 'flex', alignItems: 'center', justifyContent: 'center',
              }}>
                <TeamOutlined style={{ color: '#1677ff', fontSize: 18 }} />
              </div>
              <div>
                <div style={{ fontSize: 22, fontWeight: 700, lineHeight: 1 }}>
                  {feedData?.length ?? 0}
                </div>
                <div style={{ fontSize: 12, color: '#8c8c8c', marginTop: 2 }}>On Feed</div>
              </div>
            </div>
          </Card>
        </Col>
      </Row>

      {/* ── Feed / Table toggle ─────────────────────────────────── */}
      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '0 24px', borderBottom: '1px solid #f0f0f0' }}>
          <Tabs
            activeKey={activeTab}
            onChange={k => setActiveTab(k as 'feed' | 'table' | 'performance')}
            items={[
              { key: 'feed',        label: <span><ThunderboltOutlined /> Live Feed</span>    },
              { key: 'table',       label: <span><TeamOutlined /> Activity Log</span>        },
              { key: 'performance', label: <span><TrophyOutlined /> Performance</span>       },
            ]}
            size="small"
          />
        </div>

        {activeTab === 'feed' ? (
          <div style={{ padding: 24 }}>
            <ActivityFeed
              activities={feedData ?? []}
              onStatusUpdate={canManage ? handleStatusUpdate : undefined}
              canManage={canManage}
            />
          </div>
        ) : (
          <>
            <div style={{ padding: '16px 24px', borderBottom: '1px solid #f0f0f0' }}>
              <Space wrap>
                <Select
                  style={{ width: 140 }}
                  value={activeStatus}
                  onChange={v => { setActiveStatus(v); setPage(1); }}
                  options={STATUS_TABS.map(t => ({ value: t.key, label: t.label }))}
                />
                <Select
                  placeholder="Category"
                  allowClear
                  style={{ width: 160 }}
                  value={category}
                  onChange={v => { setCategory(v); setPage(1); }}
                  options={Object.entries(ACTIVITY_CATEGORY_META).map(([k, m]) => ({
                    value: k, label: m.label,
                  }))}
                />
              </Space>
            </div>

            <Table<StaffActivity>
              columns={columns}
              dataSource={listData?.items ?? []}
              rowKey="id"
              loading={listFetching}
              pagination={{
                current:  page,
                pageSize: 15,
                total:    listData?.totalCount ?? 0,
                onChange: p => setPage(p),
                showTotal: (total, [from, to]) => `${from}–${to} of ${total}`,
                showSizeChanger: false,
              }}
              size="middle"
              style={{ padding: '0 8px' }}
              scroll={{ x: 800 }}
            />
          </>
        )}

        {activeTab === 'performance' && (
          <div style={{ padding: 24 }}>
            <PerformanceDashboard data={perfData ?? []} loading={perfFetching} />
          </div>
        )}
      </Card>

      <LogActivityModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onCreated={refresh}
      />
    </div>
  );
}

// ── Performance Dashboard ─────────────────────────────────────────────────────
function PerformanceDashboard({ data, loading }: { data: TechnicianSummary[]; loading: boolean }) {
  const perfColumns: AntColumnsType<TechnicianSummary> = [
    {
      title: 'Technician', dataIndex: 'name', key: 'name', width: 160,
      render: (v: string) => <Text strong>{v}</Text>,
    },
    {
      title: 'Total Assigned', dataIndex: 'totalAssigned', key: 'total', width: 120,
      render: (v: number) => <Statistic value={v} valueStyle={{ fontSize: 18 }} />,
    },
    {
      title: 'In Progress', dataIndex: 'inProgress', key: 'inProgress', width: 110,
      render: (v: number) => <Tag color="processing">{v}</Tag>,
    },
    {
      title: 'Completed', dataIndex: 'completed', key: 'completed', width: 100,
      render: (v: number) => <Tag color="success">{v}</Tag>,
    },
    {
      title: 'Pending', dataIndex: 'pending', key: 'pending', width: 90,
      render: (v: number) => <Tag color="default">{v}</Tag>,
    },
    {
      title: 'Completion Rate', key: 'rate', width: 150,
      render: (_: unknown, r: TechnicianSummary) => {
        const rate = r.totalAssigned > 0
          ? Math.round((r.completed / r.totalAssigned) * 100)
          : 0;
        return (
          <div style={{ width: 120 }}>
            <Progress percent={rate} size="small" strokeColor={rate >= 70 ? '#52c41a' : rate >= 40 ? '#fa8c16' : '#ff4d4f'} />
          </div>
        );
      },
    },
    {
      title: 'Today Updates', dataIndex: 'todayLogs', key: 'today', width: 115,
      render: (v: number) => (
        <Tag color={v > 0 ? 'blue' : 'default'}>{v} log{v !== 1 ? 's' : ''}</Tag>
      ),
    },
    {
      title: 'This Week', dataIndex: 'weekLogs', key: 'week', width: 100,
      render: (v: number) => <Text style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'This Month', dataIndex: 'monthLogs', key: 'month', width: 105,
      render: (v: number) => <Text style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'Blockers', key: 'blockers', width: 160,
      render: (_: unknown, r: TechnicianSummary) => (
        <Space size={4} wrap>
          {r.awaitingMaterials > 0 && <Tag color="gold">{r.awaitingMaterials} Spares</Tag>}
          {r.awaitingVendor > 0    && <Tag color="orange">{r.awaitingVendor} Vendor</Tag>}
          {r.awaitingMaterials === 0 && r.awaitingVendor === 0 && <Text type="secondary">—</Text>}
        </Space>
      ),
    },
  ];

  const totalAssigned  = data.reduce((s, t) => s + t.totalAssigned, 0);
  const totalCompleted = data.reduce((s, t) => s + t.completed, 0);
  const totalToday     = data.reduce((s, t) => s + t.todayLogs, 0);
  const totalBlockers  = data.reduce((s, t) => s + t.awaitingMaterials + t.awaitingVendor, 0);

  return (
    <div>
      {/* Summary KPIs */}
      <Row gutter={16} style={{ marginBottom: 24 }}>
        {[
          { label: 'Total Technicians',   value: data.length,      color: '#1677ff' },
          { label: 'Total Assigned Jobs', value: totalAssigned,    color: '#722ed1' },
          { label: 'Total Completed',     value: totalCompleted,   color: '#52c41a' },
          { label: "Today's Updates",     value: totalToday,       color: '#1677ff' },
          { label: 'Active Blockers',     value: totalBlockers,    color: '#fa541c' },
        ].map(k => (
          <Col key={k.label} style={{ flex: '1 1 140px', minWidth: 130 }}>
            <Card size="small" styles={{ body: { padding: '14px 18px' } }}>
              <Statistic
                title={<Text style={{ fontSize: 12 }}>{k.label}</Text>}
                value={k.value}
                valueStyle={{ color: k.color, fontSize: 24, fontWeight: 700 }}
              />
            </Card>
          </Col>
        ))}
      </Row>

      <Table<TechnicianSummary>
        columns={perfColumns}
        dataSource={data}
        rowKey="email"
        loading={loading}
        pagination={false}
        size="middle"
        scroll={{ x: 1100 }}
        locale={{ emptyText: 'No assignment data yet — assign requests to technicians to see their performance.' }}
      />
    </div>
  );
}
