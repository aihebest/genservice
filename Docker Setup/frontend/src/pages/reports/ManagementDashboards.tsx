import { useState } from 'react';
import type { ReactNode } from 'react';
import {
  Alert, Card, Col, DatePicker, Empty, Progress, Row, Segmented, Select, Space, Spin, Statistic, Tag, Typography,
} from 'antd';
import { useQuery } from '@tanstack/react-query';
import dayjs from 'dayjs';
import {
  Bar, BarChart, CartesianGrid, Cell, LabelList, ResponsiveContainer, Tooltip as ChartTooltip, XAxis, YAxis,
} from 'recharts';
import { analyticsApi } from '../../api/analytics.api';
import type { AssetCost, MonthlyCost, SplitRow } from '../../api/analytics.api';
import FilterableTable from '../../components/shared/FilterableTable';

const { Text, Title } = Typography;
const { RangePicker } = DatePicker;

/**
 * Live versions of the department's Excel reports:
 *   • Summary of Repairs and Maintenance Register
 *   • Overall Diesel / PHED Purchase Analysis
 *   • Per-location Consumption Analysis
 * Figures come straight from the modules, so there is no monthly spreadsheet to
 * prepare. Date pickers stand in for the Excel timeline / slicers.
 */

const naira = (v?: number | null) => `₦${(v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 2 })}`;
const short = (v: number) =>
  v >= 1_000_000 ? `${(v / 1_000_000).toFixed(1)}M` : v >= 1_000 ? `${(v / 1_000).toFixed(0)}K` : `${Math.round(v)}`;
const ORANGE = '#c55a11', GREEN = '#92d050', BLUE = '#4472c4';
const REGION_COLOURS: Record<string, string> = {
  LAGOS: '#4472c4', 'PORT HARCOURT': '#ed7d31', UYO: '#a9d18e', ABUJA: '#ffc000', OTHER: '#a5a5a5',
};

type Range = [dayjs.Dayjs, dayjs.Dayjs] | null;
const toParams = (r: Range) => ({ from: r?.[0]?.format('YYYY-MM-DD'), to: r?.[1]?.format('YYYY-MM-DD') });

function Section({ title, children, extra }: { title: string; children: ReactNode; extra?: ReactNode }) {
  return (
    <Card size="small" title={<Text strong style={{ fontSize: 13 }}>{title}</Text>} extra={extra}
      style={{ marginBottom: 16 }} styles={{ body: { padding: 16 } }}>
      {children}
    </Card>
  );
}

/** Horizontal percentage bars — the "split per end user" panels. */
function SplitBars({ rows, colour, value }: {
  rows: { label: string; percent: number; value: string }[]; colour: string; value?: boolean;
}) {
  if (rows.length === 0) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No records in this period" />;
  return (
    <div>
      {rows.map(r => (
        <div key={r.label} style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
          <Text style={{ width: 120, fontSize: 12, textAlign: 'right' }} ellipsis>{r.label}</Text>
          <div style={{ flex: 1 }}>
            <Progress percent={r.percent} showInfo={false} strokeColor={colour} size="small" />
          </div>
          <Text style={{ width: value === false ? 60 : 170, fontSize: 12 }}>
            {r.percent.toFixed(2)}%{value === false ? '' : <Text type="secondary" style={{ fontSize: 11 }}> · {r.value}</Text>}
          </Text>
        </div>
      ))}
    </div>
  );
}

