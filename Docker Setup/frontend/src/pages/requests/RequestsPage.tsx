import { useState, useCallback } from 'react';
import {
  Button, Card, Input, Select, Space, Table, Tag, Badge,
  Typography, Tooltip, Tabs, Row, Col,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { requestsApi } from '../../api/requests.api';
import {
  CATEGORY_META, STATUS_META, PRIORITY_META,
} from '../../types';
import type { ServiceRequest, RequestStatus, RequestCategory, RequestPriority } from '../../types';
import { useAuthStore } from '../../store/authStore';
import RequestStats        from './components/RequestStats';
import NewRequestModal     from './components/NewRequestModal';
import RequestDetailDrawer from './components/RequestDetailDrawer';

dayjs.extend(relativeTime);

const { Title, Text } = Typography;

// ── Status tab definitions ─────────────────────────────────────────────────
const STATUS_TABS = [
  { key: '',                   label: 'All'                    },
  { key: 'Open',               label: 'Open'                   },
  { key: 'PendingLineManager', label: 'Pending Line Manager'   },
  { key: 'PendingApproval',    label: 'Pending GS Approval'    },
  { key: 'Approved',        label: 'Approved'        },
  { key: 'InProgress',      label: 'In Progress'     },
  { key: 'MaterialAwaited', label: 'Awaiting Spares' },
  { key: 'AwaitingFunds',   label: 'Awaiting Funds'  },
  { key: 'Reassigned',      label: 'Reassigned'      },
  { key: 'Completed',       label: 'Completed'       },
  { key: 'Rejected',        label: 'Rejected'        },
];

// ── Table columns ──────────────────────────────────────────────────────────
function buildColumns(
  onView: (r: ServiceRequest) => void
): ColumnsType<ServiceRequest> {
  return [
    {
      title: 'Ticket',
      dataIndex: 'ticketNumber',
      key: 'ticketNumber',
      width: 130,
      render: (v: string) => <Text code style={{ fontSize: 12 }}>{v}</Text>,
    },
    {
      title: 'Title',
      dataIndex: 'title',
      key: 'title',
      ellipsis: true,
      render: (v: string, r) => (
        <Button type="link" style={{ padding: 0, height: 'auto', textAlign: 'left' }}
          onClick={() => onView(r)}>
          {v}
        </Button>
      ),
    },
    {
      title: 'Category',
      dataIndex: 'category',
      key: 'category',
      width: 160,
      render: (v: RequestCategory) => {
        const m = CATEGORY_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 145,
      render: (v: RequestStatus) => {
        const m = STATUS_META[v];
        return <Badge status={m?.color as any} text={m?.label ?? v} />;
      },
    },
    {
      title: 'Priority',
      dataIndex: 'priority',
      key: 'priority',
      width: 95,
      render: (v: RequestPriority) => {
        const m = PRIORITY_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Raised By',
      dataIndex: 'requestedByName',
      key: 'requestedByName',
      width: 155,
      ellipsis: true,
      render: (v: string) => <Text style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'Assigned To',
      dataIndex: 'assignedToName',
      key: 'assignedToName',
      width: 155,
      ellipsis: true,
      render: (v?: string) => v
        ? <Text style={{ fontSize: 13 }}>{v}</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>Unassigned</Text>,
    },
    {
      title: 'Submitted',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 115,
      render: (v: string) => (
        <Tooltip title={dayjs(v).format('D MMM YYYY, HH:mm')}>
          <Text type="secondary" style={{ fontSize: 12 }}>{dayjs(v).fromNow()}</Text>
        </Tooltip>
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 70,
      render: (_: unknown, r: ServiceRequest) => (
        <Button size="small" onClick={() => onView(r)}>View</Button>
      ),
    },
  ];
}

// ── Main page ──────────────────────────────────────────────────────────────
export default function RequestsPage() {
  const role = useAuthStore(s => s.user?.role);
  const qc   = useQueryClient();

  const [activeStatus, setActiveStatus] = useState('');
  const [category,     setCategory]     = useState<string | undefined>();
  const [priority,     setPriority]     = useState<string | undefined>();
  const [search,       setSearch]       = useState('');
  const [page,         setPage]         = useState(1);
  const [modalOpen,    setModalOpen]    = useState(false);
  const [selected,     setSelected]     = useState<ServiceRequest | null>(null);
  const [drawerOpen,   setDrawerOpen]   = useState(false);

  const queryKey = ['requests', activeStatus, category, priority, search, page];

  const { data, isFetching } = useQuery({
    queryKey,
    queryFn: () => requestsApi.list({
      status:   activeStatus || undefined,
      category: category     || undefined,
      priority: priority     || undefined,
      search:   search       || undefined,
      page,
      pageSize: 15,
    }),
  });

  const { data: stats, isFetching: statsFetching } = useQuery({
    queryKey: ['request-stats'],
    queryFn: requestsApi.stats,
    refetchInterval: 30_000,
  });

  const refresh = useCallback(() => {
    qc.invalidateQueries({ queryKey: ['requests'] });
    qc.invalidateQueries({ queryKey: ['request-stats'] });
  }, [qc]);

  const openDetail = (r: ServiceRequest) => {
    setSelected(r);
    setDrawerOpen(true);
  };

  const handleStatusTab = (key: string) => {
    setActiveStatus(key);
    setPage(1);
  };

  const handleSearch = (v: string) => {
    setSearch(v);
    setPage(1);
  };

  const columns = buildColumns(openDetail);

  return (
    <div>
      {/* ── Page header ───────────────────────────────────────────── */}
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Request Management</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            All service requests — submission, approval, assignment, and tracking
          </Text>
        </Col>
        <Col>
          <Space>
            <Tooltip title="Refresh">
              <Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} />
            </Tooltip>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setModalOpen(true)}
            >
              New Request
            </Button>
          </Space>
        </Col>
      </Row>

      {/* ── Stats row ─────────────────────────────────────────────── */}
      <RequestStats stats={stats} loading={statsFetching} />

      {/* ── Filters + table ───────────────────────────────────────── */}
      <Card styles={{ body: { padding: 0 } }}>
        {/* Status tabs */}
        <div style={{ padding: '0 24px', borderBottom: '1px solid #f0f0f0' }}>
          <Tabs
            activeKey={activeStatus}
            onChange={handleStatusTab}
            items={STATUS_TABS.map(t => ({ key: t.key, label: t.label }))}
            size="small"
          />
        </div>

        {/* Search + category + priority filters */}
        <div style={{ padding: '16px 24px', borderBottom: '1px solid #f0f0f0' }}>
          <Space wrap>
            <Input
              prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
              placeholder="Search by ticket, title, name, location…"
              style={{ width: 300 }}
              allowClear
              onChange={e => !e.target.value && handleSearch('')}
              onPressEnter={(e: React.KeyboardEvent<HTMLInputElement>) =>
                handleSearch((e.target as HTMLInputElement).value)
              }
            />
            <Select
              placeholder="Category"
              allowClear
              style={{ width: 180 }}
              value={category}
              onChange={v => { setCategory(v); setPage(1); }}
              options={Object.entries(CATEGORY_META).map(([k, m]) => ({
                value: k, label: m.label,
              }))}
            />
            <Select
              placeholder="Priority"
              allowClear
              style={{ width: 130 }}
              value={priority}
              onChange={v => { setPriority(v); setPage(1); }}
              options={Object.entries(PRIORITY_META).map(([k, m]) => ({
                value: k,
                label: <Tag color={m.color}>{m.label}</Tag>,
              }))}
            />
          </Space>
        </div>

        {/* Table */}
        <Table<ServiceRequest>
          columns={columns}
          dataSource={data?.items ?? []}
          rowKey="id"
          loading={isFetching}
          pagination={{
            current:  page,
            pageSize: 15,
            total:    data?.totalCount ?? 0,
            onChange: p => setPage(p),
            showTotal: (total, [from, to]) => `${from}–${to} of ${total} requests`,
            showSizeChanger: false,
          }}
          onRow={r => ({ onClick: () => openDetail(r), style: { cursor: 'pointer' } })}
          rowClassName={r =>
            r.status === 'PendingApproval' && (role === 'DepartmentManager' || role === 'Supervisor')
              ? 'ant-table-row-pending'
              : ''
          }
          size="middle"
          style={{ padding: '0 8px' }}
          scroll={{ x: 900 }}
        />
      </Card>

      {/* ── New request modal ──────────────────────────────────────── */}
      <NewRequestModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onCreated={refresh}
      />

      {/* ── Detail drawer ─────────────────────────────────────────── */}
      <RequestDetailDrawer
        request={selected}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        onUpdate={() => {
          refresh();
          // refresh the selected request from cache
          if (selected) {
            requestsApi.getById(selected.id).then(setSelected).catch(() => {});
          }
        }}
      />
    </div>
  );
}
