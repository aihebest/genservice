import { useState } from 'react';
import {
  Badge, Button, Card, Col, List, Progress, Row,
  Select, Space, Statistic, Tabs, Tag, Tooltip, Typography,
} from 'antd';
import {
  BarChartOutlined, ToolOutlined, ThunderboltOutlined,
  ReloadOutlined, CheckCircleOutlined, WarningOutlined,
  ClockCircleOutlined, FireOutlined,
} from '@ant-design/icons';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartTooltip,
  ResponsiveContainer, PieChart, Pie, Cell, Legend, LineChart, Line, Area, AreaChart,
} from 'recharts';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { reportsApi } from '../../api/reports.api';
import type { ReportPeriod, PeriodBreakdownItem } from '../../api/reports.api';
import {
  CATEGORY_META, STATUS_META, PRIORITY_META,
  MAINTENANCE_CATEGORY_META, GENERATOR_RUN_REASON_META,
} from '../../types';
import type { RequestCategory, RequestStatus, RequestPriority, MaintenanceCategory, GeneratorRunReason } from '../../types';

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

// ── Breakdown bar list ─────────────────────────────────────────────────────────
function BreakdownList({ items, total, getLabelColor }: {
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

  const priorityData = data.byPriority.map(b => ({
    name:  PRIORITY_META[b.label as RequestPriority]?.label ?? b.label,
    count: b.count,
  }));

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
                    percent > 0.05 ? `${name} ${(percent * 100).toFixed(0)}%` : ''
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
                  {catData.map((entry, i) => (
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
                  label={({ name, percent }) => percent > 0.08 ? `${name} ${(percent * 100).toFixed(0)}%` : ''}
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
                  label={({ name, percent }) => percent > 0.08 ? `${(percent * 100).toFixed(0)}%` : ''}
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
        ]}
        style={{ background: 'white', padding: '0 16px 16px', borderRadius: 8 }}
      />
    </div>
  );
}
