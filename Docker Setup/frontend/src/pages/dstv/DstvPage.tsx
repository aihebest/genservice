import { useState } from 'react';
import {
  Alert, Button, Card, Col, Form, Input, InputNumber, Modal, Row,
  Select, Space, Table, Tag, Typography, DatePicker, message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined, PlayCircleOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { dstvApi } from '../../api/dstv.api';
import { DSTV_PACKAGES, DSTV_STATUS_META, ELECTRICITY_LOCATIONS } from '../../types';
import type { DstvSubscription, DstvStatus } from '../../types';

const { Title, Text } = Typography;
const { TextArea } = Input;

const PAYMENT_METHODS = ['Transfer', 'Cash', 'POS', 'Online'];

export default function DstvPage() {
  const qc = useQueryClient();
  const [open, setOpen]       = useState(false);
  const [renewTarget, setRenew] = useState<DstvSubscription | null>(null);
  const [saving, setSaving]   = useState(false);
  const [statusFilter, setStatus] = useState<string | undefined>();
  const [page, setPage]       = useState(1);
  const [form] = Form.useForm();
  const [renewForm] = Form.useForm();

  const { data, isFetching } = useQuery({
    queryKey: ['dstv', 'list', statusFilter, page],
    queryFn: () => dstvApi.list({ status: statusFilter, page }),
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['dstv'] });

  const handleSave = async (v: Record<string, unknown>) => {
    setSaving(true);
    try {
      await dstvApi.create({
        decoderNumber:  v.decoderNumber as string,
        location:       v.location as string,
        package:        v.package as string,
        durationMonths: v.durationMonths as number,
        amountNaira:    v.amountNaira as number,
        startDate:      v.startDate ? (v.startDate as dayjs.Dayjs).toISOString() : undefined,
        paymentMethod:  v.paymentMethod as string | undefined,
        vendor:         v.vendor as string | undefined,
        notes:          v.notes as string | undefined,
      });
      message.success('Subscription added');
      form.resetFields(); setOpen(false); refresh();
    } catch (e: unknown) {
      message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save');
    } finally { setSaving(false); }
  };

  const handleRenew = async (v: Record<string, unknown>) => {
    if (!renewTarget) return;
    setSaving(true);
    try {
      await dstvApi.renew(renewTarget.id, {
        durationMonths: v.durationMonths as number,
        amountNaira:    v.amountNaira as number,
        paymentMethod:  v.paymentMethod as string | undefined,
        notes:          v.notes as string | undefined,
      });
      message.success('Subscription renewed');
      renewForm.resetFields(); setRenew(null); refresh();
    } catch {
      message.error('Failed to renew');
    } finally { setSaving(false); }
  };

  const columns: ColumnsType<DstvSubscription> = [
    { title: 'Decoder / Smartcard', dataIndex: 'decoderNumber', width: 160 },
    { title: 'Location', dataIndex: 'location', width: 140 },
    { title: 'Package', dataIndex: 'package', width: 130 },
    { title: 'Start', dataIndex: 'startDate', width: 110, render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Expiry', dataIndex: 'expiryDate', width: 110, render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Days Left', dataIndex: 'daysToExpiry', width: 100,
      render: (v: number) => <Text type={v < 0 ? 'danger' : v <= 7 ? 'warning' : undefined}>{v < 0 ? `${Math.abs(v)}d ago` : `${v}d`}</Text> },
    { title: 'Amount', dataIndex: 'amountNaira', width: 110, render: (v: number) => `₦${v.toLocaleString()}` },
    { title: 'Status', dataIndex: 'status', width: 130,
      render: (v: DstvStatus) => { const m = DSTV_STATUS_META[v]; return <Tag color={m?.color}>{m?.label ?? v}</Tag>; } },
    { title: '', key: 'act', width: 90,
      render: (_: unknown, r: DstvSubscription) => (
        <Button size="small" icon={<PlayCircleOutlined />} onClick={() => { setRenew(r); renewForm.setFieldsValue({ durationMonths: r.durationMonths, amountNaira: r.amountNaira }); }}>Renew</Button>
      ) },
  ];

  const expiringCount = data?.items.filter(s => s.status !== 'Active').length ?? 0;

  return (
    <div>
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>DStv Subscriptions</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>Centralised register with automatic expiry tracking and renewal reminders</Text>
        </Col>
        <Col>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} />
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setOpen(true)}>Add Subscription</Button>
          </Space>
        </Col>
      </Row>

      {expiringCount > 0 && (
        <Alert type="warning" showIcon style={{ marginBottom: 16 }}
          message={`${expiringCount} subscription(s) expiring soon or expired — renew to avoid disruption.`} />
      )}

      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '12px 16px', borderBottom: '1px solid #f0f0f0' }}>
          <Select allowClear placeholder="Filter by status" style={{ width: 180 }} value={statusFilter}
            onChange={v => { setStatus(v); setPage(1); }}
            options={Object.entries(DSTV_STATUS_META).map(([k, m]) => ({ value: k, label: m.label }))} />
        </div>
        <Table<DstvSubscription>
          columns={columns} dataSource={data?.items ?? []} rowKey="id" loading={isFetching}
          pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0, onChange: setPage, showSizeChanger: false }}
          size="middle" scroll={{ x: 1150 }} style={{ padding: '0 8px' }} />
      </Card>

      {/* Add modal */}
      <Modal title="Add DStv Subscription" open={open} onOk={() => form.submit()}
        onCancel={() => { setOpen(false); form.resetFields(); }} confirmLoading={saving} okText="Save" width={540} destroyOnClose>
        <Form form={form} layout="vertical" onFinish={handleSave} initialValues={{ durationMonths: 1 }}>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="decoderNumber" label="Decoder / Smartcard No." rules={[{ required: true }]}>
                <Input placeholder="e.g. 4021 5566 778" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="location" label="Location" rules={[{ required: true }]}>
                <Select showSearch options={ELECTRICITY_LOCATIONS.map(l => ({ value: l, label: l }))} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="package" label="Package" rules={[{ required: true }]}>
                <Select options={DSTV_PACKAGES.map(p => ({ value: p, label: p }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="durationMonths" label="Duration (months)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={1} max={24} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="startDate" label="Start Date">
                <DatePicker style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="amountNaira" label="Amount Paid (₦)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="paymentMethod" label="Payment Method">
                <Select allowClear options={PAYMENT_METHODS.map(m => ({ value: m, label: m }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="vendor" label="Vendor / Agent">
                <Input />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="notes" label="Notes"><TextArea rows={2} maxLength={1000} /></Form.Item>
        </Form>
      </Modal>

      {/* Renew modal */}
      <Modal title={`Renew — ${renewTarget?.decoderNumber ?? ''}`} open={!!renewTarget} onOk={() => renewForm.submit()}
        onCancel={() => { setRenew(null); renewForm.resetFields(); }} confirmLoading={saving} okText="Renew" width={440} destroyOnClose>
        <Form form={renewForm} layout="vertical" onFinish={handleRenew}>
          <Form.Item name="durationMonths" label="Extend by (months)" rules={[{ required: true }]}>
            <InputNumber style={{ width: '100%' }} min={1} max={24} />
          </Form.Item>
          <Form.Item name="amountNaira" label="Amount Paid (₦)" rules={[{ required: true }]}>
            <InputNumber style={{ width: '100%' }} min={0} />
          </Form.Item>
          <Form.Item name="paymentMethod" label="Payment Method">
            <Select allowClear options={PAYMENT_METHODS.map(m => ({ value: m, label: m }))} />
          </Form.Item>
          <Form.Item name="notes" label="Notes"><TextArea rows={2} maxLength={1000} /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
