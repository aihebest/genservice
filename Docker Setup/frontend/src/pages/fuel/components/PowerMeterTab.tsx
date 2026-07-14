import { useState, useCallback } from 'react';
import {
  Alert, Button, Col, DatePicker, Form, Input, InputNumber, Modal, Row,
  Select, Statistic, Table, Tag, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, BulbOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { generatorMonitoringApi } from '../../../api/generatorMonitoring.api';
import type { PowerMeterReading } from '../../../types';

dayjs.extend(relativeTime);
const { Text } = Typography;
const { TextArea } = Input;

// Fixed NPA tariff and the two locations tracked for utility power.
const RATE_NAIRA = 209;
const NPA_LOCATIONS = ['DR', 'Office'];

const columns: ColumnsType<PowerMeterReading> = [
  { title: 'Date', dataIndex: 'readingDate', key: 'date', width: 105,
    render: (v: string) => <Text strong style={{ fontSize: 13 }}>{dayjs(v).format('D MMM YY')}</Text> },
  { title: 'Location', dataIndex: 'location', key: 'loc', width: 100,
    render: (v: string) => <Text style={{ fontSize: 13 }}>{v}</Text> },
  { title: 'Meter #', dataIndex: 'meterNumber', key: 'meter', width: 115,
    render: (v: string) => <Text code style={{ fontSize: 12 }}>{v}</Text> },
  { title: 'Previous (kWh)', dataIndex: 'previousMeterReading', key: 'prev', width: 120,
    render: (v?: number) => <Text style={{ fontSize: 13 }}>{v != null ? v.toLocaleString() : '—'}</Text> },
  { title: 'Current (kWh)', dataIndex: 'currentMeterReading', key: 'cur', width: 120,
    render: (v?: number) => <Text style={{ fontSize: 13 }}>{v != null ? v.toLocaleString() : '—'}</Text> },
  { title: 'Consumed 24h', dataIndex: 'unitsConsumedToday', key: 'consumed', width: 125,
    render: (v?: number) => v != null
      ? <Tag color={v > 400 ? 'red' : v > 250 ? 'orange' : 'blue'}>{v.toLocaleString()} kWh</Tag>
      : <Text type="secondary">—</Text> },
  { title: 'Rate (₦/kWh)', dataIndex: 'costPerKwhNaira', key: 'rate', width: 105,
    render: (v?: number) => <Text style={{ fontSize: 12 }}>₦{Number(v ?? RATE_NAIRA).toLocaleString()}</Text> },
  { title: 'Total Cost (₦)', dataIndex: 'totalElectricityCostNaira', key: 'cost', width: 130,
    render: (v?: number) => v != null
      ? <Text strong style={{ fontSize: 13, color: '#722ed1' }}>₦{Number(v).toLocaleString()}</Text>
      : <Text type="secondary">—</Text> },
  { title: 'Utility Hrs', dataIndex: 'utilityAvailableHours', key: 'utility', width: 90,
    render: (v?: number) => v != null
      ? <Tag color={v >= 16 ? 'green' : v >= 8 ? 'orange' : 'red'}>{v} h</Tag>
      : <Text type="secondary">—</Text> },
  { title: 'Logged By', dataIndex: 'loggedByName', key: 'by', width: 130,
    render: (v: string) => <Text style={{ fontSize: 12 }}>{v}</Text> },
];

export default function PowerMeterTab() {
  const qc = useQueryClient();
  const [locationFilter, setLocationFilter] = useState<string | undefined>();
  const [page,           setPage]           = useState(1);
  const [createOpen,     setCreateOpen]     = useState(false);
  const [createLoading,  setCreateLoading]  = useState(false);
  const [createError,    setCreateError]    = useState<string | null>(null);
  const [form]           = Form.useForm();

  const wPrev = Form.useWatch('previousMeterReading', form) as number | undefined;
  const wCurr = Form.useWatch('currentMeterReading',  form) as number | undefined;
  const consumedPreview = wPrev != null && wCurr != null ? Math.max(0, wCurr - wPrev) : null;
  const costPreview = consumedPreview != null ? consumedPreview * RATE_NAIRA : null;

  const refresh = useCallback(() => qc.invalidateQueries({ queryKey: ['power-meter'] }), [qc]);

  const { data, isFetching } = useQuery({
    queryKey: ['power-meter', 'list', locationFilter, page],
    queryFn: () => generatorMonitoringApi.listPowerReadings({ location: locationFilter, days: 60, page }),
  });

  const latest = data?.items ?? [];
  const latestByLocation = Object.values(
    latest.reduce((acc, r) => {
      if (!acc[r.location] || dayjs(r.readingDate) > dayjs(acc[r.location].readingDate))
        acc[r.location] = r;
      return acc;
    }, {} as Record<string, PowerMeterReading>)
  );

  const totalConsumedToday = latestByLocation.reduce((s, r) => s + (r.unitsConsumedToday ?? 0), 0);
  const totalCostToday     = latestByLocation.reduce((s, r) => s + (r.totalElectricityCostNaira ?? 0), 0);

  const handleCreate = async (values: Record<string, unknown>) => {
    setCreateLoading(true); setCreateError(null);
    try {
      await generatorMonitoringApi.createPowerReading({
        location:             values.location as string,
        meterNumber:          (values.meterNumber as string).trim(),
        previousMeterReading: values.previousMeterReading as number,
        currentMeterReading:  values.currentMeterReading as number,
        readingDate:          values.readingDate ? (values.readingDate as dayjs.Dayjs).format('YYYY-MM-DD') : undefined,
        utilityAvailableHours:values.utilityAvailableHours as number | undefined,
        notes:                values.notes as string | undefined,
      });
      form.resetFields(); setCreateOpen(false); refresh();
    } catch(e: unknown) {
      setCreateError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save.');
    } finally { setCreateLoading(false); }
  };

  return (
    <div>
      {/* Summary row */}
      <Row gutter={12} style={{ marginBottom: 20 }}>
        {[
          { label: 'Locations Tracked', value: latestByLocation.length,       color: '#1677ff', suffix: '' },
          { label: 'Total kWh (latest)', value: Math.round(totalConsumedToday), color: '#722ed1', suffix: ' kWh' },
          { label: 'Total Cost (latest)', value: `₦${Math.round(totalCostToday).toLocaleString()}`, color: '#eb2f96', suffix: '' },
        ].map(s => (
          <Col key={s.label} style={{ flex: '1 1 150px', minWidth: 140 }}>
            <div style={{ background: '#fff', borderRadius: 8, padding: '12px 16px', border: '1px solid #f0f0f0' }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>{s.label}</Text>}
                value={s.value} suffix={s.suffix}
                valueStyle={{ color: s.color, fontSize: 22, fontWeight: 700 }} />
            </div>
          </Col>
        ))}
      </Row>

      {/* Filters + add */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap' }}>
        <Select allowClear placeholder="Filter by location…" style={{ width: 200 }}
          value={locationFilter}
          onChange={v => { setLocationFilter(v); setPage(1); }}
          options={NPA_LOCATIONS.map(l => ({ value: l, label: l }))} />
        <div style={{ marginLeft: 'auto' }}>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
            Log Meter Reading
          </Button>
        </div>
      </div>

      <Table<PowerMeterReading>
        columns={columns}
        dataSource={data?.items ?? []} rowKey="id" loading={isFetching}
        pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0,
          onChange: p => setPage(p), showTotal: (t, [f, to]) => `${f}–${to} of ${t}`, showSizeChanger: false }}
        size="middle" scroll={{ x: 1150 }} />

      {/* Create modal */}
      <Modal title={<><BulbOutlined /> Log Power Meter Reading</>}
        open={createOpen} onOk={() => form.submit()}
        onCancel={() => { setCreateOpen(false); form.resetFields(); setCreateError(null); }}
        okText="Save Reading" confirmLoading={createLoading} width={520} destroyOnClose>
        {createError && <Alert message={createError} type="error" showIcon style={{ marginBottom: 12 }} />}
        <Form form={form} layout="vertical" onFinish={handleCreate}>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="location" label="Location" rules={[{ required: true }]}>
                <Select placeholder="Select…" options={NPA_LOCATIONS.map(l => ({ value: l, label: l }))} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="meterNumber" label="NPA Meter Number" rules={[{ required: true }]}>
                <Input placeholder="e.g. NPA-DR-001" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="readingDate" label="Date"
                tooltip="Defaults to today; pick a past date to backdate.">
                <DatePicker style={{ width: '100%' }} format="DD MMM YYYY" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="previousMeterReading" label="Previous Meter Reading (kWh)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 124260" min={0} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="currentMeterReading" label="Current Meter Reading (kWh)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 124580" min={0} />
              </Form.Item>
            </Col>
          </Row>
          {consumedPreview != null && (
            <Text type="secondary" style={{ display: 'block', marginTop: -6, marginBottom: 12, fontSize: 12 }}>
              Current kWh Consumed (24 h) = <strong>{consumedPreview.toLocaleString()} kWh</strong>
              {'  ·  '}Total Cost = <strong>₦{(costPreview ?? 0).toLocaleString()}</strong> (at ₦{RATE_NAIRA}/kWh)
            </Text>
          )}
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="utilityAvailableHours" label="Utility Available (hours)">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 8" min={0} max={24} step={0.5} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="notes" label="Notes (optional)">
            <TextArea rows={2} placeholder="Any power outages, fluctuations, or remarks…" maxLength={2000} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
