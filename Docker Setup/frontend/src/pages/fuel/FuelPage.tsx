import { useState, useCallback } from 'react';
import {
  Alert, Badge, Button, Card, Col, Row, Select, Space,
  Statistic, Table, Tag, Tooltip, Tabs, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, ReloadOutlined, ThunderboltOutlined,
  FireOutlined, StopOutlined, PlayCircleOutlined,
  DropboxOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import duration from 'dayjs/plugin/duration';
import { fuelApi } from '../../api/fuel.api';
import {
  GENERATOR_RUN_REASON_META, DIESEL_RECORD_TYPE_META,
} from '../../types';
import type { GeneratorLog, DieselRecord, GeneratorRunReason, DieselRecordType } from '../../types';
import DailyReadingsTab from './components/DailyReadingsTab';
import PowerMeterTab    from './components/PowerMeterTab';
import DieselTankTab    from './components/DieselTankTab';
import { useAuthStore } from '../../store/authStore';
import LogGeneratorModal from './components/LogGeneratorModal';
import AddDieselModal    from './components/AddDieselModal';

dayjs.extend(relativeTime);
dayjs.extend(duration);

const { Title, Text } = Typography;

// ── Generator log table columns ────────────────────────────────────────────────
function genColumns(
  canManage: boolean,
  onStop: (id: string) => void,
): ColumnsType<GeneratorLog> {
  return [
    {
      title: 'Location',
      dataIndex: 'location',
      key: 'location',
      width: 175,
      render: (v: string) => <Text strong style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'Reason',
      dataIndex: 'runReason',
      key: 'runReason',
      width: 135,
      render: (v: GeneratorRunReason) => {
        const m = GENERATOR_RUN_REASON_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (v: string) => (
        <Badge
          status={v === 'Running' ? 'processing' : 'default'}
          text={<Text style={{ fontSize: 12 }}>{v}</Text>}
        />
      ),
    },
    {
      title: 'Start Time',
      dataIndex: 'startTime',
      key: 'startTime',
      width: 140,
      render: (v: string) => (
        <Tooltip title={dayjs(v).format('D MMM YYYY, HH:mm')}>
          <Text type="secondary" style={{ fontSize: 12 }}>{dayjs(v).fromNow()}</Text>
        </Tooltip>
      ),
    },
    {
      title: 'Runtime',
      dataIndex: 'runtimeHours',
      key: 'runtimeHours',
      width: 100,
      render: (v?: number, r?: GeneratorLog) => {
        if (r?.status === 'Running') {
          const hrs = dayjs().diff(dayjs(r.startTime), 'minute') / 60;
          return <Text style={{ color: '#52c41a', fontSize: 12 }}>{hrs.toFixed(1)}h (live)</Text>;
        }
        return v != null
          ? <Text style={{ fontSize: 12 }}>{v.toFixed(1)}h</Text>
          : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>;
      },
    },
    {
      title: 'Fuel Used (L)',
      dataIndex: 'fuelConsumed',
      key: 'fuelConsumed',
      width: 110,
      render: (v?: number) => v != null
        ? <Text style={{ fontSize: 12 }}>{v.toFixed(0)} L</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>,
    },
    {
      title: 'Outage Cause',
      dataIndex: 'outageCause',
      key: 'outageCause',
      ellipsis: true,
      render: (v?: string) => v
        ? <Text style={{ fontSize: 12 }}>{v}</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>,
    },
    {
      title: 'Logged By',
      dataIndex: 'loggedByName',
      key: 'loggedByName',
      width: 140,
      render: (v: string) => <Text type="secondary" style={{ fontSize: 12 }}>{v}</Text>,
    },
    ...(canManage ? [{
      title: '',
      key: 'actions',
      width: 80,
      render: (_: unknown, r: GeneratorLog) =>
        r.status === 'Running' ? (
          <Button size="small" danger icon={<StopOutlined />}
            onClick={() => onStop(r.id)}>
            Stop
          </Button>
        ) : null,
    }] : []),
  ];
}

// ── Diesel record table columns ────────────────────────────────────────────────
function dieselColumns(): ColumnsType<DieselRecord> {
  return [
    {
      title: 'Date',
      dataIndex: 'recordDate',
      key: 'recordDate',
      width: 115,
      render: (v: string) => <Text style={{ fontSize: 12 }}>{dayjs(v).format('D MMM YYYY')}</Text>,
    },
    {
      title: 'Type',
      dataIndex: 'recordType',
      key: 'recordType',
      width: 105,
      render: (v: DieselRecordType) => {
        const m = DIESEL_RECORD_TYPE_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Qty (L)',
      dataIndex: 'quantityLitres',
      key: 'quantityLitres',
      width: 90,
      render: (v: number) => <Text strong style={{ fontSize: 13 }}>{v.toLocaleString()} L</Text>,
    },
    {
      title: 'Unit Cost (₦)',
      dataIndex: 'unitCostNaira',
      key: 'unitCostNaira',
      width: 120,
      render: (v: number) => v > 0
        ? <Text style={{ fontSize: 12 }}>₦{v.toLocaleString()}</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>,
    },
    {
      title: 'Total (₦)',
      dataIndex: 'totalCostNaira',
      key: 'totalCostNaira',
      width: 130,
      render: (v: number) => v > 0
        ? <Text strong style={{ fontSize: 12, color: '#cf1322' }}>₦{v.toLocaleString()}</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>,
    },
    {
      title: 'Supplier / Destination',
      key: 'supplyDest',
      ellipsis: true,
      render: (_: unknown, r: DieselRecord) => (
        <Text style={{ fontSize: 12 }}>{r.supplier ?? r.destination ?? '—'}</Text>
      ),
    },
    {
      title: 'Requested By',
      dataIndex: 'requestedByName',
      key: 'requestedByName',
      width: 145,
      ellipsis: true,
      render: (v: string) => <Text type="secondary" style={{ fontSize: 12 }}>{v}</Text>,
    },
    {
      title: 'Approved',
      dataIndex: 'approvedByName',
      key: 'approvedByName',
      width: 60,
      render: (v?: string) => v
        ? <Tooltip title={v}><Badge status="success" /></Tooltip>
        : <Badge status="warning" />,
    },
  ];
}

// ── Main page ──────────────────────────────────────────────────────────────────
export default function FuelPage() {
  const user      = useAuthStore(s => s.user);
  const role      = user?.role;
  const qc        = useQueryClient();
  const canManage = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';

  const [activeTab,     setActiveTab]     = useState('generator');
  const [genPage,       setGenPage]       = useState(1);
  const [dieselPage,    setDieselPage]    = useState(1);
  const [dieselType,    setDieselType]    = useState<string | undefined>();
  const [genModalOpen,  setGenModalOpen]  = useState(false);
  const [genModalMode,  setGenModalMode]  = useState<'start' | 'stop'>('start');
  const [stopTargetId,  setStopTargetId]  = useState<string | undefined>();
  const [dieselOpen,    setDieselOpen]    = useState(false);

  const refresh = useCallback(() => {
    qc.invalidateQueries({ queryKey: ['fuel'] });
  }, [qc]);

  // Summary stats
  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ['fuel', 'summary'],
    queryFn:  fuelApi.summary,
    refetchInterval: 30_000,
  });

  // Generator logs
  const { data: genData, isFetching: genFetching } = useQuery({
    queryKey: ['fuel', 'generator', genPage],
    queryFn:  () => fuelApi.listGeneratorLogs({ page: genPage, pageSize: 15 }),
    enabled:  activeTab === 'generator',
  });

  // Diesel records
  const { data: dieselData, isFetching: dieselFetching } = useQuery({
    queryKey: ['fuel', 'diesel', dieselType, dieselPage],
    queryFn:  () => fuelApi.listDieselRecords({ recordType: dieselType, page: dieselPage, pageSize: 15 }),
    enabled:  activeTab === 'diesel',
  });

  const openStop = (id: string) => {
    setStopTargetId(id);
    setGenModalMode('stop');
    setGenModalOpen(true);
  };

  const openStart = () => {
    setGenModalMode('start');
    setStopTargetId(undefined);
    setGenModalOpen(true);
  };

  const genRunning = summary?.generator.currentlyRunning ?? 0;

  return (
    <div>
      {/* ── Header ──────────────────────────────────────────────── */}
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Fuel &amp; Power</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            Generator runtime, diesel consumption, and power outage tracking
          </Text>
        </Col>
        <Col>
          <Space>
            <Tooltip title="Refresh">
              <Button icon={<ReloadOutlined />} onClick={refresh} loading={genFetching || dieselFetching} />
            </Tooltip>
            {activeTab === 'generator' && (
              <Button type="primary" icon={<PlayCircleOutlined />} onClick={openStart}>
                Start Generator
              </Button>
            )}
            {activeTab === 'diesel' && (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setDieselOpen(true)}>
                Add Diesel Record
              </Button>
            )}
          </Space>
        </Col>
      </Row>

      {/* ── Generator running alert ──────────────────────────────── */}
      {genRunning > 0 && (
        <Alert
          type="info"
          showIcon
          icon={<ThunderboltOutlined />}
          message={
            <span>
              <strong>{genRunning} generator{genRunning > 1 ? 's are' : ' is'} currently running.</strong>
              {' '}Remember to log the stop when power is restored.
            </span>
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {/* ── KPI cards ────────────────────────────────────────────── */}
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
        {[
          {
            label: 'Runtime This Month',
            value: summary?.generator.totalRuntimeHoursThisMonth?.toFixed(1) + 'h',
            rawVal: summary?.generator.totalRuntimeHoursThisMonth,
            icon: <ThunderboltOutlined />,
            bg: '#f0f5ff', color: '#1677ff',
            loading: summaryLoading,
          },
          {
            label: 'Outages This Month',
            value: summary?.generator.outagesThisMonth,
            rawVal: summary?.generator.outagesThisMonth,
            icon: <StopOutlined />,
            bg: '#fff1f0', color: '#ff4d4f',
            loading: summaryLoading,
          },
          {
            label: 'Fuel Consumed (Mo.)',
            value: (summary?.generator.totalFuelConsumedThisMonth?.toFixed(0) ?? '0') + ' L',
            rawVal: summary?.generator.totalFuelConsumedThisMonth,
            icon: <FireOutlined />,
            bg: '#fff7e6', color: '#fa8c16',
            loading: summaryLoading,
          },
          {
            label: 'Diesel Purchased (Mo.)',
            value: (summary?.diesel.totalPurchasedLitresThisMonth?.toFixed(0) ?? '0') + ' L',
            rawVal: summary?.diesel.totalPurchasedLitresThisMonth,
            icon: <DropboxOutlined />,
            bg: '#f6ffed', color: '#52c41a',
            loading: summaryLoading,
          },
          {
            label: 'Spend This Month',
            value: '₦' + (summary?.diesel.totalSpendThisMonth?.toLocaleString() ?? '0'),
            rawVal: summary?.diesel.totalSpendThisMonth,
            icon: <FireOutlined />,
            bg: '#fff0f6', color: '#eb2f96',
            loading: summaryLoading,
          },
          {
            label: 'Stock Estimate',
            value: (summary?.diesel.currentStockLitres?.toFixed(0) ?? '0') + ' L',
            rawVal: summary?.diesel.currentStockLitres,
            icon: <DropboxOutlined />,
            bg: '#f9f0ff', color: '#722ed1',
            loading: summaryLoading,
          },
        ].map(s => (
          <Col xs={12} sm={8} md={8} lg={4} key={s.label}>
            <Card styles={{ body: { padding: '14px 18px' } }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <div style={{
                  width: 40, height: 40, borderRadius: 8, background: s.bg,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  color: s.color, fontSize: 18, flexShrink: 0,
                }}>
                  {s.icon}
                </div>
                <div>
                  <div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1.2, color: s.color }}>
                    {s.loading ? '…' : s.value}
                  </div>
                  <div style={{ fontSize: 11, color: '#8c8c8c', marginTop: 2 }}>{s.label}</div>
                </div>
              </div>
            </Card>
          </Col>
        ))}
      </Row>

      {/* ── Generator / Diesel tabs ───────────────────────────────── */}
      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '0 24px', borderBottom: '1px solid #f0f0f0' }}>
          <Tabs
            activeKey={activeTab}
            onChange={k => setActiveTab(k)}
            items={[
              { key: 'generator', label: <Space><ThunderboltOutlined />Generator Log</Space>           },
              { key: 'daily',     label: <Space><FireOutlined />Daily Readings</Space>                 },
              { key: 'power',     label: <Space><PlayCircleOutlined />Power Meter (NPA)</Space>        },
              { key: 'diesel',    label: <Space><DropboxOutlined />Diesel Records</Space>              },
              { key: 'tanklog',   label: <Space><DropboxOutlined />Tank Level Log</Space>              },
            ]}
            size="small"
          />
        </div>

        {/* ── Generator Log tab ──────────────────────────────────── */}
        {activeTab === 'generator' && (
          <Table<GeneratorLog>
            columns={genColumns(canManage, openStop)}
            dataSource={genData?.items ?? []}
            rowKey="id"
            loading={genFetching}
            pagination={{
              current:  genPage,
              pageSize: 15,
              total:    genData?.totalCount ?? 0,
              onChange: p => setGenPage(p),
              showTotal: (total, [from, to]) => `${from}–${to} of ${total} sessions`,
              showSizeChanger: false,
            }}
            rowClassName={r => r.status === 'Running' ? 'ant-table-row-running' : ''}
            size="middle"
            style={{ padding: '0 8px' }}
            scroll={{ x: 900 }}
          />
        )}

        {/* ── Daily Generator Readings tab ───────────────────────── */}
        {activeTab === 'daily' && (
          <div style={{ padding: 20 }}>
            <DailyReadingsTab />
          </div>
        )}

        {/* ── Power Meter (NPA) tab ──────────────────────────────── */}
        {activeTab === 'power' && (
          <div style={{ padding: 20 }}>
            <PowerMeterTab />
          </div>
        )}

        {/* ── Diesel Records tab ─────────────────────────────────── */}
        {activeTab === 'diesel' && (
          <>
            <div style={{ padding: '16px 24px', borderBottom: '1px solid #f0f0f0' }}>
              <Select
                placeholder="Filter by type"
                allowClear
                style={{ width: 160 }}
                value={dieselType}
                onChange={v => { setDieselType(v); setDieselPage(1); }}
                options={Object.entries(DIESEL_RECORD_TYPE_META).map(([k, m]) => ({
                  value: k, label: m.label,
                }))}
              />
            </div>
            <Table<DieselRecord>
              columns={dieselColumns()}
              dataSource={dieselData?.items ?? []}
              rowKey="id"
              loading={dieselFetching}
              pagination={{
                current:  dieselPage,
                pageSize: 15,
                total:    dieselData?.totalCount ?? 0,
                onChange: p => setDieselPage(p),
                showTotal: (total, [from, to]) => `${from}–${to} of ${total} records`,
                showSizeChanger: false,
              }}
              size="middle"
              style={{ padding: '0 8px' }}
              scroll={{ x: 850 }}
            />
          </>
        )}

        {/* ── Tank Level Log tab ─────────────────────────────────── */}
        {activeTab === 'tanklog' && (
          <div style={{ padding: 20 }}>
            <DieselTankTab />
          </div>
        )}
      </Card>

      {/* ── Modals ───────────────────────────────────────────────── */}
      <LogGeneratorModal
        open={genModalOpen}
        mode={genModalMode}
        runningId={stopTargetId}
        onClose={() => setGenModalOpen(false)}
        onDone={refresh}
      />

      <AddDieselModal
        open={dieselOpen}
        onClose={() => setDieselOpen(false)}
        onDone={refresh}
      />
    </div>
  );
}
