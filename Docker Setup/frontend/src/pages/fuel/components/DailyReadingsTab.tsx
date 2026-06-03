import { useState, useCallback } from 'react';
import {
  Alert, Badge, Button, Col, Descriptions, Divider, Drawer, Form,
  Input, InputNumber, Modal, Progress, Row, Select, Space,
  Statistic, Table, Tag, Tooltip, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, WarningOutlined, CheckCircleOutlined, ThunderboltOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { generatorMonitoringApi } from '../../../api/generatorMonitoring.api';
import { GENERATOR_DAILY_STATUS_META, OFFICE_LOCATIONS, PRIORITY_META } from '../../../types';
import type { GeneratorDailyReading, GeneratorDailyStatus } from '../../../types';

dayjs.extend(relativeTime);
const { Text } = Typography;
const { TextArea } = Input;

const GEN_STATUSES = ['Running', 'Standby', 'UnderMaintenance', 'Fault'];

function buildColumns(onView: (r: GeneratorDailyReading) => void): ColumnsType<GeneratorDailyReading> {
  return [
    { title: 'Date', dataIndex: 'readingDate', key: 'date', width: 110,
      render: (v: string) => <Text strong style={{ fontSize: 13 }}>{dayjs(v).format('D MMM YY')}</Text> },
    { title: 'Asset', dataIndex: 'assetDescription', key: 'asset', ellipsis: true,
      render: (v: string, r) => (
        <div>
          <Text style={{ fontSize: 13 }}>{v}</Text>
          <br /><Text type="secondary" style={{ fontSize: 11 }}>{r.assetNo} · {r.location}</Text>
        </div>
      )},
    { title: 'Status', dataIndex: 'generatorStatus', key: 'status', width: 140,
      render: (v: GeneratorDailyStatus) => {
        const m = GENERATOR_DAILY_STATUS_META[v];
        return <Badge status={m?.badge as any} text={m?.label ?? v} />;
      }},
    { title: 'Cumulative Hrs', dataIndex: 'cumulativeRunHours', key: 'cumHours', width: 120,
      render: (v: number) => <Text style={{ fontSize: 13 }}>{v.toLocaleString()} h</Text> },
    { title: 'Today (hrs)', dataIndex: 'runHoursToday', key: 'todayHrs', width: 100,
      render: (v: number) => <Tag color={v > 0 ? 'processing' : 'default'}>{v} h</Tag> },
    { title: 'Fuel Level', dataIndex: 'fuelLevelLitres', key: 'fuel', width: 100,
      render: (v: number) => (
        <Text type={v < 100 ? 'danger' : v < 200 ? 'warning' : undefined} style={{ fontSize: 13 }}>
          {v < 100 && '⚠ '}{v.toLocaleString()} L
        </Text>
      )},
    { title: 'Next Service', key: 'service', width: 135,
      render: (_: unknown, r) => (
        <div>
          {r.serviceAlertActive
            ? <Tag color="red" icon={<WarningOutlined />}>Alert! {r.hoursUntilNextService.toFixed(0)}h left</Tag>
            : <Text type="secondary" style={{ fontSize: 12 }}>{r.hoursUntilNextService.toFixed(0)} h away</Text>
          }
        </div>
      )},
    { title: 'Utility Hrs', dataIndex: 'utilityAvailableHours', key: 'utility', width: 90,
      render: (v?: number) => <Text type="secondary" style={{ fontSize: 12 }}>{v != null ? `${v} h` : '—'}</Text> },
    { title: 'Logged By', dataIndex: 'loggedByName', key: 'by', width: 140, ellipsis: true,
      render: (v: string) => <Text style={{ fontSize: 12 }}>{v}</Text> },
    { title: '', key: 'act', width: 65,
      render: (_: unknown, r: GeneratorDailyReading) => <Button size="small" onClick={() => onView(r)}>View</Button> },
  ];
}

export default function DailyReadingsTab() {
  const qc = useQueryClient();
  const [locationFilter, setLocationFilter] = useState<string | undefined>();
  const [page,           setPage]           = useState(1);
  const [createOpen,     setCreateOpen]     = useState(false);
  const [selected,       setSelected]       = useState<GeneratorDailyReading | null>(null);
  const [drawerOpen,     setDrawerOpen]     = useState(false);
  const [createLoading,  setCreateLoading]  = useState(false);
  const [createError,    setCreateError]    = useState<string | null>(null);
  const [form]           = Form.useForm();
  const [locationSel,    setLocationSel]    = useState<string | null>(null);

  const refresh = useCallback(() => qc.invalidateQueries({ queryKey: ['gen-readings'] }), [qc]);

  const { data, isFetching } = useQuery({
    queryKey: ['gen-readings', 'list', locationFilter, page],
    queryFn: () => generatorMonitoringApi.listReadings({ location: locationFilter, days: 60, page, pageSize: 20 }),
  });

  const { data: summary } = useQuery({
    queryKey: ['gen-readings', 'summary'],
    queryFn: generatorMonitoringApi.summary,
    refetchInterval: 60_000,
  });

  const alertCount = summary?.filter(s => s.serviceAlertActive).length ?? 0;
  const runningCount = summary?.filter(s => s.latestStatus === 'Running').length ?? 0;
  const lowFuelCount = summary?.filter(s => s.latestFuelLevel < 100).length ?? 0;

  const handleCreate = async (values: Record<string, unknown>) => {
    setCreateLoading(true); setCreateError(null);
    try {
      const location = values.locationSelect === 'Other'
        ? (values.locationOther as string)?.trim() ?? 'Other'
        : values.locationSelect as string;
      await generatorMonitoringApi.createReading({
        assetNo: (values.assetNo as string).trim(),
        assetDescription: (values.assetDescription as string).trim(),
        location,
        cumulativeRunHours: values.cumulativeRunHours as number,
        runHoursToday: values.runHoursToday as number,
        generatorStatus: values.generatorStatus as string,
        fuelLevelLitres: values.fuelLevelLitres as number,
        fuelConsumedLitres: values.fuelConsumedLitres as number | undefined,
        utilityAvailableHours: values.utilityAvailableHours as number | undefined,
        serviceIntervalHours: (values.serviceIntervalHours as number) ?? 250,
        lastServicedAtHours: values.lastServicedAtHours as number | undefined,
        notes: values.notes as string | undefined,
      });
      form.resetFields(); setLocationSel(null); setCreateOpen(false); refresh();
    } catch(e: unknown) {
      setCreateError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save.');
    } finally { setCreateLoading(false); }
  };

  return (
    <div>
      {/* Service alerts */}
      {alertCount > 0 && (
        <Alert type="warning" showIcon icon={<WarningOutlined />} style={{ marginBottom: 12 }}
          message={
            <Text><strong>{alertCount} generator{alertCount > 1 ? 's' : ''}</strong> approaching service threshold — immediate attention required.</Text>
          } />
      )}

      {/* Fleet overview cards */}
      {summary && summary.length > 0 && (
        <Row gutter={12} style={{ marginBottom: 20 }}>
          <Col style={{ flex: '1 1 130px', minWidth: 120 }}>
            <div style={{ background: '#fff', borderRadius: 8, padding: '12px 16px', border: '1px solid #f0f0f0' }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>Generators Tracked</Text>}
                value={summary.length} valueStyle={{ color: '#1677ff', fontSize: 22, fontWeight: 700 }} />
            </div>
          </Col>
          <Col style={{ flex: '1 1 130px', minWidth: 120 }}>
            <div style={{ background: '#fff', borderRadius: 8, padding: '12px 16px', border: '1px solid #f0f0f0' }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>Currently Running</Text>}
                value={runningCount} valueStyle={{ color: '#52c41a', fontSize: 22, fontWeight: 700 }} />
            </div>
          </Col>
          <Col style={{ flex: '1 1 130px', minWidth: 120 }}>
            <div style={{ background: '#fff', borderRadius: 8, padding: '12px 16px', border: '1px solid #f0f0f0' }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>Service Alerts</Text>}
                value={alertCount} valueStyle={{ color: alertCount > 0 ? '#ff4d4f' : '#52c41a', fontSize: 22, fontWeight: 700 }} />
            </div>
          </Col>
          <Col style={{ flex: '1 1 130px', minWidth: 120 }}>
            <div style={{ background: '#fff', borderRadius: 8, padding: '12px 16px', border: '1px solid #f0f0f0' }}>
              <Statistic title={<Text style={{ fontSize: 11 }}>Low Fuel (&lt;100L)</Text>}
                value={lowFuelCount} valueStyle={{ color: lowFuelCount > 0 ? '#fa8c16' : '#52c41a', fontSize: 22, fontWeight: 700 }} />
            </div>
          </Col>
        </Row>
      )}

      {/* Fleet summary table (latest per generator) */}
      {summary && summary.length > 0 && (
        <div style={{ background: '#fff', borderRadius: 8, border: '1px solid #f0f0f0', marginBottom: 20, padding: 16 }}>
          <Text strong style={{ marginBottom: 12, display: 'block' }}>Generator Fleet Overview (Latest Readings)</Text>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
            {summary.map(s => (
              <div key={s.assetNo} style={{
                border: `1px solid ${s.serviceAlertActive ? '#ff4d4f' : s.latestFuelLevel < 100 ? '#fa8c16' : '#f0f0f0'}`,
                borderRadius: 8, padding: '12px 16px', minWidth: 220, flex: '1 1 220px',
                background: s.serviceAlertActive ? '#fff2f0' : '#fafafa',
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                  <Text strong style={{ fontSize: 12 }}>{s.assetDescription.length > 30 ? s.assetDescription.slice(0, 30) + '…' : s.assetDescription}</Text>
                  <Badge status={GENERATOR_DAILY_STATUS_META[s.latestStatus as GeneratorDailyStatus]?.badge as any} />
                </div>
                <Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 8 }}>{s.location}</Text>
                <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                  <Statistic title="Cumulative Hrs" value={s.latestCumulativeHours.toLocaleString()}
                    suffix="h" valueStyle={{ fontSize: 14 }} />
                  <Statistic title="Fuel Level" value={s.latestFuelLevel.toLocaleString()}
                    suffix="L" valueStyle={{ fontSize: 14, color: s.latestFuelLevel < 100 ? '#fa8c16' : undefined }} />
                </div>
                <div style={{ marginTop: 8 }}>
                  <Text style={{ fontSize: 11 }}>Service: </Text>
                  {s.serviceAlertActive
                    ? <Tag color="red" icon={<WarningOutlined />} style={{ fontSize: 10 }}>ALERT — {s.hoursUntilNextService.toFixed(0)}h left</Tag>
                    : <Text type="secondary" style={{ fontSize: 11 }}>{s.hoursUntilNextService.toFixed(0)}h until next</Text>}
                  <Progress percent={Math.min(100, Math.round(((250 - s.hoursUntilNextService) / 250) * 100))}
                    size="small" showInfo={false} strokeColor={s.serviceAlertActive ? '#ff4d4f' : '#52c41a'}
                    style={{ marginTop: 4 }} />
                </div>
              </div>
            ))}
          </div>
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
            Log Reading
          </Button>
        </div>
      </div>

      <Table<GeneratorDailyReading>
        columns={buildColumns(r => { setSelected(r); setDrawerOpen(true); })}
        dataSource={data?.items ?? []} rowKey="id" loading={isFetching}
        pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0,
          onChange: p => setPage(p), showTotal: (t, [f, to]) => `${f}–${to} of ${t}`, showSizeChanger: false }}
        size="middle" scroll={{ x: 1050 }} />

      {/* Create modal */}
      <Modal title={<><ThunderboltOutlined /> Log Daily Generator Reading</>}
        open={createOpen} onOk={() => form.submit()}
        onCancel={() => { setCreateOpen(false); form.resetFields(); setLocationSel(null); setCreateError(null); }}
        okText="Save Reading" confirmLoading={createLoading} width={580} destroyOnClose>
        {createError && <Alert message={createError} type="error" showIcon style={{ marginBottom: 12 }} />}
        <Form form={form} layout="vertical" onFinish={handleCreate}>
          <Row gutter={12}>
            <Col span={10}>
              <Form.Item name="assetNo" label="Asset / Tag Number" rules={[{ required: true }]}>
                <Input placeholder="e.g. 6660003188" />
              </Form.Item>
            </Col>
            <Col span={14}>
              <Form.Item name="assetDescription" label="Generator Description" rules={[{ required: true }]}>
                <Input placeholder="e.g. PHC Office CAT 350KVA Generator" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="locationSelect" label="Location" rules={[{ required: true }]}>
                <Select placeholder="Select…" onChange={(v: string) => setLocationSel(v)}
                  options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="generatorStatus" label="Generator Status" initialValue="Standby" rules={[{ required: true }]}>
                <Select options={GEN_STATUSES.map(s => ({
                  value: s, label: GENERATOR_DAILY_STATUS_META[s as GeneratorDailyStatus]?.label ?? s,
                }))} />
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
              <Form.Item name="cumulativeRunHours" label="Cumulative Run Hours (meter)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 16245" min={0} step={0.5} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="runHoursToday" label="Hours Run Today" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 6.5" min={0} max={24} step={0.5} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="fuelLevelLitres" label="Current Fuel Level (L)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 380" min={0} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="fuelConsumedLitres" label="Fuel Consumed Today (L)">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 65" min={0} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="utilityAvailableHours" label="Utility Power Hours">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 8" min={0} max={24} step={0.5} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="serviceIntervalHours" label="Service Interval (hrs)" initialValue={250}>
                <InputNumber style={{ width: '100%' }} min={50} step={50} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="lastServicedAtHours" label="Last Serviced At (hrs)">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 16010" min={0} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="notes" label="Notes (optional)">
            <TextArea rows={2} placeholder="Any observations, faults, or remarks…" maxLength={2000} />
          </Form.Item>
        </Form>
      </Modal>

      {/* Detail drawer */}
      {selected && (
        <Drawer
          title={<Space><Tag>{selected.assetNo}</Tag><Text strong>{selected.assetDescription}</Text></Space>}
          open={drawerOpen} onClose={() => setDrawerOpen(false)} width={480}>
          <Space wrap style={{ marginBottom: 12 }}>
            {(()=>{ const m=GENERATOR_DAILY_STATUS_META[selected.generatorStatus as GeneratorDailyStatus];
              return <Badge status={m?.badge as any} text={<Text strong>{m?.label}</Text>} />; })()}
            {selected.serviceAlertActive && <Tag color="red" icon={<WarningOutlined />}>Service Alert</Tag>}
            {selected.fuelLevelLitres < 100 && <Tag color="orange">Low Fuel</Tag>}
          </Space>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Asset No.">{selected.assetNo}</Descriptions.Item>
            <Descriptions.Item label="Asset">{selected.assetDescription}</Descriptions.Item>
            <Descriptions.Item label="Location">{selected.location}</Descriptions.Item>
            <Descriptions.Item label="Reading Date">{dayjs(selected.readingDate).format('D MMM YYYY')}</Descriptions.Item>
            <Descriptions.Item label="Cumulative Hrs">{selected.cumulativeRunHours.toLocaleString()} h</Descriptions.Item>
            <Descriptions.Item label="Run Hours Today">{selected.runHoursToday} h</Descriptions.Item>
            <Descriptions.Item label="Fuel Level">{selected.fuelLevelLitres.toLocaleString()} L
              {selected.fuelConsumedLitres != null && <Text type="secondary"> (consumed: {selected.fuelConsumedLitres} L)</Text>}
            </Descriptions.Item>
            {selected.utilityAvailableHours != null && (
              <Descriptions.Item label="Utility Power">{selected.utilityAvailableHours} h available</Descriptions.Item>
            )}
          </Descriptions>
          <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Service Status</Divider>
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Service Interval">{selected.serviceIntervalHours} hours</Descriptions.Item>
            {selected.lastServicedAtHours != null && (
              <Descriptions.Item label="Last Serviced At">{selected.lastServicedAtHours.toLocaleString()} h</Descriptions.Item>
            )}
            <Descriptions.Item label="Next Service In">
              {selected.serviceAlertActive
                ? <Text type="danger"><WarningOutlined /> Only {selected.hoursUntilNextService.toFixed(0)} hours remaining!</Text>
                : <Text>{selected.hoursUntilNextService.toFixed(0)} hours remaining</Text>}
            </Descriptions.Item>
          </Descriptions>
          {selected.notes && <>
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Notes</Divider>
            <Text type="secondary">{selected.notes}</Text>
          </>}
          <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Logged By</Divider>
          <Text>{selected.loggedByName} · {dayjs(selected.createdAt).fromNow()}</Text>
        </Drawer>
      )}
    </div>
  );
}
