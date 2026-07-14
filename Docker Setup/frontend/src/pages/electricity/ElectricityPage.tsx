import { useState } from 'react';
import {
  Alert, Button, Card, Col, Form, Input, InputNumber, Modal, Popconfirm, Row,
  Select, Space, Table, Tag, Typography, DatePicker, message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined, ThunderboltOutlined, DeleteOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { electricityApi } from '../../api/electricity.api';
import {
  ELECTRICITY_LOCATIONS, ELECTRICITY_TYPES, ELECTRICITY_STATUS_META,
} from '../../types';
import type { ElectricityPurchase, ElectricityBalance, ElectricityStatus } from '../../types';

const { Title, Text } = Typography;
const { TextArea } = Input;

export default function ElectricityPage() {
  const qc = useQueryClient();
  const [open, setOpen]         = useState(false);
  const [saving, setSaving]     = useState(false);
  const [typeFilter, setType]   = useState<string | undefined>();
  const [locFilter, setLoc]     = useState<string | undefined>();
  const [page, setPage]         = useState(1);
  const [form] = Form.useForm();
  const purchaseType = Form.useWatch('purchaseType', form) as string | undefined;

  const { data: balances } = useQuery({
    queryKey: ['electricity', 'balances'],
    queryFn: electricityApi.balances,
    refetchInterval: 60_000,
  });

  const { data, isFetching } = useQuery({
    queryKey: ['electricity', 'list', typeFilter, locFilter, page],
    queryFn: () => electricityApi.list({ purchaseType: typeFilter, location: locFilter, page }),
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['electricity'] });

  const handleSave = async (v: Record<string, unknown>) => {
    setSaving(true);
    try {
      await electricityApi.create({
        purchaseType:           v.purchaseType as string,
        location:               v.location as string,
        amountNaira:            v.amountNaira as number,
        unitsKwh:               v.unitsKwh as number,
        purchaseDate:           v.purchaseDate ? (v.purchaseDate as dayjs.Dayjs).format('YYYY-MM-DD') : undefined,
        vendor:                 v.vendor as string | undefined,
        paymentReference:       v.paymentReference as string | undefined,
        tokenNumber:            v.tokenNumber as string | undefined,
        meterReadingKwh:        v.meterReadingKwh as number | undefined,
        lowBalanceThresholdKwh: v.lowBalanceThresholdKwh as number | undefined,
        notes:                  v.notes as string | undefined,
      });
      message.success('Electricity purchase recorded');
      form.resetFields(); setOpen(false); refresh();
    } catch (e: unknown) {
      message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save');
    } finally { setSaving(false); }
  };

  const handleDelete = async (id: string) => {
    try {
      await electricityApi.remove(id);
      message.success('Record deleted');
      refresh();
    } catch {
      message.error('Failed to delete');
    }
  };

  const columns: ColumnsType<ElectricityPurchase> = [
    { title: 'Date', dataIndex: 'purchaseDate', width: 110,
      render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Type', dataIndex: 'purchaseType', width: 90,
      render: (v: string) => <Tag color={v === 'PHED' ? 'blue' : 'purple'}>{v}</Tag> },
    { title: 'Location', dataIndex: 'location', width: 130 },
    { title: 'Vendor', dataIndex: 'vendor', ellipsis: true, render: (v?: string) => v ?? '—' },
    { title: 'Amount', dataIndex: 'amountNaira', width: 120,
      render: (v: number) => `₦${v.toLocaleString()}` },
    { title: 'Units', dataIndex: 'unitsKwh', width: 100,
      render: (v: number) => `${v.toLocaleString()} kWh` },
    { title: 'Balance', dataIndex: 'balanceAfterKwh', width: 110,
      render: (v: number) => <Text strong>{v.toLocaleString()} kWh</Text> },
    { title: 'Status', dataIndex: 'status', width: 110,
      render: (v: ElectricityStatus) => {
        const m = ELECTRICITY_STATUS_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      } },
    { title: 'Ref / Token', key: 'ref', ellipsis: true,
      render: (_: unknown, r: ElectricityPurchase) => r.paymentReference ?? r.tokenNumber ?? '—' },
    { title: 'Logged By', dataIndex: 'loggedByName', width: 130, ellipsis: true },
    { title: '', key: 'act', width: 60,
      render: (_: unknown, r: ElectricityPurchase) => (
        <Popconfirm title="Delete this record?" okText="Delete" okButtonProps={{ danger: true }}
          onConfirm={() => handleDelete(r.id)}>
          <Button size="small" danger icon={<DeleteOutlined />} />
        </Popconfirm>
      ) },
  ];

  return (
    <div>
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Electricity Management</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>PHED &amp; prepaid purchases, balances and low-balance alerts</Text>
        </Col>
        <Col>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} />
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setOpen(true)}>Record Purchase</Button>
          </Space>
        </Col>
      </Row>

      {/* Location balance cards */}
      <Row gutter={[12, 12]} style={{ marginBottom: 20 }}>
        {ELECTRICITY_LOCATIONS.map(loc => {
          const b: ElectricityBalance | undefined = balances?.find(x => x.location === loc);
          const status = b?.status ?? 'Active';
          const meta = ELECTRICITY_STATUS_META[status];
          return (
            <Col xs={12} sm={8} lg={4} key={loc}>
              <Card styles={{ body: { padding: '12px 16px' } }}
                style={{ borderColor: status !== 'Active' ? meta.color : undefined }}>
                <div style={{ fontSize: 11, color: '#8c8c8c' }}>{loc}</div>
                <div style={{ fontSize: 20, fontWeight: 700 }}>{(b?.balanceKwh ?? 0).toLocaleString()} <span style={{ fontSize: 12 }}>kWh</span></div>
                <Tag color={meta.color} style={{ marginTop: 4 }}>{meta.label}</Tag>
              </Card>
            </Col>
          );
        })}
      </Row>

      {balances?.some(b => b.status !== 'Active') && (
        <Alert type="warning" showIcon icon={<ThunderboltOutlined />} style={{ marginBottom: 16 }}
          message={`${balances.filter(b => b.status !== 'Active').length} location(s) at or below their low-balance threshold — arrange a recharge.`} />
      )}

      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '12px 16px', borderBottom: '1px solid #f0f0f0', display: 'flex', gap: 8 }}>
          <Select allowClear placeholder="Type" style={{ width: 130 }} value={typeFilter}
            onChange={v => { setType(v); setPage(1); }}
            options={ELECTRICITY_TYPES.map(t => ({ value: t, label: t }))} />
          <Select allowClear placeholder="Location" style={{ width: 170 }} value={locFilter}
            onChange={v => { setLoc(v); setPage(1); }}
            options={ELECTRICITY_LOCATIONS.map(l => ({ value: l, label: l }))} />
        </div>
        <Table<ElectricityPurchase>
          columns={columns} dataSource={data?.items ?? []} rowKey="id" loading={isFetching}
          pagination={{ current: page, pageSize: 20, total: data?.totalCount ?? 0,
            onChange: setPage, showSizeChanger: false }}
          size="middle" scroll={{ x: 1100 }} style={{ padding: '0 8px' }} />
      </Card>

      <Modal title="Record Electricity Purchase" open={open} onOk={() => form.submit()}
        onCancel={() => { setOpen(false); form.resetFields(); }} confirmLoading={saving}
        okText="Save" width={560} destroyOnClose>
        <Form form={form} layout="vertical" onFinish={handleSave} initialValues={{ purchaseType: 'PHED', lowBalanceThresholdKwh: 50 }}>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="purchaseType" label="Purchase Type" rules={[{ required: true }]}>
                <Select options={ELECTRICITY_TYPES.map(t => ({ value: t, label: t === 'PHED' ? 'PHED (postpaid)' : 'Prepaid (token)' }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="location" label="Location" rules={[{ required: true }]}>
                <Select options={ELECTRICITY_LOCATIONS.map(l => ({ value: l, label: l }))} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="amountNaira" label="Amount Spent (₦)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} placeholder="e.g. 50000" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="unitsKwh" label="Units (kWh)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} placeholder="e.g. 250" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="purchaseDate" label="Purchase Date">
                <DatePicker style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="vendor" label={purchaseType === 'Prepaid' ? 'Agent' : 'Vendor / Supplier'}>
                <Input placeholder="e.g. PHED / vendor name" />
              </Form.Item>
            </Col>
          </Row>
          {purchaseType === 'PHED' ? (
            <Form.Item name="paymentReference" label="Payment Reference / PO Number">
              <Input placeholder="e.g. PO-2026-014" />
            </Form.Item>
          ) : (
            <Row gutter={12}>
              <Col span={14}>
                <Form.Item name="tokenNumber" label="Token Number">
                  <Input placeholder="20-digit recharge token" />
                </Form.Item>
              </Col>
              <Col span={10}>
                <Form.Item name="meterReadingKwh" label="Current Meter Reading (kWh)"
                  tooltip="If supplied, this becomes the location's running balance.">
                  <InputNumber style={{ width: '100%' }} min={0} />
                </Form.Item>
              </Col>
            </Row>
          )}
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="lowBalanceThresholdKwh" label="Low-Balance Threshold (kWh)">
                <InputNumber style={{ width: '100%' }} min={0} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="notes" label="Notes">
            <TextArea rows={2} maxLength={1000} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