/** Monthly cost bars with the month-on-month percentage change above each bar. */
function MonthlyChange({ data }: { data: MonthlyCost[] }) {
  if (data.length === 0) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No records in this period" />;
  return (
    <ResponsiveContainer width="100%" height={240}>
      <BarChart data={data} margin={{ top: 24, right: 8, left: 8, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" vertical={false} />
        <XAxis dataKey="month" tick={{ fontSize: 11 }} />
        <YAxis tickFormatter={short} tick={{ fontSize: 11 }} />
        <ChartTooltip formatter={(raw: unknown) => { const v = Number(raw); return naira(v); }} />
        <Bar dataKey="cost" name="Maintenance cost" fill={BLUE} maxBarSize={48}>
          <LabelList dataKey="changePercent" position="top"
            content={(p: { x?: unknown; y?: unknown; width?: unknown; value?: unknown }) => {
              if (p.value === null || p.value === undefined || p.value === false) return null;
              const v = Number(p.value);
              return (
                <text x={Number(p.x) + Number(p.width) / 2} y={Number(p.y) - 6} textAnchor="middle"
                  fontSize={11} fill={v >= 0 ? '#389e0d' : '#cf1322'}>
                  {v >= 0 ? '▲' : '▼'} {Math.abs(v)}%
                </text>
              );
            }} />
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

// ═══════════════════════════ 1. MAINTENANCE ═══════════════════════════════════

function MaintenanceDashboard() {
  const [range, setRange] = useState<Range>(null);
  const [kind, setKind]   = useState<'vehicle' | 'equipment' | 'facility'>('vehicle');
  const { data, isFetching } = useQuery({
    queryKey: ['reports', 'analytics', 'maintenance', toParams(range)],
    queryFn:  () => analyticsApi.maintenance(toParams(range)),
  });

  if (!data) return <Spin style={{ display: 'block', margin: '48px auto' }} />;
  const split = (rows: SplitRow[]) => rows.map(r => ({ label: r.label, percent: r.percent, value: naira(r.cost) }));
  const assets: AssetCost[] = data.byAsset[kind];
  const kindLabel = { vehicle: 'Vehicle', equipment: 'Equipment', facility: 'Facility' }[kind];

  return (
    <Spin spinning={isFetching}>
      <Space style={{ marginBottom: 12 }} wrap>
        <Text type="secondary">Date of request:</Text>
        <RangePicker value={range} onChange={v => setRange(v as Range)} format="D MMM YYYY" allowClear
          presets={[
            { label: 'This month', value: [dayjs().startOf('month'), dayjs()] },
            { label: 'This year',  value: [dayjs().startOf('year'),  dayjs()] },
            { label: 'Last year',  value: [dayjs().subtract(1, 'year').startOf('year'), dayjs().subtract(1, 'year').endOf('year')] },
          ]} />
        <Text type="secondary" style={{ fontSize: 12 }}>{range ? '' : 'Showing all time'}</Text>
      </Space>

      {data.uncosted > 0 && (
        <Alert type="warning" showIcon style={{ marginBottom: 12 }}
          message={`${data.uncosted} completed job(s) have no cost recorded, so the totals below understate actual spend. Add the Final Amount on those records.`} />
      )}

      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        {[
          { label: 'Overall Maintenance Cost',   v: data.totals.overall,   bg: '#1f3864', fg: '#fff' },
          { label: 'Vehicle Maintenance Cost',   v: data.totals.vehicle },
          { label: 'Equipment Maintenance Cost', v: data.totals.equipment },
          { label: 'Facility Maintenance Cost',  v: data.totals.facility },
        ].map(c => (
          <Col xs={24} sm={12} lg={6} key={c.label}>
            <Card size="small" style={{ background: c.bg, textAlign: 'center' }}>
              <div style={{ fontSize: 12, color: c.fg ?? '#8c8c8c' }}>{c.label}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: c.fg }}>{naira(c.v)}</div>
            </Card>
          </Col>
        ))}
      </Row>

      <Row gutter={16}>
        <Col xs={24} lg={6}>
          <Section title="Work Completion Tracker">
            {(['equipment', 'vehicle', 'facility'] as const).map(k => (
              <Row key={k} gutter={8} style={{ marginBottom: 10 }} align="middle">
                <Col span={12}><Statistic title={<Text style={{ fontSize: 11 }}>Closed-out {k}</Text>}
                  value={data.tracker[k].closed} valueStyle={{ fontSize: 20, color: '#389e0d' }} /></Col>
                <Col span={12}><Statistic title={<Text style={{ fontSize: 11 }}>Open {k}</Text>}
                  value={data.tracker[k].open} valueStyle={{ fontSize: 20, color: '#d46b08' }} /></Col>
              </Row>
            ))}
          </Section>
        </Col>
        <Col xs={24} lg={6}><Section title="Vehicle cost per location"><SplitBars rows={split(data.vehicleByRegion)} colour={BLUE} /></Section></Col>
        <Col xs={24} lg={6}><Section title="Equipment cost per end user"><SplitBars rows={split(data.equipmentByEndUser)} colour={BLUE} /></Section></Col>
        <Col xs={24} lg={6}><Section title="Facility cost per end user"><SplitBars rows={split(data.facilityByEndUser)} colour={BLUE} /></Section></Col>
      </Row>

      <Section title="Overall Maintenance Cost / Location">
        {data.overallByRegion.length === 0
          ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No records in this period" />
          : (
            <ResponsiveContainer width="100%" height={260}>
              <BarChart data={data.overallByRegion} margin={{ top: 24, right: 8, left: 8, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis tickFormatter={short} tick={{ fontSize: 11 }} />
                <ChartTooltip formatter={(raw: unknown) => { const v = Number(raw); return naira(v); }} />
                <Bar dataKey="cost" name="Maintenance cost" maxBarSize={90}>
                  {data.overallByRegion.map(r => <Cell key={r.label} fill={REGION_COLOURS[r.label] ?? '#a5a5a5'} />)}
                  <LabelList dataKey="cost" position="top" formatter={(raw: unknown) => { const v = Number(raw); return naira(v); }} style={{ fontSize: 11 }} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          )}
      </Section>

      <Segmented value={kind} onChange={v => setKind(v as typeof kind)} style={{ marginBottom: 12 }}
        options={[{ value: 'vehicle', label: 'Vehicle' }, { value: 'equipment', label: 'Equipment' }, { value: 'facility', label: 'Facility' }]} />

      <Row gutter={16}>
        <Col xs={24} lg={12}>
          <Section title={`Monthly ${kindLabel} Maintenance Cost / Percentage Change`}>
            <MonthlyChange data={data.monthly[kind]} />
          </Section>
        </Col>
        <Col xs={24} lg={12}>
          <Section title={`Top 5 ${kindLabel === 'Facility' ? 'Facilities' : kindLabel + 's'} with Highest Maintenance Cost`}>
            {assets.slice(0, 5).map((a, i) => (
              <div key={a.label} style={{ display: 'flex', gap: 8, padding: '6px 0', borderBottom: '1px solid #f5f5f5' }}>
                <Text style={{ width: 20 }} type="secondary">{i + 1}.</Text>
                <Text style={{ flex: 1, fontSize: 12 }} ellipsis>{a.label}</Text>
                <Text strong style={{ fontSize: 12 }}>{naira(a.cost)}</Text>
                <Tag style={{ fontSize: 10 }}>{a.region}</Tag>
              </div>
            ))}
            {assets.length === 0 && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No records in this period" />}
          </Section>
        </Col>
      </Row>

      <Section title={`Each ${kindLabel} Repair / Maintenance Cost`}>
        <FilterableTable<AssetCost> rowKey="label" size="small" dataSource={assets}
          sticky={false} pagination={{ pageSize: 10, showSizeChanger: false }}
          columns={[
            { title: kind === 'vehicle' ? 'Vehicle Reg No.' : kind === 'equipment' ? 'Asset Description' : 'Location',
              dataIndex: 'label', key: 'label', ellipsis: true },
            { title: 'Region', dataIndex: 'region', key: 'region', width: 150 },
            { title: 'Jobs', dataIndex: 'jobs', key: 'jobs', width: 90 },
            { title: 'Maintenance Cost', dataIndex: 'cost', key: 'cost', width: 180,
              defaultSortOrder: 'descend', render: (v: number) => <Text strong>{naira(v)}</Text> },
          ]} />
      </Section>
    </Spin>
  );
}

// ═══════════════════════ 2. DIESEL / PHED PURCHASE ════════════════════════════

function EnergyPurchaseDashboard() {
  const [range, setRange] = useState<Range>([dayjs().startOf('year'), dayjs()]);
  const [dUser, setDUser] = useState<string | undefined>();
  const [pUser, setPUser] = useState<string | undefined>();
  const { data, isFetching } = useQuery({
    queryKey: ['reports', 'analytics', 'energy', toParams(range)],
    queryFn:  () => analyticsApi.energyPurchase(toParams(range)),
  });
  if (!data) return <Spin style={{ display: 'block', margin: '48px auto' }} />;

  const d = data.diesel, p = data.phed;
  const dUsers = d.byEndUser.map(x => x.label), pUsers = p.byEndUser.map(x => x.label);
  const dSel = dUser && dUsers.includes(dUser) ? dUser : dUsers[0];
  const pSel = pUser && pUsers.includes(pUser) ? pUser : pUsers[0];
  // Total cost per month across all end users.
  const sumByMonth = <T extends { month: string; cost: number }>(rows: T[]) =>
    Object.values(rows.reduce<Record<string, { month: string; cost: number }>>((acc, r) => {
      (acc[r.month] ??= { month: r.month, cost: 0 }).cost += r.cost; return acc;
    }, {}));

  const chart = (rows: object[], key: string, colour: string, unit: string) => rows.length === 0
    ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No records in this period" />
    : (
      <ResponsiveContainer width="100%" height={220}>
        <BarChart data={rows} margin={{ top: 22, right: 8, left: 8, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="month" tick={{ fontSize: 11 }} />
          <YAxis tickFormatter={short} tick={{ fontSize: 11 }} />
          <ChartTooltip formatter={(raw: unknown) => { const v = Number(raw); return unit === '₦' ? naira(v) : `${v.toLocaleString()} ${unit}`; }} />
          <Bar dataKey={key} fill={colour} maxBarSize={56}>
            <LabelList dataKey={key} position="top" formatter={(raw: unknown) => { const v = Number(raw); return unit === '₦' ? short(v) : v.toLocaleString(); }} style={{ fontSize: 11 }} />
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    );

  return (
    <Spin spinning={isFetching}>
      <Space style={{ marginBottom: 12 }} wrap>
        <Text type="secondary">Period:</Text>
        <RangePicker value={range} onChange={v => setRange(v as Range)} format="D MMM YYYY" allowClear
          presets={[
            { label: 'This month', value: [dayjs().startOf('month'), dayjs()] },
            { label: 'This year',  value: [dayjs().startOf('year'),  dayjs()] },
          ]} />
        <Text type="secondary" style={{ fontSize: 12 }}>Last updated {dayjs().format('D MMM YYYY, HH:mm')}</Text>
      </Space>

      <Section title="Diesel">
        <Row gutter={16}>
          <Col xs={24} lg={6}>
            <Statistic title="Diesel issued — cost" value={naira(d.issuedCost)} valueStyle={{ color: '#1f4e79', fontSize: 22 }} />
            <Statistic title="Diesel issued — quantity" value={d.issuedLitres} suffix="Ltrs" precision={2}
              valueStyle={{ color: '#1f4e79', fontSize: 22 }} style={{ marginTop: 12 }} />
            <Text type="secondary" style={{ fontSize: 11, display: 'block', marginTop: 10 }}>
              Bulk purchased in period: {naira(d.purchasedCost)} · {d.purchasedLitres.toLocaleString()} L
            </Text>
          </Col>
          <Col xs={24} lg={9}>
            <Text type="secondary" style={{ fontSize: 12 }}>Split of diesel <b>cost</b> per end user</Text>
            <SplitBars colour={ORANGE} rows={d.byEndUser.map(x => ({ label: x.label, percent: x.percentCost, value: naira(x.cost) }))} />
          </Col>
          <Col xs={24} lg={9}>
            <Text type="secondary" style={{ fontSize: 12 }}>Split of diesel <b>quantity</b> per end user</Text>
            <SplitBars colour={ORANGE} rows={d.byEndUser.map(x => ({ label: x.label, percent: x.percentLitres, value: `${x.litres.toLocaleString()} L` }))} />
          </Col>
        </Row>
      </Section>

      <Section title="PHED (Electricity)">
        <Row gutter={16}>
          <Col xs={24} lg={6}>
            <Statistic title="Total PHED bill amount" value={naira(p.totalCost)} valueStyle={{ color: '#1f4e79', fontSize: 22 }} />
            <Statistic title="Total PHED kW consumed" value={p.totalKwh} suffix="kWh" precision={2}
              valueStyle={{ color: '#1f4e79', fontSize: 22 }} style={{ marginTop: 12 }} />
          </Col>
          <Col xs={24} lg={9}>
            <Text type="secondary" style={{ fontSize: 12 }}>Split of PHED <b>cost</b> per end user</Text>
            <SplitBars colour={GREEN} rows={p.byEndUser.map(x => ({ label: x.label, percent: x.percentCost, value: naira(x.cost) }))} />
          </Col>
          <Col xs={24} lg={9}>
            <Text type="secondary" style={{ fontSize: 12 }}>Split of PHED <b>kWh</b> per end user</Text>
            <SplitBars colour={GREEN} rows={p.byEndUser.map(x => ({ label: x.label, percent: x.percentKwh, value: `${x.kwh.toLocaleString()} kWh` }))} />
          </Col>
        </Row>
      </Section>

      <Row gutter={16}>
        <Col xs={24} lg={12}>
          <Section title="Monthly Diesel Supply Qty / End User"
            extra={<Select size="small" style={{ width: 150 }} value={dSel} onChange={setDUser} options={dUsers.map(u => ({ value: u, label: u }))} />}>
            {chart(d.monthly.filter(x => x.endUser === dSel), 'litres', ORANGE, 'L')}
          </Section>
        </Col>
        <Col xs={24} lg={12}>
          <Section title="Monthly Electricity kW / End User"
            extra={<Select size="small" style={{ width: 150 }} value={pSel} onChange={setPUser} options={pUsers.map(u => ({ value: u, label: u }))} />}>
            {chart(p.monthly.filter(x => x.endUser === pSel), 'kwh', GREEN, 'kWh')}
          </Section>
        </Col>
        <Col xs={24} lg={12}>
          <Section title="Monthly Diesel Cost — All Locations">{chart(sumByMonth(d.monthly), 'cost', '#ed7d31', '₦')}</Section>
        </Col>
        <Col xs={24} lg={12}>
          <Section title="Monthly Electricity Purchase Cost — All Locations">{chart(sumByMonth(p.monthly), 'cost', GREEN, '₦')}</Section>
        </Col>
      </Row>
    </Spin>
  );
}

// ═══════════════════════ 3. LOCATION CONSUMPTION ══════════════════════════════

function LocationConsumptionDashboard() {
  const [site, setSite]   = useState<string | undefined>();
  const [month, setMonth] = useState(dayjs());
  const { data, isFetching } = useQuery({
    queryKey: ['reports', 'analytics', 'location', site, month.format('YYYY-MM')],
    queryFn:  () => analyticsApi.locationConsumption(site, month.format('YYYY-MM')),
  });
  if (!data) return <Spin style={{ display: 'block', margin: '48px auto' }} />;
  const t = data.totals;

  return (
    <Spin spinning={isFetching}>
      <Space style={{ marginBottom: 12 }} wrap>
        <Text type="secondary">Location:</Text>
        <Select style={{ width: 200 }} value={data.site} onChange={setSite}
          options={data.sites.map(s => ({ value: s, label: s }))} />
        <DatePicker picker="month" value={month} onChange={v => v && setMonth(v)} format="MMM YYYY" allowClear={false} />
      </Space>

      <Card style={{ background: '#1f4e79', marginBottom: 16 }} styles={{ body: { padding: 16 } }}>
        <Title level={5} style={{ color: '#fff', marginTop: 0, textAlign: 'center' }}>
          {data.site} CONSUMPTION ANALYSIS — {month.format('MMM YYYY').toUpperCase()}
        </Title>
        <Row gutter={[12, 12]}>
          {[
            { label: 'TOTAL GEN RUNNING HRS',  v: t.runHours.toLocaleString() },
            { label: 'TOTAL DIESEL CONSUMED',  v: `${t.dieselConsumed.toLocaleString()} L` },
            { label: 'CURRENT DIESEL LEVEL',   v: `${t.currentLevel.toLocaleString()} L` },
            { label: 'TOTAL PHED KW CONSUMED', v: `${t.phedKwh.toLocaleString()} kWh`,
              note: t.phedKwhSource === 'purchases' ? 'from units purchased' : 'from daily meter readings' },
            { label: 'LAST DIESEL RECEIVED',   v: t.lastDieselLitres != null ? `${t.lastDieselLitres.toLocaleString()} L` : '—',
              note: t.lastDieselDate ? dayjs(t.lastDieselDate).format('D MMM YYYY') : undefined },
          ].map(c => (
            <Col xs={12} lg={4} key={c.label} style={{ flex: '1 1 0' }}>
              <Card size="small" styles={{ body: { padding: '10px 12px' } }}>
                <div style={{ fontSize: 10, color: '#8c8c8c' }}>{c.label}</div>
                <div style={{ fontSize: 18, fontWeight: 700, color: '#1f4e79' }}>{c.v}</div>
                {c.note && <div style={{ fontSize: 10, color: '#bfbfbf' }}>{c.note}</div>}
              </Card>
            </Col>
          ))}
        </Row>
      </Card>

      {t.daysLogged === 0 && (
        <Alert type="info" showIcon style={{ marginBottom: 12 }}
          message={`No Daily Readings were logged at ${data.site} in ${month.format('MMM YYYY')}. This analysis is built from the Daily Readings under Fuel & Power.`} />
      )}

      <Row gutter={16}>
        <Col xs={24} lg={7}>
          <Section title="Diesel Consumption in the Last 7 Days">
            {data.last7Days.length === 0 && <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />}
            {data.last7Days.map(x => (
              <div key={x.date} style={{ display: 'flex', padding: '4px 0', borderBottom: '1px solid #f5f5f5' }}>
                <Text strong style={{ width: 90 }}>{x.litres.toLocaleString()} L</Text>
                <Text style={{ flex: 1 }}>{dayjs(x.date).format('D-MMM-YY')}</Text>
                <Text type="secondary">{x.day}</Text>
              </div>
            ))}
          </Section>
        </Col>
        <Col xs={24} lg={7}>
          <Section title="Weekdays / Weekends Diesel Consumption (Ltrs)">
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={[{ name: 'Weekdays', litres: data.weekdayWeekend.weekday }, { name: 'Weekends', litres: data.weekdayWeekend.weekend }]}
                margin={{ top: 22, right: 8, left: 8, bottom: 0 }}>
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis tickFormatter={short} tick={{ fontSize: 11 }} />
                <ChartTooltip formatter={(raw: unknown) => { const v = Number(raw); return `${v.toLocaleString()} L`; }} />
                <Bar dataKey="litres" maxBarSize={70}>
                  <Cell fill={ORANGE} /><Cell fill="#ffc000" />
                  <LabelList dataKey="litres" position="top" formatter={(raw: unknown) => { const v = Number(raw); return v.toLocaleString(); }} style={{ fontSize: 11 }} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </Section>
        </Col>
        <Col xs={24} lg={10}>
          <Section title={`Monthly Diesel Consumed — ${month.format('YYYY')}`}>
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={data.monthly} margin={{ top: 22, right: 8, left: 8, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="month" tick={{ fontSize: 11 }} />
                <YAxis tickFormatter={short} tick={{ fontSize: 11 }} />
                <ChartTooltip formatter={(raw: unknown) => { const v = Number(raw); return `${v.toLocaleString()} L`; }} />
                <Bar dataKey="litres" fill={ORANGE} maxBarSize={40}>
                  <LabelList dataKey="litres" position="top" formatter={(raw: unknown) => { const v = Number(raw); return v.toLocaleString(); }} style={{ fontSize: 10 }} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </Section>
        </Col>
      </Row>

      <Section title={`Daily Diesel Consumed and Generator Hours — ${month.format('MMM YYYY')}`}>
        {data.daily.length === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No daily readings this month" /> : (
          <ResponsiveContainer width="100%" height={240}>
            <BarChart data={data.daily.map(x => ({ ...x, d: dayjs(x.date).format('D') }))} margin={{ top: 10, right: 8, left: 8, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} />
              <XAxis dataKey="d" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} />
              <ChartTooltip />
              <Bar dataKey="litres" name="Diesel (L)" fill={ORANGE} maxBarSize={18} />
              <Bar dataKey="hours"  name="Gen hours"  fill={BLUE}   maxBarSize={18} />
            </BarChart>
          </ResponsiveContainer>
        )}
      </Section>
    </Spin>
  );
}

// ═════════════════════════════════ shell ══════════════════════════════════════

export default function ManagementDashboards() {
  const [view, setView] = useState<'maintenance' | 'energy' | 'location'>('maintenance');
  return (
    <div>
      <Segmented value={view} onChange={v => setView(v as typeof view)} style={{ marginBottom: 16 }}
        options={[
          { value: 'maintenance', label: 'Repairs & Maintenance Summary' },
          { value: 'energy',      label: 'Diesel / PHED Purchase Analysis' },
          { value: 'location',    label: 'Location Consumption Analysis' },
        ]} />
      {view === 'maintenance' && <MaintenanceDashboard />}
      {view === 'energy'      && <EnergyPurchaseDashboard />}
      {view === 'location'    && <LocationConsumptionDashboard />}
    </div>
  );
}
