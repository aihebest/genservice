import { useState, useCallback } from 'react';
import {
  Alert, Button, Col, Descriptions, Divider, Drawer,
  Form, Input, InputNumber, Modal, Row, Select, Space,
  Statistic, Table, Tag, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ArrowDownOutlined, CheckCircleOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { dieselTankApi } from '../../../api/dieselTank.api';
import { OFFICE_LOCATIONS } from '../../../types';
import type { DieselTankReading } from '../../../types';

dayjs.extend(relativeTime);

const { Text } = Typography;
const { TextArea } = Input;

// Common tank identifiers used at Desicon
const TANK_OPTIONS = [
  'Main Generator Tank',
  'Overhead Tank',
  'DGS Residence Tank',
  'DR Overhead Tank',
  'WOJI Store Tank',
  'TANU Tank (WOJI)',
  'Office Base Tank (Cummins)',
  'Other',
];

function buildColumns(onView: (r: DieselTankReading) => void): ColumnsType<DieselTankReading> {
  return [
    {
      title: 'Date', dataIndex: 'readingDate', key: 'date', width: 110,
      render: (v: string) => <Text strong style={{ fontSize: 13 }}>{dayjs(v).format('D MMM YY')}</Text>,
    },
    {
      title: 'Location', dataIndex: 'location', key: 'loc', width: 160,
      render: (v: string) => <Text style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'Tank', dataIndex: 'tankIdentifier', key: 'tank', width: 180, ellipsis: true,
      render: (v: string) => <Text style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'Current Level (L)', dataIndex: 'tankLevelLitres', key: 'level', width: 140,
      render: (v: number) => (
        <Tag color={v < 500 ? 'red' : v < 1500 ? 'orange' : 'blue'}>
          {v.toLocaleString()} L
        </Tag>
      ),
    },
    {
      title: 'Previous Level (L)', dataIndex: 'previousLevelLitres', key: 'prev', width: 145,
      render: (v?: number) => v != null
        ? <Text type="secondary" style={{ fontSize: 13 }}>{v.toLocaleString()} L</Text>
        : <Text type="secondary">—</Text>,
    },
    {
      title: 'Consumed (L)', dataIndex: 'consumptionLitres', key: 'consumed', width: 120,
      render: (v?: number) => v != null && v > 0
        ? (
          <Space size={4}>
            <ArrowDownOutlined style={{ color: '#fa8c16', fontSize: 11 }} />
            <Text style={{ fontSize: 13, color: '#fa8c16', fontWeight: 600 }}>
              {v.toLocaleString()} L
            </Text>
          </Space>
        )
        : <Text type="secondary">—</Text>,
    },
    {
      title: 'Cost (₦)', dataIndex: 'totalConsumptionCostNaira', key: 'cost', width: 120,
      render: (v?: number) => v != null
        ? <Text style={{ fontSize: 13, color: '#722ed1' }}>₦{Number(v).toLocaleString()}</Text>
        : <Text type="secondary">—</Text>,
    },
    {
      title: 'Logged By', dataIndex: 'loggedByName', key: 'by', width: 140, ellipsis: true,
      render: (v: string) => <Text style={{ fontSize: 12 }}>{v}</Text>,
    },
    {
      title: '', key: 'act', width: 65,
      render: (_: unknown, r: DieselTankReading) => (
        <Button size="small" onClick={() => onView(r)}>View</Button>
      ),
    },
  ];
}


type LatestTankEntry = {
  location: string; tankIdentifier: string;
  tankLevelLitres: number; consumptionLitres?: number; readingDate: string;
};
function toLatestTankEntry(e: Record<string, unknown>): LatestTankEntry {
  return {
    location:          String(e['location'] ?? ''),
    tankIdentifier:    String(e['tankIdentifier'] ?? ''),
    tankLevelLitres:   Number(e['tankLevelLitres'] ?? 0),
    consumptionLitres: e['consumptionLitres'] != null ? Number(e['consumptionLitres']) : undefined,
    readingDate:       String(e['readingDate'] ?? ''),
  };
}
function toLatestTankEntries(v: unknown): LatestTankEntry[] {
  if (!Array.isArray(v)) return [];
  return v.map((e: unknown) => toLatestTankEntry(e as Record<string, unknown>));
}

export default function DieselTankTab() {
  const qc = useQueryClient();

  const [locationFilter, setLocationFilter] = useState<string | undefined>();
  const [page,           setPage]           = useState(1);
  const [selected,       setSelected]       = useState<DieselTankReading | null>(null);
  const [drawerOpen,     setDrawerOpen]     = useState(false);
  const [createOpen,     setCreateOpen]     = useState(false);
  const [createLoading,  setCreateLoading]  = useState(false);
  const [createError,    setCreateError]    = useState<string | null>(null);
  const [form]           = Form.useForm();
  const [locationSel,    setLocationSel]    = useState<string | null>(null);
  const [tankSel,        setTankSel]        = useState<string | null>(null);

  const refresh = useCallback(() => qc.invalidateQueries({ queryKey: ['diesel-tank'] }), [qc]);

  const { data, isFetching } = useQuery({
    queryKey: ['diesel-tank', 'list', locationFilter, page],
    queryFn: () => dieselTankApi.list({ location: locationFilter, days: 60, page }),
  });

  const { data: summary } = useQuery({
    queryKey: ['diesel-tank', 'summary'],
    queryFn:  () => dieselTankApi.summary(30),
    refetchInterval: 60_000,
  });

  const sumData = summary as Record<string, unknown> | undefined;

  const handleCreate = async (values: Record<string, unknown>) => {
    setCreateLoading(true); setCreateError(null);
    try {
      const location = values.locationSelect === 'Other'
        ? (values.locationOther as string)?.trim() ?? 'Other'
        : values.locationSelect as string;
      const tank = values.tankSelect === 'Other'
        ? (values.tankOther as string)?.trim() ?? 'Other Tank'
        : values.tankSelect as string;

      await dieselTankApi.create({
        location,
        tankIdentifier:   tank,
        tankLevelLitres:  values.tankLevelLitres as number,
        costPerLitreNaira:values.costPerLitreNaira as number | undefined,
        notes:            values.notes as string | undefined,
      });
      form.resetFields(); setLocationSel(null); setTankSel(null);
      setCreateOpen(false); refresh();
    } catch (e: unknown) {
      setCreateError(
        (e as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? 'Failed to save reading.'
      );
    } finally { setCreateLoading(false); }
  };

  const latestPerTank = toLatestTankEntries(sumData?.latestPerTank);

  type TankStat = { label: string; value: number; color: string; suffix: string; fmt: boolean };
  const tankStats: TankStat[] = [
    { label: 'Tanks Tracked',        value: Number(sumData?.tankCount ?? 0),                  color: '#1677ff', suffix: '',   fmt: false },
    { label: 'Total Consumed (30d)', value: Number(sumData?.totalConsumptionLitres ?? 0),     color: '#fa8c16', suffix: ' L', fmt: false },
    { label: 'Avg Daily (L)',        value: Number(sumData?.avgDailyConsumptionLitres ?? 0),  color: '#722ed1', suffix: ' L', fmt: false },
    { label: 'Total Cost (30d)',     value: Number(sumData?.totalCostNaira ?? 0),             color: '#52c41a', suffix: '',   fmt: true  },
  ];

  return (
    <div>
      {/* Summary stats */}
      <Row gutter={12} style={{ marginBottom: 20 }}>
        {tankStats.map(s => (
          <Col key={s.label} style={{ flex: '1 1 140px', minWidth: 130, marginBottom: 8 }}>
            <div style={{ background: '#fff', borderRadius: 8, padding: '12px 16px',
              border: '1px solid #f0f0f0' }}>
              <Statistic
                title={<Text style={{ fontSize: 11 }}>{s.label}</Text>}
                value={s.fmt ? `₦${Number(s.value ?? 0).toLocaleString()}` : Number(s.value ?? 0)}
                suffix={!s.fmt ? (s.suffix ?? '') : ''}
                valueStyle={{ color: s.color, fontSize: 20, fontWeight: 700 }}
              />
            </div>
          </Col>
        ))}
      </Row>

      {/* Latest reading per tank cards */}
      {latestPerTank.length > 0 && (
        <div style={{ background: '#fff', borderRadius: 8, border: '1px solid #f0f0f0',
          marginBottom: 20, padding: 16 }}>
          <Text strong style={{ marginBottom: 12, display: 'block' }}>
            Current Tank Levels
          </Text>
          <Row gutter={12}>
            {latestPerTank.map((t, i) => (
              <Col key={i} style={{ flex: '1 1 200px', minWidth: 180, marginBottom: 8 }}>
                <div style={{
                  border: `1px solid ${t.tankLevelLitres < 500 ? '#ff4d4f' : t.tankLevelLitres < 1500 ? '#fa8c16' : '#e8e8e8'}`,
                  borderRadius: 8, padding: '12px 14px',
                  background: t.tankLevelLitres < 500 ? '#fff2f0' : '#fafafa',
                }}>
                  <Text strong style={{ fontSize: 12, display: 'block' }}>{t.tankIdentifier}</Text>
                  <Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 8 }}>
                    {t.location}
                  </Text>
                  <Text style={{ fontSize: 20, fontWeight: 700,
                    color: t.tankLevelLitres < 500 ? '#ff4d4f' : t.tankLevelLitres < 1500 ? '#fa8c16' : '#1677ff' }}>
                    {t.tankLevelLitres.toLocaleString()} L
                  </Text>
                  {t.consumptionLitres != null && t.consumptionLitres > 0 && (
                    <Text type="secondary" style={{ fontSize: 11, display: 'block', marginTop: 4 }}>
                      <ArrowDownOutlined style={{ color: '#fa8c16' }} /> {t.consumptionLitres} L consumed today
                    </Text>
                  )}
                  <Text type="secondary" style={{ fontSize: 10, display: 'block', marginTop: 2 }}>
                    Last: {dayjs(t.readingDate).format('D MMM')}
                  </Text>
                </div>
              </Col>
            ))}
          </Row>
        </div>
      )}

      {/* Filters + add button */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap' }}>
        <Select allowClear placeholder="Filter by location…" style={{ width: 200 }}
          value={locationFilter}
          onChange={v => { setLocationFilter(v); setPage(1); }}
          options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
        <div style={{ marginLeft: 'auto' }}>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            Log Tank Reading
          </Button>
        </div>
      </div>

      <Table<DieselTankReading>
        columns={buildColumns(r => { setSelected(r); setDrawerOpen(true); })}
        dataSource={data?.items ?? []} rowKey="id" loading={isFetching}
        pagination={{
          current: page, pageSize: 20, total: data?.totalCount ?? 0,
          onChange: p => setPage(p),
          showTotal: (t, [f, to]) => `${f}–${to} of ${t}`,
          showSizeChanger: false,
        }}
        onRow={r => ({ onClick: () => { setSelected(r); setDrawerOpen(true); }, style: { cursor: 'pointer' } })}
        size="middle" scroll={{ x: 1000 }}
      />

      {/* Create modal */}
      <Modal
        title={<><ArrowDownOutlined /> Log Diesel Tank Level Reading</>}
        open={createOpen}
        onOk={() => form.submit()}
        onCancel={() => { setCreateOpen(false); form.resetFields(); setLocationSel(null); setTankSel(null); setCreateError(null); }}
        okText="Save Reading"
        confirmLoading={createLoading}
        width={520}
        destroyOnClose
      >
        {createError && <Alert message={createError} type="error" showIcon style={{ marginBottom: 12 }} />}
        <Alert type="info" showIcon style={{ marginBottom: 14 }}
          message="The system will automatically calculate today's consumption by comparing with the previous reading for this tank." />
        <Form form={form} layout="vertical" onFinish={handleCreate}>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="locationSelect" label="Location" rules={[{ required: true }]}>
                <Select placeholder="Select location…"
                  onChange={(v: string) => setLocationSel(v)}
                  options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="tankSelect" label="Tank Identifier" rules={[{ required: true }]}>
                <Select placeholder="Select tank…"
                  onChange={(v: string) => setTankSel(v)}
                  options={TANK_OPTIONS.map(t => ({ value: t, label: t }))} />
              </Form.Item>
            </Col>
          </Row>
          {locationSel === 'Other' && (
            <Form.Item name="locationOther" label="Specify Location" rules={[{ required: true }]}>
              <Input placeholder="Enter location…" />
            </Form.Item>
          )}
          {tankSel === 'Other' && (
            <Form.Item name="tankOther" label="Specify Tank Name" rules={[{ required: true }]}>
              <Input placeholder="e.g. Block C Overhead Tank" />
            </Form.Item>
          )}
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="tankLevelLitres" label="Current Diesel Level (Litres)"
                rules={[{ required: true, message: 'Enter current tank level' }]}>
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 4,700" min={0} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="costPerLitreNaira" label="Cost per Litre (₦) — optional"
                tooltip="Used to calculate consumption cost. Leave blank to skip cost calculation.">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 1,250"
                  formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
                  parser={(v: string | undefined) => parseFloat(v?.replace(/₦\s?|(,*)/g, '') ?? '0') as 0}
                  min={0} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="notes" label="Notes (optional)">
            <TextArea rows={2} placeholder="Any remarks about the tank level, top-up events, etc."
              maxLength={2000} />
          </Form.Item>
        </Form>
      </Modal>

      {/* Detail drawer */}
      {selected && (
        <Drawer
          title={
            <Space>
              <Text strong>{selected.tankIdentifier}</Text>
              <Text type="secondary" style={{ fontSize: 13 }}>· {selected.location}</Text>
            </Space>
          }
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          width={460}
        >
          <Space wrap style={{ marginBottom: 14 }}>
            <Tag color={selected.tankLevelLitres < 500 ? 'red' : selected.tankLevelLitres < 1500 ? 'orange' : 'blue'}>
              {selected.tankLevelLitres.toLocaleString()} L current level
            </Tag>
            {selected.consumptionLitres != null && selected.consumptionLitres > 0 && (
              <Tag color="orange" icon={<ArrowDownOutlined />}>
                {selected.consumptionLitres} L consumed
              </Tag>
            )}
            {selected.consumptionLitres === 0 && (
              <Tag color="green" icon={<CheckCircleOutlined />}>No consumption</Tag>
            )}
          </Space>

          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Location">{selected.location}</Descriptions.Item>
            <Descriptions.Item label="Tank">{selected.tankIdentifier}</Descriptions.Item>
            <Descriptions.Item label="Reading Date">
              {dayjs(selected.readingDate).format('D MMMM YYYY')}
            </Descriptions.Item>
            <Descriptions.Item label="Current Level">
              <Text strong style={{ fontSize: 16 }}>
                {selected.tankLevelLitres.toLocaleString()} Litres
              </Text>
            </Descriptions.Item>
            {selected.previousLevelLitres != null && (
              <Descriptions.Item label="Previous Level">
                {selected.previousLevelLitres.toLocaleString()} L
              </Descriptions.Item>
            )}
          </Descriptions>

          {selected.consumptionLitres != null && (
            <>
              <Divider titlePlacement="left" orientationMargin={0} style={{ fontSize: 12 }}>
                Auto-Calculated Consumption
              </Divider>
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label="Formula">
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {selected.previousLevelLitres?.toLocaleString()} L − {selected.tankLevelLitres.toLocaleString()} L
                  </Text>
                </Descriptions.Item>
                <Descriptions.Item label="Consumption">
                  <Text strong style={{ color: '#fa8c16', fontSize: 16 }}>
                    {selected.consumptionLitres.toLocaleString()} Litres
                  </Text>
                </Descriptions.Item>
                {selected.costPerLitreNaira != null && (
                  <Descriptions.Item label="Rate">
                    ₦{Number(selected.costPerLitreNaira).toLocaleString()} per litre
                  </Descriptions.Item>
                )}
                {selected.totalConsumptionCostNaira != null && (
                  <Descriptions.Item label="Total Cost">
                    <Text strong style={{ color: '#722ed1', fontSize: 16 }}>
                      ₦{Number(selected.totalConsumptionCostNaira).toLocaleString()}
                    </Text>
                  </Descriptions.Item>
                )}
              </Descriptions>
            </>
          )}

          {selected.notes && (
            <>
              <Divider titlePlacement="left" orientationMargin={0} style={{ fontSize: 12 }}>Notes</Divider>
              <Text type="secondary">{selected.notes}</Text>
            </>
          )}

          <Divider titlePlacement="left" orientationMargin={0} style={{ fontSize: 12 }}>Logged By</Divider>
          <Text>{selected.loggedByName}</Text>
          <Text type="secondary" style={{ fontSize: 12, marginLeft: 8 }}>
            {dayjs(selected.createdAt).fromNow()}
          </Text>
        </Drawer>
      )}
    </div>
  );
}
