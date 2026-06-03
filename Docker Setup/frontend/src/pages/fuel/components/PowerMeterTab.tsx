import { useState, useCallback } from 'react';
import {
  Alert, Button, Col, Form, Input, InputNumber, Modal, Row,
  Select, Statistic, Table, Tag, Tooltip, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, BulbOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { generatorMonitoringApi } from '../../../api/generatorMonitoring.api';
import { OFFICE_LOCATIONS } from '../../../types';
import type { PowerMeterReading } from '../../../types';

dayjs.extend(relativeTime);
const { Text } = Typography;
const { TextArea } = Input;

const columns: ColumnsType<PowerMeterReading> = [
  { title: 'Date', dataIndex: 'readingDate', key: 'date', width: 110,
    render: (v: string) => <Text strong style={{ fontSize: 13 }}>{dayjs(v).format('D MMM YY')}</Text> },
  { title: 'Location', dataIndex: 'location', key: 'loc', width: 160,
    render: (v: string) => <Text style={{ fontSize: 13 }}>{v}</Text> },
  { title: 'Meter #', dataIndex: 'meterNumber', key: 'meter', width: 120,
    render: (v: string) => <Text code style={{ fontSize: 12 }}>{v}</Text> },
  { title: 'Reading (kWh)', dataIndex: 'meterReadingKwh', key: 'reading', width: 130,
    render: (v: number) => <Text style={{ fontSize: 13 }}>{v.toLocaleString()}</Text> },
  { title: 'Consumed Today', dataIndex: 'unitsConsumedToday', key: 'consumed', width: 130,
    render: (v?: number) => v != null
      ? <Tag color={v > 400 ? 'red' : v > 250 ? 'orange' : 'blue'}>{v.toLocaleString()} kWh</Tag>
      : <Text type="secondary">—</Text> },
  { title: 'Elec. Cost (₦)', dataIndex: 'totalElectricityCostNaira', key: 'cost', width: 130,
    render: (v?: number) => v != null
      ? <Text strong style={{ fontSize: 13, color: '#722ed1' }}>₦{Number(v).toLocaleString()}</Text>
      : <Text type="secondary">—</Text> },
  { title: 'Utility Hrs', dataIndex: 'utilityAvailableHours', key: 'utility', width: 95,
    render: (v?: number) => v != null
      ? <Tag color={v >= 16 ? 'green' : v >= 8 ? 'orange' : 'red'}>{v} h</Tag>
      : <Text type="secondary">—</Text> },
  { title: 'Rate (₦/kWh)', dataIndex: 'costPerKwhNaira', key: 'rate', width: 110,
    render: (v?: number) => v != null ? <Text style={{ fontSize: 12 }}>₦{Number(v).toLocaleString()}</Text> : <Text type="secondary">—</Text> },
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
  const [locationSel,    setLocationSel]    = useState<string | null>(null);

  const refresh = useCallback(() => qc.invalidateQueries({ queryKey: ['power-meter'] }), [qc]);

  const { data, isFetching } = useQuery({
    queryKey: ['power-meter', 'list', locationFilter, page],
    queryFn: () => generatorMonitoringApi.listPowerReadings({ location: locationFilter, days: 60, page, pageSize: 20 }),
  });

  // Summary stats
  const latest = data?.items ?? [];
  const latestByLocation = Object.values(
    latest.reduce((acc, r) => {
      if (!acc[r.location] || dayjs(r.readingDate) > dayjs(acc[r.location].readingDate))
        acc[r.location] = r;
      return acc;
    }, {} as Record<string, PowerMeterReading>)
  );

  const totalConsumedToday = latestByLocation.reduce((s, r) => s + (r.unitsConsumedToday ?? 0), 0);
  const avgUtilityHours    = latestByLocation.length > 0
    ? latestByLocation.reduce((s, r) => s + (r.utilityAvailableHours ?? 0), 0) / latestByLocation.length
    : 0;

  const handleCreate = async (values: Record<string, unknown>) => {
    setCreateLoading(true); setCreateError(null);
    try {
      const location = values.locationSelect === 'Other'
        ? (values.locationOther as string)?.trim() ?? 'Other'
        : values.locationSelect as string;
      await generatorMonitoringApi.createPowerReading({
        location,
        meterNumber:          (values.meterNumber as string).trim(),
        meterReadingKwh:      values.meterReadingKwh as number,
        utilityAvailableHours:values.utilityAvailableHours as number | undefined,
        costPerKwhNaira:      values.costPerKwhNaira as number | undefined,
        notes:                values.notes as string | undefined,
      });
      form.resetFields(); setLocationSel(null); setCreateOpen(false); refresh();
    } catch(e: unknown) {
      setCreateError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save.');
    } finally { setCreateLoading(false); }
  };

  return (
    <div>
      {/* Summary row */}
      <Row gutter={12} style={{ marginBottom: 20 }}>
        {[
          { label: 'Locations Tracked', value: latestByLocation.length,          color: '#1677ff' },
          { label: 'Total kWh Today',   value: Math.round(totalConsumedToday),    color: '#722ed1', suffix: ' kWh' },
          { label: 'Avg Utility Hours', value: Math.round(avgUtilityHours * 10) / 10, color: '#52c41a', suffix: ' h' },
        ].map(s => (
          <Col key={s.label} style={{ flex: '1 1 140px', minWidth: 130 }}>
            <div style={{ background: '#fff', borderRadius: 8, padding: '12px 16px', border: '1px solid #f0f0f0' }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>{s.label}</Text>}
                value={s.value}
                suffix={s.suffix ?? ''}
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
          options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
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
        size="middle" scroll={{ x: 900 }} />

      {/* Create modal */}
      <Modal title={<><BulbOutlined /> Log Power Meter Reading</>}
        open={createOpen} onOk={() => form.submit()}
        onCancel={() => { setCreateOpen(false); form.resetFields(); setLocationSel(null); setCreateError(null); }}
        okText="Save Reading" confirmLoading={createLoading} width={480} destroyOnClose>
        {createError && <Alert message={createError} type="error" showIcon style={{ marginBottom: 12 }} />}
        <Form form={form} layout="vertical" onFinish={handleCreate}>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="locationSelect" label="Location" rules={[{ required: true }]}>
                <Select placeholder="Select…" onChange={(v: string) => setLocationSel(v)}
                  options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="meterNumber" label="NPA Meter Number" rules={[{ required: true }]}>
                <Input placeholder="e.g. NPA-PHC-001" />
              </Form.Item>
            </Col>
          </Row>
          {locationSel === 'Other' && (
            <Form.Item name="locationOther" label="Specify Location" rules={[{ required: true }]}>
              <Input placeholder="Enter location…" />
            </Form.Item>
          )}
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="meterReadingKwh" label="Meter Reading (kWh)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 124580" min={0} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="utilityAvailableHours" label="Utility Available (hours today)">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 8" min={0} max={24} step={0.5} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="costPerKwhNaira" label="Electricity Rate (₦ per kWh)"
                tooltip="Enter tariff rate to auto-calculate daily electricity cost">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 200" min={0}
                  formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
                  parser={(v) => v?.replace(/₦\s?|(,*)/g, '') as unknown as number} />
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
