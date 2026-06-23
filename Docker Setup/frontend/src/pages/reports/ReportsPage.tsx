import { useState } from 'react';
import {
  Alert, Badge, Button, Card, Col, Dropdown, List, Progress, Row,
  Select, Space, Statistic, Table, Tabs, Tag, Tooltip, Typography,
} from 'antd';
import {
  BarChartOutlined, DownloadOutlined, ToolOutlined, ThunderboltOutlined,
  ReloadOutlined, CheckCircleOutlined, WarningOutlined,
  ClockCircleOutlined, FireOutlined, CarOutlined,
  HomeOutlined, TeamOutlined, BankOutlined,
} from '@ant-design/icons';
import {
  exportVehicleRegister,
  exportEquipmentMaintenance,
  exportFacilityMaintenance,
  exportGeneratorLog,
  exportMaintenanceSchedules,
} from '../../api/export.api';
import { taskProgressLogApi } from '../../api/taskProgressLog.api';
import type { TechnicianSummary } from '../../types';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartTooltip,
  ResponsiveContainer, PieChart, Pie, Cell, Legend, Area, AreaChart,
} from 'recharts';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { reportsApi } from '../../api/reports.api';
import type { ReportPeriod, PeriodBreakdownItem } from '../../api/reports.api';
import {
  CATEGORY_META, STATUS_META,
  MAINTENANCE_CATEGORY_META, GENERATOR_RUN_REASON_META,
} from '../../types';
import type { RequestCategory, RequestStatus, MaintenanceCategory, GeneratorRunReason } from '../../types';

const { Title, Text } = Typography;

// ── Colour palettes ────────────────────────────────────────────────────────────
const CHART_COLORS = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#eb2f96', '#13c2c2', '#f5222d', '#a0d911'];

const PERIOD_OPTIONS = [
  { value: '7d',  label: 'Last 7 days'  },
  { value: '30d', label: 'Last 30 days' },
  { value: '90d', label: 'Last 90 days' },
];

// ── Reusable chart card ────────────────────────────────────────────────────────
function ChartCard({ title, children, height = 220 }: { title: string; children: React.ReactNode; height?: number }) {
  return (
    <Card size="small" title={<Text strong style={{ fontSize: 13 }}>{title}</Text>}
      styles={{ body: { padding: '8px 12px' } }}>
      <div style={{ height }}>{children}</div>
    </Card>
  );
}

// ── KPI row card ───────────────────────────────────────────────────────────────
function KpiCard({ label, value, color, icon, suffix }: {
  label: string; value: string | number; color: string; icon: React.ReactNode; suffix?: string;
}) {
  return (
    <Card size="small" styles={{ body: { padding: '14px 16px' } }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          width: 38, height: 38, borderRadius: 8, background: color + '18',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          color, fontSize: 17, flexShrink: 0,
        }}>{icon}</div>
        <div>
          <div style={{ fontSize: 20, fontWeight: 700, lineHeight: 1.2, color }}>
            {value}{suffix}
          </div>
          <div style={{ fontSize: 11, color: '#8c8c8c', marginTop: 2 }}>{label}</div>
        </div>
      </div>
    </Card>
  );
}

export function BreakdownList({ items, total, getLabelColor }: {
  items: PeriodBreakdownItem[];
  total: number;
  getLabelColor?: (label: string) => string;
}) {
  if (!items.length) return <Text type="secondary">No data</Text>;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {items.map(item => {
        const pct = total > 0 ? Math.round(item.count / total * 100) : 0;
        const color = getLabelColor ? getLabelColor(item.label) : '#1677ff';
        return (
          <div key={item.label}>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 3 }}>
              <Text style={{ fontSize: 12 }}>{item.label}</Text>
              <Text style={{ fontSize: 12 }}>{item.count} <Text type="secondary">({pct}%)</Text></Text>
            </div>
            <Progress percent={pct} showInfo={false} strokeColor={color} size="small" />
          </div>
        );
      })}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════════
//  Tab 1 — Request Reports
// ══════════════════════════════════════════════════════════════════════════════
function RequestReportTab({ period }: { period: ReportPeriod }) {
  const { data, isLoading } = useQuery({
    queryKey: ['reports', 'requests', period],
    queryFn:  () => reportsApi.requests(period),
  });

  if (isLoading) return <Card loading style={{ minHeight: 300 }} />;
  if (!data)     return null;

  const catData = data.byCategory.map(b => ({
    name:  CATEGORY_META[b.label as RequestCategory]?.label ?? b.label,
    count: b.count,
  }));

  const statusData = data.byStatus.map(b => ({
    name:  STATUS_META[b.label as RequestStatus]?.label ?? b.label,
    count: b.count,
    color: STATUS_META[b.label as RequestStatus]?.color,
  }));

  void 0; // priorityData removed

  const trendData = data.submissionTrend.map(t => ({ date: t.date, submissions: t.value }));

  return (
    <div>
      {/* KPIs */}
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        {[
          { label: 'Total Requests',    value: data.totalRequests,         color: '#1677ff', icon: <BarChartOutlined /> },
          { label: 'Open',              value: data.openRequests,          color: '#fa8c16', icon: <ClockCircleOutlined /> },
          { label: 'Completed',         value: data.completedRequests,     color: '#52c41a', icon: <CheckCircleOutlined /> },
          { label: 'Pending Approval',  value: data.pendingApproval,       color: '#fa8c16', icon: <ClockCircleOutlined /> },
          { label: 'Rejected',          value: data.rejectedRequests,      color: '#f5222d', icon: <WarningOutlined /> },
          { label: 'Completion Rate',   value: data.completionRatePercent, color: '#13c2c2', icon: <CheckCircleOutlined />, suffix: '%' },
        ].map(k => (
          <Col xs={12} sm={8} md={4} key={k.label}>
            <KpiCard {...k} value={k.value} />
          </Col>
        ))}
      </Row>

      {/* Charts row 1 */}
      <Row gutter={[12, 12]} style={{ marginBottom: 12 }}>
        <Col xs={24} md={14}>
          <ChartCard title="Requests by Category" height={240}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={catData} margin={{ top: 5, right: 10, left: -20, bottom: 40 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} angle={-35} textAnchor="end" interval={0} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <RechartTooltip />
                <Bar dataKey="count" name="Requests" fill="#1677ff" radius={[3, 3, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
        <Col xs={24} md={10}>
          <ChartCard title="Status Distribution" height={240}>
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie data={statusData} dataKey="count" nameKey="name"
                  cx="50%" cy="50%" outerRadius={80} label={({ name, percent }) =>
                    percent != null && percent > 0.05 ? `${name} ${((percent ?? 0) * 100).toFixed(0)}%` : ''
                  } labelLine={false} fontSize={10}>
                  {statusData.map((_, i) => (
                    <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                  ))}
                </Pie>
                <Legend iconSize={10} wrapperStyle={{ fontSize: 11 }} />
              </PieChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
      </Row>

      {/* Charts row 2 */}
      <Row gutter={[12, 12]}>
        <Col xs={24} md={16}>
          <ChartCard title="Daily Submission Trend" height={200}>
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={trendData} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="subGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#1677ff" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#1677ff" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="date" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <RechartTooltip />
                <Area type="monotone" dataKey="submissions" stroke="#1677ff"
                  fill="url(#subGrad)" name="Submissions" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
        <Col xs={24} md={8}>
          <Card size="small" title={<Text strong style={{ fontSize: 13 }}>Top Requesters</Text>}
            styles={{ body: { padding: '12px 16px' } }} style={{ height: '100%' }}>
            <List
              size="small"
              dataSource={data.topRequesters}
              renderItem={(item, idx) => (
                <List.Item style={{ padding: '6px 0' }}>
                  <Space>
                    <Tag style={{ fontSize: 11, minWidth: 20, textAlign: 'center' }}>#{idx + 1}</Tag>
                    <Text style={{ fontSize: 12 }}>{item.label}</Text>
                  </Space>
                  <Text strong style={{ fontSize: 12 }}>{item.count}</Text>
                </List.Item>
              )}
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════════
//  Tab 2 — Maintenance Reports
// ══════════════════════════════════════════════════════════════════════════════
function MaintenanceReportTab({ period }: { period: ReportPeriod }) {
  const { data, isLoading } = useQuery({
    queryKey: ['reports', 'maintenance', period],
    queryFn:  () => reportsApi.maintenance(period),
  });

  if (isLoading) return <Card loading style={{ minHeight: 300 }} />;
  if (!data)     return null;

  const catData = data.byCategory.map(b => ({
    name:  MAINTENANCE_CATEGORY_META[b.label as MaintenanceCategory]?.label ?? b.label,
    count: b.count,
    color: MAINTENANCE_CATEGORY_META[b.label as MaintenanceCategory]?.color,
  }));

  const freqData = data.byFrequency.map(b => ({ name: b.label, count: b.count }));

  return (
    <div>
      {/* ── Export toolbar ─────────────────────────────────────────────────── */}
      <Row justify="end" style={{ marginBottom: 12 }}>
        <Space>
          <Dropdown menu={{ items: [
            { key: 'eq-excel', label: 'Equipment Register (Excel)', icon: <DownloadOutlined />,
              onClick: () => exportEquipmentMaintenance({ format: 'excel' }) },
            { key: 'eq-pdf',   label: 'Equipment Register (PDF)',   icon: <DownloadOutlined />,
              onClick: () => exportEquipmentMaintenance({ format: 'pdf' }) },
          ] }}>
            <Button icon={<DownloadOutlined />}>Equipment</Button>
          </Dropdown>
          <Dropdown menu={{ items: [
            { key: 'fac-excel', label: 'Facility Register (Excel)', icon: <DownloadOutlined />,
              onClick: () => exportFacilityMaintenance({ format: 'excel' }) },
            { key: 'fac-pdf',   label: 'Facility Register (PDF)',   icon: <DownloadOutlined />,
              onClick: () => exportFacilityMaintenance({ format: 'pdf' }) },
          ] }}>
            <Button icon={<DownloadOutlined />}>Facility</Button>
          </Dropdown>
          <Dropdown menu={{ items: [
            { key: 'sched-excel-all',    label: 'Scheduler — All (Excel)',     icon: <DownloadOutlined />,
              onClick: () => exportMaintenanceSchedules({ format: 'excel', status: 'all' }) },
            { key: 'sched-excel-active', label: 'Scheduler — Active (Excel)',  icon: <DownloadOutlined />,
              onClick: () => exportMaintenanceSchedules({ format: 'excel', status: 'active' }) },
            { key: 'sched-excel-due',    label: 'Scheduler — Overdue (Excel)', icon: <DownloadOutlined />,
              onClick: () => exportMaintenanceSchedules({ format: 'excel', status: 'overdue' }) },
            { type: 'divider' },
            { key: 'sched-pdf-all',      label: 'Scheduler — All (PDF)',       icon: <DownloadOutlined />,
              onClick: () => exportMaintenanceSchedules({ format: 'pdf', status: 'all' }) },
            { key: 'sched-pdf-active',   label: 'Scheduler — Active (PDF)',    icon: <DownloadOutlined />,
              onClick: () => exportMaintenanceSchedules({ format: 'pdf', status: 'active' }) },
          ] }}>
            <Button icon={<DownloadOutlined />}>Scheduler</Button>
          </Dropdown>
        </Space>
      </Row>

      {/* KPIs */}
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        {[
          { label: 'Total Schedules',   value: data.totalSchedules,        color: '#1677ff', icon: <ToolOutlined /> },
          { label: 'Overdue',           value: data.overdueCount,          color: '#f5222d', icon: <WarningOutlined /> },
          { label: 'Completed (Period)', value: data.completedThisPeriod,  color: '#52c41a', icon: <CheckCircleOutlined /> },
          { label: 'Due This Week',     value: data.dueSoon,               color: '#fa8c16', icon: <ClockCircleOutlined /> },
          { label: 'Compliance Rate',   value: data.complianceRatePercent, color: '#13c2c2', icon: <CheckCircleOutlined />, suffix: '%' },
        ].map(k => (
          <Col xs={12} sm={8} md={5} key={k.label}>
            <KpiCard {...k} value={k.value} />
          </Col>
        ))}
      </Row>

      {/* Compliance progress */}
      <Card size="small" style={{ marginBottom: 12 }}
        title={<Text strong style={{ fontSize: 13 }}>Maintenance Compliance</Text>}
        styles={{ body: { padding: '16px 20px' } }}>
        <Row gutter={24} align="middle">
          <Col flex="auto">
            <Progress
              percent={data.complianceRatePercent}
              strokeColor={data.complianceRatePercent >= 80 ? '#52c41a' : data.complianceRatePercent >= 50 ? '#fa8c16' : '#f5222d'}
              format={p => <Text strong>{p}%</Text>}
              size={['100%', 16] as any}
            />
          </Col>
          <Col>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {data.completedThisPeriod} completed of tasks due in {data.periodLabel.toLowerCase()}
            </Text>
          </Col>
        </Row>
      </Card>

      {/* Charts */}
      <Row gutter={[12, 12]} style={{ marginBottom: 12 }}>
        <Col xs={24} md={14}>
          <ChartCard title="Schedules by Category" height={220}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={catData} margin={{ top: 5, right: 10, left: -20, bottom: 40 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} angle={-35} textAnchor="end" interval={0} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <RechartTooltip />
                <Bar dataKey="count" name="Schedules" radius={[3, 3, 0, 0]}>
                  {catData.map((_entry, i) => (
                    <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
        <Col xs={24} md={10}>
          <ChartCard title="By Frequency" height={220}>
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie data={freqData} dataKey="count" nameKey="name"
                  cx="50%" cy="50%" outerRadius={80}
                  label={({ name, percent }) => percent != null && percent > 0.08 ? `${name} ${((percent ?? 0) * 100).toFixed(0)}%` : ''}
                  labelLine={false} fontSize={10}>
                  {freqData.map((_, i) => (
                    <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                  ))}
                </Pie>
                <Legend iconSize={10} wrapperStyle={{ fontSize: 11 }} />
              </PieChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
      </Row>

      {/* Recent completions */}
      <Card size="small"
        title={<Text strong style={{ fontSize: 13 }}>Recent Completions</Text>}
        styles={{ body: { padding: 0 } }}>
        <List
          size="small"
          dataSource={data.recentCompletions}
          locale={{ emptyText: 'No completions in this period' }}
          renderItem={item => {
            const catMeta = MAINTENANCE_CATEGORY_META[item.category as MaintenanceCategory];
            return (
              <List.Item style={{ padding: '10px 16px' }}>
                <List.Item.Meta
                  title={<Text style={{ fontSize: 13 }}>{item.taskName}</Text>}
                  description={
                    <Space size={6}>
                      <Tag color={catMeta?.color} style={{ fontSize: 10 }}>{catMeta?.label ?? item.category}</Tag>
                      <Text type="secondary" style={{ fontSize: 11 }}>{item.location}</Text>
                      {item.completedByName && (
                        <Text type="secondary" style={{ fontSize: 11 }}>by {item.completedByName}</Text>
                      )}
                    </Space>
                  }
                />
                <Text type="secondary" style={{ fontSize: 11 }}>
                  {dayjs(item.completedAt).format('D MMM YYYY')}
                </Text>
              </List.Item>
            );
          }}
        />
      </Card>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════════
//  Tab 3 — Fuel & Power Reports
// ══════════════════════════════════════════════════════════════════════════════
function FuelReportTab({ period }: { period: ReportPeriod }) {
  const { data, isLoading } = useQuery({
    queryKey: ['reports', 'fuel', period],
    queryFn:  () => reportsApi.fuel(period),
  });

  if (isLoading) return <Card loading style={{ minHeight: 300 }} />;
  if (!data)     return null;

  const outageReasonData = data.outagesByReason.map(b => ({
    name:  GENERATOR_RUN_REASON_META[b.label as GeneratorRunReason]?.label ?? b.label,
    count: b.count,
  }));

  const runtimeData  = data.runtimeTrend.map(t => ({ date: t.date, hours: t.value }));
  const dieselData   = data.dieselUsageTrend.map(t => ({ date: t.date, litres: t.value }));

  return (
    <div>
      {/* ── Export toolbar ─────────────────────────────────────────────────── */}
      <Row justify="end" style={{ marginBottom: 12 }}>
        <Button icon={<DownloadOutlined />} onClick={() => exportGeneratorLog({})}>
          Export Generator Log (Excel)
        </Button>
      </Row>

      {/* Generator KPIs */}
      <Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 8 }}>Generator Performance</Text>
      <Row gutter={[12, 12]} style={{ marginBottom: 12 }}>
        {[
          { label: 'Total Runtime',       value: `${data.totalRuntimeHours}h`, color: '#1677ff', icon: <ThunderboltOutlined /> },
          { label: 'Power Outages',       value: data.totalOutages,            color: '#f5222d', icon: <WarningOutlined /> },
          { label: 'Fuel Consumed',       value: `${data.totalFuelConsumedLitres}L`, color: '#fa8c16', icon: <FireOutlined /> },
          { label: 'Avg Outage Duration', value: `${data.avgOutageDurationHours}h`,  color: '#722ed1', icon: <ClockCircleOutlined /> },
        ].map(k => (
          <Col xs={12} sm={6} key={k.label}>
            <KpiCard {...k} />
          </Col>
        ))}
      </Row>

      {/* Diesel KPIs */}
      <Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 8 }}>Diesel Inventory</Text>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        {[
          { label: 'Purchased',       value: `${data.totalPurchasedLitres}L`,       color: '#52c41a', icon: <FireOutlined /> },
          { label: 'Dispensed',       value: `${data.totalDispensedLitres}L`,       color: '#13c2c2', icon: <FireOutlined /> },
          { label: 'Total Spend',     value: `₦${data.totalSpendNaira.toLocaleString()}`, color: '#eb2f96', icon: <FireOutlined /> },
          { label: 'Stock Estimate',  value: `${data.currentStockEstimateLitres}L`, color: '#1677ff', icon: <FireOutlined /> },
        ].map(k => (
          <Col xs={12} sm={6} key={k.label}>
            <KpiCard {...k} />
          </Col>
        ))}
      </Row>

      {/* Charts */}
      <Row gutter={[12, 12]} style={{ marginBottom: 12 }}>
        <Col xs={24} md={12}>
          <ChartCard title="Daily Generator Runtime (hours)" height={200}>
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={runtimeData} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
                <defs>
                  <linearGradient id="rtGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#1677ff" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#1677ff" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="date" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <RechartTooltip />
                <Area type="monotone" dataKey="hours" name="Runtime (h)"
                  stroke="#1677ff" fill="url(#rtGrad)" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
        <Col xs={24} md={12}>
          <ChartCard title="Daily Diesel Dispensed (litres)" height={200}>
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={dieselData} margin={{ top: 5, right: 10, left: -15, bottom: 0 }}>
                <defs>
                  <linearGradient id="dlGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#fa8c16" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#fa8c16" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="date" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <RechartTooltip />
                <Area type="monotone" dataKey="litres" name="Litres Dispensed"
                  stroke="#fa8c16" fill="url(#dlGrad)" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
      </Row>

      <Row gutter={[12, 12]}>
        <Col xs={24} md={10}>
          <ChartCard title="Outages by Reason" height={200}>
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie data={outageReasonData} dataKey="count" nameKey="name"
                  cx="50%" cy="50%" outerRadius={70}
                  label={({ percent }) => percent != null && percent > 0.08 ? `${((percent ?? 0) * 100).toFixed(0)}%` : ''}
                  labelLine={false} fontSize={10}>
                  {outageReasonData.map((_, i) => (
                    <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                  ))}
                </Pie>
                <Legend iconSize={10} wrapperStyle={{ fontSize: 11 }} />
              </PieChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
        <Col xs={24} md={14}>
          <Card size="small"
            title={<Text strong style={{ fontSize: 13 }}>Recent Generator Sessions</Text>}
            styles={{ body: { padding: 0 } }}>
            <List
              size="small"
              dataSource={data.recentSessions}
              locale={{ emptyText: 'No sessions in this period' }}
              renderItem={item => {
                const rm = GENERATOR_RUN_REASON_META[item.runReason as GeneratorRunReason];
                return (
                  <List.Item style={{ padding: '8px 14px' }}>
                    <List.Item.Meta
                      title={
                        <Space size={6}>
                          <Text style={{ fontSize: 12 }}>{item.location}</Text>
                          <Tag color={rm?.color} style={{ fontSize: 10 }}>{rm?.label ?? item.runReason}</Tag>
                          {item.status === 'Running' && <Badge status="processing" text={<Text style={{ fontSize: 10, color: '#52c41a' }}>Running</Text>} />}
                        </Space>
                      }
                      description={
                        <Text type="secondary" style={{ fontSize: 11 }}>
                          {dayjs(item.startTime).format('D MMM, HH:mm')}
                          {item.runtimeHours != null && ` · ${item.runtimeHours.toFixed(1)}h`}
                          {item.fuelConsumed  != null && ` · ${item.fuelConsumed.toFixed(0)}L used`}
                          {item.outageCause && ` · ${item.outageCause}`}
                        </Text>
                      }
                    />
                  </List.Item>
                );
              }}
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════════
//  Main Page
// ══════════════════════════════════════════════════════════════════════════════
export default function ReportsPage() {
  const [activeTab, setActiveTab] = useState('requests');
  const [period, setPeriod]       = useState<ReportPeriod>('30d');
  const qc = useQueryClient();

  const refresh = () => qc.invalidateQueries({ queryKey: ['reports'] });

  return (
    <div>
      {/* Header */}
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Reports &amp; Analytics</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            Operational insights across requests, maintenance, and fuel &amp; power
          </Text>
        </Col>
        <Col>
          <Space>
            <Select
              value={period}
              onChange={p => setPeriod(p)}
              options={PERIOD_OPTIONS}
              style={{ width: 140 }}
            />
            <Tooltip title="Refresh reports">
              <Button icon={<ReloadOutlined />} onClick={refresh} />
            </Tooltip>
          </Space>
        </Col>
      </Row>

      {/* Tabs */}
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        size="small"
        items={[
          {
            key:      'requests',
            label:    <Space><BarChartOutlined />Request Reports</Space>,
            children: <RequestReportTab period={period} />,
          },
          {
            key:      'maintenance',
            label:    <Space><ToolOutlined />Maintenance</Space>,
            children: <MaintenanceReportTab period={period} />,
          },
          {
            key:      'fuel',
            label:    <Space><ThunderboltOutlined />Fuel &amp; Power</Space>,
            children: <FuelReportTab period={period} />,
          },
          {
            key:      'vehicle',
            label:    <Space><CarOutlined />Vehicle Maintenance</Space>,
            children: <VehicleReportTab period={period} />,
          },
          {
            key:      'facility',
            label:    <Space><HomeOutlined />Facility Maintenance</Space>,
            children: <FacilityReportTab period={period} />,
          },
          {
            key:      'generator',
            label:    <Space><FireOutlined />Generator Report</Space>,
            children: <GeneratorReportTab period={period} />,
          },
          {
            key:      'technicians',
            label:    <Space><TeamOutlined />Technician Activity</Space>,
            children: <TechnicianReportTab />,
          },
          {
            key:      'accommodation',
            label:    <Space><BankOutlined />Accommodation</Space>,
            children: <AccommodationReportTab period={period} />,
          },
        ]}
        style={{ background: 'white', padding: '0 16px 16px', borderRadius: 8 }}
      />
    </div>
  );
}

// ── Vehicle Maintenance Report (full register view) ───────────────────────────
type VehiclePerRow = {
  vehicleRegNo:    string;
  vehicleType:     string;
  totalJobs:       number;
  completedJobs:   number;
  activeJobs:      number;
  totalSparesCost: number;
  lastServiceDate: string | null;
  currentStatus:   string | null;
};
type LongStandingRow = {
  requestNumber:  string;
  vehicleRegNo:   string;
  vehicleType:    string;
  status:         string;
  workshopName:   string | null;
  faultIdentified:string | null;
  daysInWorkshop: number;
  sentToWorkshopAt:string;
};
type HistoryRow = {
  requestNumber:  string;
  vehicleRegNo:   string;
  vehicleType:    string;
  maintenanceType:string;
  status:         string;
  priority:       string;
  workshopName:   string | null;
  faultIdentified:string | null;
  workDone:       string | null;
  sparesCostNaira:number | null;
  daysOpen:       number;
  createdAt:      string;
  completedAt:    string | null;
};
type MonthlyTrendRow = { month: string; completed: number; newJobs: number };
type StatusByTypeRow = {
  type: string; pending: number; inWorkshop: number;
  awaitingParts: number; awaitingFunds: number; completed: number; rejected: number;
};

function VehicleReportTab({ period }: { period: ReportPeriod }) {
  const [filterReg, setFilterReg] = useState<string | undefined>();

  // Aggregate report (period-scoped totals)
  const { data: agg, isFetching: aggLoading } = useQuery({
    queryKey: ['reports', 'vehicle', period],
    queryFn: () => reportsApi.vehicle(period),
  });

  // Full register report (all-time, per-vehicle)
  const { data: reg, isFetching: regLoading } = useQuery({
    queryKey: ['reports', 'vehicle-register', filterReg],
    queryFn: () => reportsApi.vehicleRegister(filterReg),
  });

  const isLoading = aggLoading || regLoading;
  if (isLoading || !agg || !reg) return <div style={{ padding: 32, textAlign: 'center' }}>Loading…</div>;

  const a  = agg  as Record<string, unknown>;
  const r  = reg  as Record<string, unknown>;

  const perVehicle    = (r.perVehicle    as VehiclePerRow[])    ?? [];
  const longStanding  = (r.longStanding  as LongStandingRow[])  ?? [];
  const history       = (r.history       as HistoryRow[])        ?? [];
  const monthlyTrends = (r.monthlyTrends as MonthlyTrendRow[])  ?? [];
  const statusByType  = (r.statusByType  as StatusByTypeRow[])  ?? [];
  const sparesCost    = (r.sparesCostSummary as { label: string; totalSparesCost: number; count: number }[]) ?? [];

  const fmtCost = (v: number) => v > 0
    ? `₦${v.toLocaleString('en-NG', { minimumFractionDigits: 0 })}` : '—';

  const statusColor = (s: string) =>
    ({ Pending:'orange', Approved:'blue', InWorkshop:'purple', AwaitingParts:'gold',
       AwaitingFunds:'volcano', Completed:'green', Rejected:'red' }[s] ?? 'default');

  // ── Per-vehicle register table columns ──────────────────────────────────────
  const registerCols = [
    { title: 'Reg No',        dataIndex: 'vehicleRegNo',    width: 120,
      render: (v: string) => <strong>{v}</strong> },
    { title: 'Vehicle Type',  dataIndex: 'vehicleType',     width: 130,
      render: (v: string) => <Tag>{v}</Tag> },
    { title: 'Total Jobs',    dataIndex: 'totalJobs',       width: 90,
      render: (v: number) => <strong>{v}</strong> },
    { title: 'Active',        dataIndex: 'activeJobs',      width: 80,
      render: (v: number) => v > 0 ? <Tag color="blue">{v}</Tag> : <Text type="secondary">0</Text> },
    { title: 'Completed',     dataIndex: 'completedJobs',   width: 90,
      render: (v: number) => <Tag color="green">{v}</Tag> },
    { title: 'Total Spares Cost', dataIndex: 'totalSparesCost', width: 140,
      sorter: (a: VehiclePerRow, b: VehiclePerRow) => a.totalSparesCost - b.totalSparesCost,
      render: (v: number) => <span style={{ color: v > 0 ? '#722ed1' : '#bfbfbf' }}>{fmtCost(v)}</span> },
    { title: 'Last Serviced', dataIndex: 'lastServiceDate', width: 120,
      render: (v: string | null) => v ? dayjs(v).format('DD MMM YYYY') : <Text type="secondary">—</Text> },
    { title: 'Current Status', dataIndex: 'currentStatus', width: 130,
      render: (v: string | null) => v
        ? <Tag color={statusColor(v)}>{v}</Tag> : <Text type="secondary">—</Text> },
    { title: '',              key: 'action',                width: 80,
      render: (_: unknown, row: VehiclePerRow) => (
        <Button size="small" onClick={() => setFilterReg(
          filterReg === row.vehicleRegNo ? undefined : row.vehicleRegNo
        )}>
          {filterReg === row.vehicleRegNo ? 'Clear' : 'History'}
        </Button>
      ) },
  ];

  // ── History table columns ────────────────────────────────────────────────────
  const historyCols = [
    { title: 'Ref No',          dataIndex: 'requestNumber',   width: 110 },
    { title: 'Reg No',          dataIndex: 'vehicleRegNo',    width: 110,
      render: (v: string) => <Tag color="blue">{v}</Tag> },
    { title: 'Type',            dataIndex: 'maintenanceType', width: 130,
      render: (v: string) => <Tag>{v}</Tag> },
    { title: 'Status',          dataIndex: 'status',          width: 130,
      render: (v: string) => <Tag color={statusColor(v)}>{v}</Tag> },
    { title: 'Workshop',        dataIndex: 'workshopName',    width: 130,
      render: (v: string | null) => v ?? '—' },
    { title: 'Fault',           dataIndex: 'faultIdentified', width: 200,
      ellipsis: true,
      render: (v: string | null) => v ?? '—' },
    { title: 'Spares Cost',     dataIndex: 'sparesCostNaira', width: 120,
      render: (v: number | null) => v != null ? fmtCost(v) : '—' },
    { title: 'Days Open',       dataIndex: 'daysOpen',        width: 80,
      render: (v: number) => v > 0
        ? <Tag color={v > 14 ? 'red' : v > 7 ? 'orange' : 'default'}>{v}d</Tag>
        : <Tag color="green">Done</Tag> },
    { title: 'Date',            dataIndex: 'createdAt',       width: 110,
      render: (v: string) => dayjs(v).format('DD MMM YYYY') },
  ];

  // ── Long-standing table columns ──────────────────────────────────────────────
  const longStandingCols = [
    { title: 'Ref No',          dataIndex: 'requestNumber',  width: 110 },
    { title: 'Reg No',          dataIndex: 'vehicleRegNo',   width: 120,
      render: (v: string) => <strong>{v}</strong> },
    { title: 'Status',          dataIndex: 'status',         width: 130,
      render: (v: string) => <Tag color={statusColor(v)}>{v}</Tag> },
    { title: 'Workshop',        dataIndex: 'workshopName',   width: 150,
      render: (v: string | null) => v ?? '—' },
    { title: 'Fault',           dataIndex: 'faultIdentified',width: 200,
      ellipsis: true,
      render: (v: string | null) => v ?? '—' },
    { title: 'Days in Workshop', dataIndex: 'daysInWorkshop', width: 130,
      sorter: (a: LongStandingRow, b: LongStandingRow) => a.daysInWorkshop - b.daysInWorkshop,
      render: (v: number) => (
        <Tag color={v > 30 ? 'red' : v > 14 ? 'orange' : 'gold'}>
          ⚠️ {v} days
        </Tag>
      ) },
  ];

  return (
    <div>
      {/* ── Export toolbar ─────────────────────────────────────────────────── */}
      <Row justify="end" style={{ marginBottom: 12 }}>
        <Dropdown
          menu={{
            items: [
              { key: 'excel', label: 'Export Register (Excel)', icon: <DownloadOutlined />,
                onClick: () => exportVehicleRegister({ format: 'excel', regNo: filterReg }) },
              { key: 'pdf',   label: 'Export Register (PDF)',   icon: <DownloadOutlined />,
                onClick: () => exportVehicleRegister({ format: 'pdf',   regNo: filterReg }) },
            ],
          }}
        >
          <Button icon={<DownloadOutlined />}>Export Register</Button>
        </Dropdown>
      </Row>

      {/* ── KPI summary ───────────────────────────────────────────────────── */}
      <Row gutter={12} style={{ marginBottom: 20 }}>
        {[
          { label: 'Total Requests (period)', value: a.total      as number, color: '#1677ff' },
          { label: 'Active Jobs',             value: r.activeJobsCount as number, color: '#722ed1' },
          { label: 'In Workshop (period)',     value: a.inWorkshop as number, color: '#fa8c16' },
          { label: 'Long-Standing (>7d)',      value: r.longStandingCount as number, color: '#f5222d' },
          { label: 'Completed (period)',       value: a.completed  as number, color: '#52c41a' },
          { label: 'Total Spares Spend',
            value: `₦${((r.totalSparesCostAll as number) ?? 0).toLocaleString('en-NG', { maximumFractionDigits: 0 })}`,
            color: '#722ed1' },
        ].map(k => (
          <Col key={k.label} style={{ flex: '1 1 130px', minWidth: 120, marginBottom: 8 }}>
            <Card size="small" styles={{ body: { padding: '10px 14px' } }}>
              <Statistic
                title={<Text style={{ fontSize: 11 }}>{k.label}</Text>}
                value={k.value ?? 0}
                valueStyle={{ color: k.color, fontSize: 18, fontWeight: 700 }}
              />
            </Card>
          </Col>
        ))}
      </Row>

      {/* ── Long-standing alert ────────────────────────────────────────────── */}
      {longStanding.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={`${longStanding.length} vehicle(s) have been in workshop for more than 7 days`}
          style={{ marginBottom: 16 }}
        />
      )}

      {/* ── Charts row ────────────────────────────────────────────────────── */}
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={24} md={12}>
          <ChartCard title="Monthly Trends — New Jobs vs Completions" height={200}>
            <ResponsiveContainer>
              <AreaChart data={monthlyTrends}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 10 }} />
                <RechartTooltip />
                <Legend />
                <Area type="monotone" dataKey="newJobs"   name="New Jobs"   stroke="#1677ff" fill="#1677ff20" />
                <Area type="monotone" dataKey="completed" name="Completed"  stroke="#52c41a" fill="#52c41a20" />
              </AreaChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
        <Col xs={24} md={12}>
          <ChartCard title="Spares Cost by Vehicle (Top 10)" height={200}>
            <ResponsiveContainer>
              <BarChart data={sparesCost.slice(0, 10)} layout="vertical">
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis type="number" tick={{ fontSize: 9 }}
                  tickFormatter={v => `₦${(v/1000).toFixed(0)}k`} />
                <YAxis type="category" dataKey="label" tick={{ fontSize: 10 }} width={90} />
                <RechartTooltip
                  formatter={(v) => `₦${Number(v ?? 0).toLocaleString('en-NG')}`} />
                <Bar dataKey="totalSparesCost" name="Spares Cost" fill="#722ed1" />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
      </Row>

      {/* ── Status × Type breakdown ────────────────────────────────────────── */}
      <Card
        title="Status Breakdown by Maintenance Type"
        size="small"
        style={{ marginBottom: 16 }}
      >
        <Table
          dataSource={statusByType}
          rowKey="type"
          size="small"
          pagination={false}
          scroll={{ x: 'max-content' }}
          columns={[
            { title: 'Type',          dataIndex: 'type',          width: 150 },
            { title: 'Pending',       dataIndex: 'pending',       width: 80,
              render: (v: number) => <Tag color="orange">{v}</Tag> },
            { title: 'In Workshop',   dataIndex: 'inWorkshop',    width: 100,
              render: (v: number) => <Tag color="purple">{v}</Tag> },
            { title: 'Awaiting Parts',dataIndex: 'awaitingParts', width: 120,
              render: (v: number) => <Tag color="gold">{v}</Tag> },
            { title: 'Awaiting Funds',dataIndex: 'awaitingFunds', width: 120,
              render: (v: number) => <Tag color="volcano">{v}</Tag> },
            { title: 'Completed',     dataIndex: 'completed',     width: 90,
              render: (v: number) => <Tag color="green">{v}</Tag> },
            { title: 'Rejected',      dataIndex: 'rejected',      width: 90,
              render: (v: number) => <Tag color="red">{v}</Tag> },
          ]}
        />
      </Card>

      {/* ── Long-standing vehicles ──────────────────────────────────────────── */}
      {longStanding.length > 0 && (
        <Card
          title={<><WarningOutlined style={{ color: '#f5222d', marginRight: 6 }} />Long-Standing Vehicles (&gt;7 days in workshop)</>}
          size="small"
          style={{ marginBottom: 16 }}
        >
          <Table
            dataSource={longStanding}
            rowKey="requestNumber"
            size="small"
            columns={longStandingCols}
            pagination={false}
            scroll={{ x: 800 }}
          />
        </Card>
      )}

      {/* ── Per-vehicle register ─────────────────────────────────────────────── */}
      <Card
        title="Vehicle Register Summary (All-Time)"
        size="small"
        style={{ marginBottom: 16 }}
        extra={filterReg
          ? <Button size="small" onClick={() => setFilterReg(undefined)}>Show All Vehicles</Button>
          : null}
      >
        <Table
          dataSource={perVehicle}
          rowKey="vehicleRegNo"
          size="small"
          columns={registerCols}
          scroll={{ x: 900 }}
          pagination={{ pageSize: 15, showTotal: t => `${t} vehicles` }}
          rowClassName={(row: VehiclePerRow) =>
            filterReg === row.vehicleRegNo ? 'ant-table-row-selected' : ''}
        />
      </Card>

      {/* ── History (all or per-vehicle) ─────────────────────────────────────── */}
      <Card
        title={filterReg
          ? `Maintenance History — ${filterReg}`
          : 'Recent Maintenance Records'}
        size="small"
        extra={filterReg
          ? <Button size="small" onClick={() => setFilterReg(undefined)}>Clear Filter</Button>
          : <Text type="secondary">Click "History" on a vehicle above to filter</Text>}
      >
        <Table
          dataSource={history}
          rowKey="requestNumber"
          size="small"
          columns={historyCols}
          scroll={{ x: 1000 }}
          pagination={{ pageSize: 10, showTotal: t => `${t} records` }}
        />
      </Card>
    </div>
  );
}

// ── Facility Maintenance Report ───────────────────────────────────────────────
function FacilityReportTab({ period }: { period: ReportPeriod }) {
  const { data: d, isFetching } = useQuery({
    queryKey: ['reports', 'facility', period],
    queryFn: () => reportsApi.facility(period),
  });

  if (isFetching || !d) return <div style={{ padding: 32, textAlign: 'center' }}>Loading…</div>;
  const r = d as Record<string, unknown>;

  return (
    <div>
      {/* ── Export toolbar ─────────────────────────────────────────────────── */}
      <Row justify="end" style={{ marginBottom: 12 }}>
        <Dropdown menu={{ items: [
          { key: 'fac-excel', label: 'Export Register (Excel)', icon: <DownloadOutlined />,
            onClick: () => exportFacilityMaintenance({ format: 'excel' }) },
          { key: 'fac-pdf',   label: 'Export Register (PDF)',   icon: <DownloadOutlined />,
            onClick: () => exportFacilityMaintenance({ format: 'pdf' }) },
        ] }}>
          <Button icon={<DownloadOutlined />}>Export Register</Button>
        </Dropdown>
      </Row>

      <Row gutter={12} style={{ marginBottom: 20 }}>
        {[
          { label: 'Total',           value: r.total          as number, color: '#1677ff' },
          { label: 'Pending',         value: r.pending        as number, color: '#fa8c16' },
          { label: 'Ongoing',         value: r.ongoing        as number, color: '#722ed1' },
          { label: 'Awaiting Spares', value: r.awaitingSpares as number, color: '#d4a015' },
          { label: 'Awaiting Funds',  value: r.awaitingFunds  as number, color: '#fa541c' },
          { label: 'Completed',       value: r.completed      as number, color: '#52c41a' },
        ].map(k => (
          <Col key={k.label} style={{ flex: '1 1 110px', minWidth: 100, marginBottom: 8 }}>
            <Card size="small" styles={{ body: { padding: '10px 14px' } }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>{k.label}</Text>}
                value={k.value ?? 0} valueStyle={{ color: k.color, fontSize: 20, fontWeight: 700 }} />
            </Card>
          </Col>
        ))}
      </Row>
      <Row gutter={16}>
        <Col xs={24} md={12}>
          <ChartCard title="By Work Type">
            <ResponsiveContainer>
              <BarChart data={(r.byType as PeriodBreakdownItem[] ?? [])}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <RechartTooltip />
                <Bar dataKey="count" fill="#52c41a" />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
        <Col xs={24} md={12}>
          <ChartCard title="By End User / Department">
            <ResponsiveContainer>
              <PieChart>
                <Pie data={(r.byEndUser as PeriodBreakdownItem[] ?? [])} dataKey="count" nameKey="label"
                  cx="50%" cy="50%" outerRadius={80} label={({ percent }: { name?: string; percent?: number }) => `${((percent ?? 0)*100).toFixed(0)}%`}>
                  {(r.byEndUser as PeriodBreakdownItem[] ?? []).map((_: PeriodBreakdownItem, i: number) =>
                    <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />)}
                </Pie>
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
      </Row>
    </div>
  );
}

// ── Generator Report ──────────────────────────────────────────────────────────
function GeneratorReportTab({ period }: { period: ReportPeriod }) {
  const { data: d, isFetching } = useQuery({
    queryKey: ['reports', 'generator', period],
    queryFn: () => reportsApi.generator(period),
  });

  if (isFetching || !d) return <div style={{ padding: 32, textAlign: 'center' }}>Loading…</div>;
  const r = d as Record<string, unknown>;

  type GenStatus = { assetDescription: string; location: string; cumulativeRunHours: number; fuelLevelLitres: number; latestStatus: string; serviceAlertActive: boolean; hoursUntilNextService: number };

  return (
    <div>
      <Row gutter={12} style={{ marginBottom: 20 }}>
        {[
          { label: 'Generators Tracked',    value: r.generatorsTracked      as number, color: '#1677ff' },
          { label: `Run Hours (${r.periodLabel})`, value: r.totalRunHoursPeriod as number, color: '#722ed1', suffix: ' h' },
          { label: 'Fuel Consumed (L)',      value: r.totalFuelConsumed      as number, color: '#fa8c16', suffix: ' L' },
          { label: 'Service Alerts',         value: r.serviceAlerts          as number, color: (r.serviceAlerts as number) > 0 ? '#f5222d' : '#52c41a' },
        ].map(k => (
          <Col key={k.label} style={{ flex: '1 1 140px', minWidth: 130, marginBottom: 8 }}>
            <Card size="small" styles={{ body: { padding: '12px 16px' } }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>{k.label}</Text>}
                value={k.value ?? 0} suffix={k.suffix ?? ''}
                valueStyle={{ color: k.color, fontSize: 22, fontWeight: 700 }} />
            </Card>
          </Col>
        ))}
      </Row>
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={24}>
          <ChartCard title="Daily Run Hours Trend" height={200}>
            <ResponsiveContainer>
              <AreaChart data={(r.runHoursTrend as { date: string; value: number }[] ?? [])}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <RechartTooltip />
                <Area type="monotone" dataKey="value" stroke="#722ed1" fill="#f0e6ff" name="Run Hours" />
              </AreaChart>
            </ResponsiveContainer>
          </ChartCard>
        </Col>
      </Row>
      <Card size="small" title={<Text strong style={{ fontSize: 13 }}>Fleet Status</Text>}>
        {(r.fleetStatus as GenStatus[] ?? []).map((g: GenStatus, i: number) => (
          <div key={i} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center',
            padding: '8px 0', borderBottom: '1px solid #f5f5f5', flexWrap: 'wrap', gap: 8 }}>
            <div>
              <Text strong style={{ fontSize: 13 }}>{g.assetDescription}</Text>
              <Text type="secondary" style={{ fontSize: 12, marginLeft: 8 }}>{g.location}</Text>
            </div>
            <Space wrap>
              <Text style={{ fontSize: 12 }}>{(g.cumulativeRunHours ?? 0).toLocaleString()} h cumulative</Text>
              <Text style={{ fontSize: 12 }}>{(g.fuelLevelLitres ?? 0).toLocaleString()} L fuel</Text>
              {g.serviceAlertActive
                ? <Tag color="red" icon={<WarningOutlined />}>Service Alert — {(g.hoursUntilNextService ?? 0).toFixed(0)}h left</Tag>
                : <Tag color="green"><CheckCircleOutlined /> {(g.hoursUntilNextService ?? 0).toFixed(0)}h to service</Tag>}
            </Space>
          </div>
        ))}
      </Card>
    </div>
  );
}

// ── Technician Activity Report ────────────────────────────────────────────────
function TechnicianReportTab() {
  const { data: techs = [], isFetching } = useQuery({
    queryKey: ['reports', 'technicians'],
    queryFn: taskProgressLogApi.performance,
  });

  return (
    <div>
      <Row gutter={12} style={{ marginBottom: 20 }}>
        {[
          { label: 'Technicians',      value: techs.length, color: '#1677ff' },
          { label: 'Total Assigned',   value: techs.reduce((s: number, t: TechnicianSummary) => s + t.totalAssigned, 0), color: '#722ed1' },
          { label: 'Total Completed',  value: techs.reduce((s: number, t: TechnicianSummary) => s + t.completed, 0), color: '#52c41a' },
          { label: "Today's Logs",     value: techs.reduce((s: number, t: TechnicianSummary) => s + t.todayLogs, 0), color: '#1677ff' },
          { label: 'Active Blockers',  value: techs.reduce((s: number, t: TechnicianSummary) => s + t.awaitingMaterials + t.awaitingVendor, 0), color: '#fa541c' },
        ].map(k => (
          <Col key={k.label} style={{ flex: '1 1 130px', minWidth: 120, marginBottom: 8 }}>
            <Card size="small" styles={{ body: { padding: '12px 16px' } }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>{k.label}</Text>}
                value={k.value} valueStyle={{ color: k.color, fontSize: 22, fontWeight: 700 }} />
            </Card>
          </Col>
        ))}
      </Row>
      <Card size="small" title={<Text strong>Technician Performance Summary</Text>} loading={isFetching}>
        {techs.map((t: TechnicianSummary) => {
          const rate = t.totalAssigned > 0 ? Math.round((t.completed / t.totalAssigned) * 100) : 0;
          return (
            <div key={t.email} style={{ padding: '12px 0', borderBottom: '1px solid #f5f5f5' }}>
              <Row gutter={8} align="middle">
                <Col flex="160px"><Text strong>{t.name}</Text></Col>
                <Col flex="80px"><Tag color="blue">{t.totalAssigned} assigned</Tag></Col>
                <Col flex="80px"><Tag color="green">{t.completed} done</Tag></Col>
                <Col flex="80px"><Tag color="processing">{t.inProgress} active</Tag></Col>
                <Col flex="130px">
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <div style={{ flex: 1 }}>
                      <Progress percent={rate} size="small" showInfo={false}
                        strokeColor={rate >= 70 ? '#52c41a' : rate >= 40 ? '#fa8c16' : '#ff4d4f'} />
                    </div>
                    <Text style={{ fontSize: 12, width: 32 }}>{rate}%</Text>
                  </div>
                </Col>
                <Col flex="auto">
                  <Space wrap size={4}>
                    {t.awaitingMaterials > 0 && <Tag color="gold">{t.awaitingMaterials} Spares</Tag>}
                    {t.awaitingVendor > 0 && <Tag color="orange">{t.awaitingVendor} Vendor</Tag>}
                    <Text type="secondary" style={{ fontSize: 11 }}>Today: {t.todayLogs} logs | Week: {t.weekLogs}</Text>
                  </Space>
                </Col>
              </Row>
            </div>
          );
        })}
        {techs.length === 0 && !isFetching && (
          <Text type="secondary">No assignment data yet.</Text>
        )}
      </Card>
    </div>
  );
}

// ── Accommodation Report ──────────────────────────────────────────────────────
function AccommodationReportTab({ period }: { period: ReportPeriod }) {
  const { data: d, isFetching } = useQuery({
    queryKey: ['reports', 'accommodation', period],
    queryFn: () => reportsApi.accommodation(period),
  });

  if (isFetching || !d) return <div style={{ padding: 32, textAlign: 'center' }}>Loading…</div>;
  const r = d as Record<string, unknown>;

  type AccomReq = { ticketNumber: string; title: string; status: string; priority: string; location: string; requestedByName: string; approvedByName?: string; createdAt: string; approvedAt?: string };

  return (
    <div>
      <Row gutter={12} style={{ marginBottom: 20 }}>
        {[
          { label: 'Total Requests', value: r.total    as number, color: '#1677ff' },
          { label: 'Pending',        value: r.pending  as number, color: '#fa8c16' },
          { label: 'Approved',       value: r.approved as number, color: '#52c41a' },
          { label: 'Completed',      value: r.completed as number, color: '#52c41a' },
          { label: 'Rejected',       value: r.rejected  as number, color: '#f5222d' },
        ].map(k => (
          <Col key={k.label} style={{ flex: '1 1 130px', minWidth: 120, marginBottom: 8 }}>
            <Card size="small" styles={{ body: { padding: '12px 16px' } }}>
              <Statistic title={<Text style={{ fontSize: 12 }}>{k.label}</Text>}
                value={k.value ?? 0} valueStyle={{ color: k.color, fontSize: 22, fontWeight: 700 }} />
            </Card>
          </Col>
        ))}
      </Row>
      <Card size="small" title={<Text strong>Accommodation Requests ({r.periodLabel as string})</Text>}>
        {(r.requests as AccomReq[] ?? []).map((req: AccomReq, i: number) => (
          <div key={i} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center',
            padding: '8px 0', borderBottom: '1px solid #f5f5f5', flexWrap: 'wrap', gap: 8 }}>
            <Space>
              <Text code style={{ fontSize: 11 }}>{req.ticketNumber}</Text>
              <Text style={{ fontSize: 13 }}>{req.title}</Text>
            </Space>
            <Space wrap>
              <Text type="secondary" style={{ fontSize: 12 }}>{req.location}</Text>
              <Text type="secondary" style={{ fontSize: 12 }}>{req.requestedByName}</Text>
              <Tag color={STATUS_META[req.status as import('../../types').RequestStatus]?.color ?? 'default'}>
                {STATUS_META[req.status as import('../../types').RequestStatus]?.label ?? req.status}
              </Tag>
            </Space>
          </div>
        ))}
        {(r.requests as AccomReq[] ?? []).length === 0 && (
          <Text type="secondary">No accommodation requests in this period.</Text>
        )}
      </Card>
    </div>
  );
}
