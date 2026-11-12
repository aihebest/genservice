import { useState } from 'react';
import {
  Alert, AutoComplete, Button, Card, Col, Form, Input, InputNumber, Modal, Row,
  Select, Space, Tag, Tabs, Tooltip, Typography, DatePicker, message, Switch,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined, DropboxOutlined, SendOutlined, EditOutlined, CarOutlined } from '@ant-design/icons';
import { useAuthStore } from '../../store/authStore';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { dieselSupplyApi } from '../../api/dieselSupply.api';
import { DIESEL_DISTRIBUTION_TYPES, OFFICE_LOCATIONS, VEHICLE_LIST } from '../../types';
import type { DieselSupply, DieselDistribution, VehicleDieselSummary } from '../../types';

import FilterableTable from '../../components/shared/FilterableTable';
const { Title, Text } = Typography;
const { TextArea } = Input;

export default function DieselSupplyPage() {
  const qc = useQueryClient();
  const [tab, setTab]           = useState('supplies');
  const [supplyModal, setSupplyModal] = useState(false);
  const [distModal, setDistModal]     = useState(false);
  const [saving, setSaving]     = useState(false);
  const [sPage, setSPage]       = useState(1);
  const [dPage, setDPage]       = useState(1);
  const [sForm] = Form.useForm();
  const [dForm] = Form.useForm();
  // Department Manager correction of a bulk supply batch (enforced server-side too).
  const role    = useAuthStore(s => s.user?.role);
  const canEdit = role === 'DepartmentManager' || role === 'SystemAdmin';
  const [editSupply, setEditSupply] = useState<DieselSupply | null>(null);
  const [eForm] = Form.useForm();
  const openEditSupply = (r: DieselSupply) => {
    setEditSupply(r);
    eForm.setFieldsValue({
      supplyDate: dayjs(r.supplyDate), vendor: r.vendor, invoiceNumber: r.invoiceNumber,
      quantityLitres: r.quantityLitres, unitPriceNaira: r.unitPriceNaira,
      storageLocation: r.storageLocation, receivingOfficer: r.receivingOfficer, notes: r.notes,
    });
  };
  const saveEditSupply = async () => {
    if (!editSupply) return;
    const v = await eForm.validateFields();
    setSaving(true);
    try {
      await dieselSupplyApi.updateSupply(editSupply.id, { ...v, supplyDate: v.supplyDate?.format('YYYY-MM-DD') });
      message.success(`${editSupply.supplyReference} updated`);
      setEditSupply(null); qc.invalidateQueries({ queryKey: ['diesel-supply'] });
    } catch (e: unknown) {
      message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Could not update the batch');
    } finally { setSaving(false); }
  };
  const distType = Form.useWatch('distributionType', dForm) as string | undefined;
  // Per-vehicle consumption tab.
  const [vsDays, setVsDays] = useState(90);
  const { data: vehSummary, isFetching: vsFetch } = useQuery({
    queryKey: ['diesel-supply', 'vehicle-summary', vsDays],
    queryFn:  () => dieselSupplyApi.vehicleSummary(vsDays),
    enabled:  tab === 'vehicles',
  });

  const { data: summary } = useQuery({ queryKey: ['diesel-supply', 'summary'], queryFn: dieselSupplyApi.summary, refetchInterval: 60_000 });
  const { data: supplies, isFetching: sFetch } = useQuery({
    queryKey: ['diesel-supply', 'supplies', sPage], queryFn: () => dieselSupplyApi.listSupplies({ page: sPage }), enabled: tab === 'supplies',
  });
  const { data: dists, isFetching: dFetch } = useQuery({
    queryKey: ['diesel-supply', 'distributions', dPage], queryFn: () => dieselSupplyApi.listDistributions({ page: dPage }), enabled: tab === 'distributions',
  });
  const { data: available } = useQuery({ queryKey: ['diesel-supply', 'available'], queryFn: dieselSupplyApi.availableSupplies });

  const refresh = () => qc.invalidateQueries({ queryKey: ['diesel-supply'] });

  const saveSupply = async (v: Record<string, unknown>) => {
    setSaving(true);
    try {
      await dieselSupplyApi.createSupply({
        vendor:            v.vendor as string,
        quantityLitres:    v.quantityLitres as number,
        unitPriceNaira:    v.unitPriceNaira as number,
        supplyDate:        v.supplyDate ? (v.supplyDate as dayjs.Dayjs).format('YYYY-MM-DD') : undefined,
        invoiceNumber:     v.invoiceNumber as string | undefined,
        storageLocation:   v.storageLocation as string | undefined,
        receivingOfficer:  v.receivingOfficer as string | undefined,
        notes:             v.notes as string | undefined,
      });
      message.success('Bulk supply recorded');
      sForm.resetFields(); setSupplyModal(false); refresh();
    } catch (e: unknown) {
      message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save');
    } finally { setSaving(false); }
  };

  const saveDist = async (v: Record<string, unknown>) => {
    setSaving(true);
    try {
      await dieselSupplyApi.createDistribution({
        distributionType:      v.distributionType as string,
        supplyType:            v.supplyType as string | undefined,
        bulkSupplyReference:   (v.bulkSupplyReference as string).trim(),
        quantityLitres:        v.quantityLitres as number,
        distributionDate:      v.distributionDate ? (v.distributionDate as dayjs.Dayjs).format('YYYY-MM-DD') : undefined,
        purpose:               v.purpose as string | undefined,
        vehicleRegNo:          v.vehicleRegNo as string | undefined,
        driver:                v.driver as string | undefined,
        odometerReading:       v.odometerReading as string | undefined,
        destinationLocation:   v.destinationLocation as string | undefined,
        issuingOfficer:        v.issuingOfficer as string | undefined,
        receivingOfficer:      v.receivingOfficer as string | undefined,
        recipientAcknowledged: v.recipientAcknowledged as boolean | undefined,
        notes:                 v.notes as string | undefined,
      });
      message.success('Diesel distributed');
      dForm.resetFields(); setDistModal(false); refresh();
    } catch (e: unknown) {
      message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Distribution failed');
    } finally { setSaving(false); }
  };

  const supplyColumns: ColumnsType<DieselSupply> = [
    { title: 'Ref', dataIndex: 'supplyReference', width: 110, render: (v: string) => <Text strong>{v}</Text> },
    { title: 'Date', dataIndex: 'supplyDate', width: 110, render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Vendor', dataIndex: 'vendor', ellipsis: true },
    { title: 'Waybill/Invoice', dataIndex: 'invoiceNumber', width: 130, render: (v?: string) => v ?? '—' },
    { title: 'Supplied', dataIndex: 'quantityLitres', width: 110, render: (v: number) => `${v.toLocaleString()} L` },
    { title: 'Remaining', dataIndex: 'quantityRemainingLitres', width: 120,
      render: (v: number) => <Text strong type={v <= 0 ? 'secondary' : undefined}>{v.toLocaleString()} L</Text> },
    { title: 'Unit ₦/L', dataIndex: 'unitPriceNaira', width: 100, render: (v: number) => `₦${v.toLocaleString()}` },
    { title: 'Total', dataIndex: 'totalCostNaira', width: 130, render: (v: number) => <Text strong style={{ color: '#cf1322' }}>₦{v.toLocaleString()}</Text> },
    { title: 'Storage', dataIndex: 'storageLocation', width: 130, render: (v?: string) => v ?? '—' },
    { title: 'Notes', dataIndex: 'notes', key: 'notes', width: 200, ellipsis: true,
      render: (v?: string) => v
        ? <Tooltip title={v}><Text style={{ fontSize: 12 }}>{v}</Text></Tooltip>
        : <Text type="secondary" style={{ fontSize: 12 }}>—</Text> },
    ...(canEdit ? [{
      title: '', key: 'edit', width: 60,
      render: (_: unknown, r: DieselSupply) => (
        <Tooltip title="Edit batch"><Button size="small" icon={<EditOutlined />} onClick={() => openEditSupply(r)} /></Tooltip>
      ),
    }] : []),
  ];

  const distColumns: ColumnsType<DieselDistribution> = [
    { title: 'Ref', dataIndex: 'distributionReference', width: 110, render: (v: string) => <Text strong>{v}</Text> },
    { title: 'Date', dataIndex: 'distributionDate', width: 110, render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Type', dataIndex: 'distributionType', width: 100, render: (v: string) => <Tag color={v === 'Vehicle' ? 'blue' : 'green'}>{v}</Tag> },
    { title: 'Supply Type', dataIndex: 'supplyType', key: 'supplyType', width: 115,
      render: (v?: string) => v === 'Extra'
        ? <Tag color="orange">Extra / Top-up</Tag>
        : <Tag>Regular</Tag> },
    { title: 'Recipient', key: 'recipient', ellipsis: true,
      render: (_: unknown, r: DieselDistribution) => r.vehicleRegNo ?? r.destinationLocation ?? '—' },
    { title: 'Odometer', dataIndex: 'odometerReading', key: 'odometer', width: 100, ellipsis: true,
      render: (v?: string) => v ?? '—' },
    { title: 'Qty', dataIndex: 'quantityLitres', width: 90, render: (v: number) => `${v.toLocaleString()} L` },
    // Option B — compared with the vehicle's previous issue (no extra input needed).
    { title: 'Previous Issue', key: 'prevIssue', width: 135,
      render: (_: unknown, r: DieselDistribution) => r.previousQuantityLitres != null
        ? <Tooltip title={`Odometer then: ${r.previousOdometer?.toLocaleString() ?? '—'}`}>
            <Text style={{ fontSize: 12 }}>{r.previousQuantityLitres.toLocaleString()} L · {dayjs(r.previousDistributionDate).format('D MMM')}</Text>
          </Tooltip>
        : <Text type="secondary" style={{ fontSize: 12 }}>—</Text> },
    { title: 'Km Covered', dataIndex: 'distanceKm', key: 'distanceKm', width: 105,
      render: (v?: number) => v != null ? `${v.toLocaleString()} km` : <Text type="secondary" style={{ fontSize: 12 }}>—</Text> },
    { title: 'Km / Litre', dataIndex: 'kmPerLitre', key: 'kmPerLitre', width: 100,
      render: (v?: number) => v != null ? <Text strong>{v.toFixed(1)}</Text> : <Text type="secondary" style={{ fontSize: 12 }}>—</Text> },
    { title: 'From Supply', dataIndex: 'bulkSupplyReference', width: 110 },
    { title: 'Purpose', dataIndex: 'purpose', ellipsis: true, render: (v?: string) => v ?? '—' },
    { title: 'Issued By', dataIndex: 'issuingOfficer', width: 130, ellipsis: true, render: (v?: string) => v ?? '—' },
    { title: 'Ack.', dataIndex: 'recipientAcknowledged', width: 60,
      render: (v: boolean) => v ? <Tag color="green">Yes</Tag> : <Tag>No</Tag> },
  ];

  return (
    <div>
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Diesel Supply &amp; Distribution</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>Bulk procurement, running balance and traceable distribution</Text>
        </Col>
        <Col>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={refresh} />
            {tab === 'supplies'
              ? <Button type="primary" icon={<PlusOutlined />} onClick={() => setSupplyModal(true)}>Add Bulk Supply</Button>
              : <Button type="primary" icon={<SendOutlined />} onClick={() => setDistModal(true)}>Distribute Diesel</Button>}
          </Space>
        </Col>
      </Row>

      {/* Stock summary */}
      <Row gutter={[12, 12]} style={{ marginBottom: 20 }}>
        {[
          { label: 'Available Balance', value: `${(summary?.availableBalanceLitres ?? 0).toLocaleString()} L`, color: '#1677ff' },
          { label: 'Total Supplied', value: `${(summary?.totalSuppliedLitres ?? 0).toLocaleString()} L`, color: '#52c41a' },
          { label: 'Total Distributed', value: `${(summary?.totalDistributedLitres ?? 0).toLocaleString()} L`, color: '#fa8c16' },
          { label: 'Purchase Value', value: `₦${(summary?.totalPurchaseValueNaira ?? 0).toLocaleString()}`, color: '#eb2f96' },
          { label: 'Active Batches', value: summary?.activeSupplyBatches ?? 0, color: '#722ed1' },
        ].map(s => (
          <Col xs={12} sm={8} lg={4} key={s.label}>
            <Card styles={{ body: { padding: '12px 16px' } }}>
              <div style={{ fontSize: 11, color: '#8c8c8c' }}>{s.label}</div>
              <div style={{ fontSize: 18, fontWeight: 700, color: s.color }}>{s.value}</div>
            </Card>
          </Col>
        ))}
      </Row>

      {(summary?.availableBalanceLitres ?? 0) <= 500 && (
        <Alert type="warning" showIcon icon={<DropboxOutlined />} style={{ marginBottom: 16 }}
          message="Diesel stock is low (≤ 500 L available). Arrange a resupply." />
      )}

      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '0 16px', borderBottom: '1px solid #f0f0f0' }}>
          <Tabs activeKey={tab} onChange={setTab} size="small"
            items={[
              { key: 'supplies',      label: <Space><DropboxOutlined />Bulk Supplies</Space> },
              { key: 'distributions', label: <Space><SendOutlined />Distributions</Space> },
              { key: 'vehicles',      label: <Space><CarOutlined />Per Vehicle</Space> },
            ]} />
        </div>
        {tab === 'supplies' && (
          <FilterableTable<DieselSupply> columns={supplyColumns} dataSource={supplies?.items ?? []} rowKey="id" loading={sFetch}
            pagination={{ current: sPage, pageSize: 20, total: supplies?.totalCount ?? 0, onChange: setSPage, showSizeChanger: false }}
            size="middle" scroll={{ x: 1350 }} style={{ padding: '0 8px' }} />
        )}
        {tab === 'distributions' && (
          <FilterableTable<DieselDistribution> columns={distColumns} dataSource={dists?.items ?? []} rowKey="id" loading={dFetch}
            pagination={{ current: dPage, pageSize: 20, total: dists?.totalCount ?? 0, onChange: setDPage, showSizeChanger: false }}
            size="middle" scroll={{ x: 1650 }} style={{ padding: '0 8px' }} />
        )}
        {tab === 'vehicles' && (
          <div style={{ padding: 16 }}>
            <Space style={{ marginBottom: 12 }} wrap>
              <Text type="secondary">Period:</Text>
              <Select value={vsDays} onChange={setVsDays} style={{ width: 140 }}
                options={[30, 90, 180, 365].map(d => ({ value: d, label: `Last ${d} days` }))} />
              <Text type="secondary" style={{ fontSize: 12 }}>
                Km covered = odometer now − odometer at the previous issue. Km/Litre = km covered ÷ litres issued at that previous issue.
              </Text>
            </Space>
            <FilterableTable<VehicleDieselSummary> rowKey="vehicleRegNo" size="middle" loading={vsFetch}
              dataSource={vehSummary ?? []} pagination={{ pageSize: 20, showSizeChanger: false }}
              columns={[
                { title: 'Vehicle', dataIndex: 'vehicleRegNo', key: 'reg', width: 140, render: (v: string) => <Text strong>{v}</Text> },
                { title: 'Issues', dataIndex: 'issues', key: 'issues', width: 80 },
                { title: 'Total Issued', dataIndex: 'totalLitres', key: 'total', width: 120, render: (v: number) => `${v.toLocaleString()} L` },
                { title: 'Last Issue', dataIndex: 'lastIssueDate', key: 'last', width: 115, render: (v: string) => dayjs(v).format('D MMM YY') },
                { title: 'Last Odometer', dataIndex: 'lastOdometer', key: 'odo', width: 125, render: (v?: number) => v != null ? v.toLocaleString() : '—' },
                { title: 'Km Covered', dataIndex: 'distanceKm', key: 'km', width: 115, render: (v?: number) => v != null ? `${v.toLocaleString()} km` : '—' },
                { title: 'Litres Used', dataIndex: 'litresForDistance', key: 'lused', width: 110, render: (v?: number) => v != null ? `${v.toLocaleString()} L` : '—' },
                { title: 'Avg Km / Litre', dataIndex: 'averageKmPerLitre', key: 'kmpl', width: 125,
                  render: (v?: number) => v != null ? <Text strong>{v.toFixed(1)}</Text> : '—' },
              ]} />
          </div>
        )}
      </Card>

      {/* Add supply modal */}
      {/* Edit bulk supply batch (Department Manager) */}
      <Modal title={`Edit ${editSupply?.supplyReference ?? ''}`} open={!!editSupply} onOk={saveEditSupply}
        onCancel={() => setEditSupply(null)} okText="Save Changes" confirmLoading={saving} destroyOnClose>
        {editSupply && (
          <Alert type="info" showIcon style={{ marginBottom: 12 }}
            message={`${(editSupply.quantityLitres - editSupply.quantityRemainingLitres).toLocaleString()} L already distributed from this batch is kept — the remaining balance adjusts to the new quantity.`} />
        )}
        <Form form={eForm} layout="vertical">
          <Row gutter={12}>
            <Col span={12}><Form.Item name="supplyDate" label="Supply Date"><DatePicker style={{ width: '100%' }} format="D MMM YYYY" /></Form.Item></Col>
            <Col span={12}><Form.Item name="vendor" label="Vendor" rules={[{ required: true }]}><Input /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="quantityLitres" label="Quantity (L)" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={1} /></Form.Item></Col>
            <Col span={12}><Form.Item name="unitPriceNaira" label="Unit Price (₦/L)" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={0} /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="invoiceNumber" label="Waybill / Invoice"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="storageLocation" label="Storage"><Input /></Form.Item></Col>
          </Row>
          <Form.Item name="receivingOfficer" label="Receiving Officer"><Input /></Form.Item>
          <Form.Item name="notes" label="Notes"><TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>

      <Modal title="Record Bulk Diesel Supply" open={supplyModal} onOk={() => sForm.submit()}
        onCancel={() => { setSupplyModal(false); sForm.resetFields(); }} confirmLoading={saving} okText="Save" width={560} destroyOnClose>
        <Form form={sForm} layout="vertical" onFinish={saveSupply}>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="vendor" label="Vendor" rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="supplyDate" label="Supply Date"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="quantityLitres" label="Quantity (L)" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={0} /></Form.Item></Col>
            <Col span={8}><Form.Item name="unitPriceNaira" label="Unit Price (₦/L)" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={0} /></Form.Item></Col>
            <Col span={8}><Form.Item name="invoiceNumber" label="Invoice / Waybill"><Input /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="storageLocation" label="Storage Location"><Select showSearch allowClear options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} /></Form.Item></Col>
            <Col span={12}><Form.Item name="receivingOfficer" label="Receiving Officer"><Input /></Form.Item></Col>
          </Row>
          <Form.Item name="notes" label="Notes"><TextArea rows={2} maxLength={1000} /></Form.Item>
        </Form>
      </Modal>

      {/* Distribute modal */}
      <Modal title="Distribute Diesel" open={distModal} onOk={() => dForm.submit()}
        onCancel={() => { setDistModal(false); dForm.resetFields(); }} confirmLoading={saving} okText="Issue" width={560} destroyOnClose>
        <Form form={dForm} layout="vertical" onFinish={saveDist} initialValues={{ distributionType: 'Vehicle' }}>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="distributionType" label="Distribute To" rules={[{ required: true }]}>
                <Select options={DIESEL_DISTRIBUTION_TYPES.map(t => ({ value: t, label: t }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="bulkSupplyReference" label="From Supply Batch" rules={[{ required: true, message: 'Select a supply batch' }]}
                tooltip="Pick a bulk-supply batch. The quantity issued is deducted from that batch's balance and from the total available.">
                <Select
                  showSearch
                  optionFilterProp="label"
                  disabled={(available ?? []).length === 0}
                  placeholder={(available ?? []).length ? 'Select a batch' : 'No batch with balance — add a bulk supply first'}
                  options={(available ?? []).map(s => ({
                    value: s.supplyReference,
                    label: `${s.supplyReference} — ${s.quantityRemainingLitres.toLocaleString()} L left`,
                  }))} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="quantityLitres" label="Quantity (L)" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={0} /></Form.Item></Col>
            <Col span={8}><Form.Item name="distributionDate" label="Date"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={8}>
              <Form.Item name="supplyType" label="Supply Type" initialValue="Regular"
                tooltip="Mark whether this is a regular distribution or an extra / top-up supply.">
                <Select options={[
                  { value: 'Regular', label: 'Regular' },
                  { value: 'Extra',   label: 'Extra / Top-up' },
                ]} />
              </Form.Item>
            </Col>
          </Row>

          {distType === 'Vehicle' ? (
            <Row gutter={12}>
              <Col span={12}>
                <Form.Item name="vehicleRegNo" label="Vehicle" rules={[{ required: true }]}
                  tooltip="Pick from the list or type the vehicle registration manually.">
                  <AutoComplete
                    placeholder="e.g. PHC 185 AM"
                    filterOption={(input, option) =>
                      String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())}
                    options={VEHICLE_LIST.map(v => ({ value: v.regNo, label: `${v.regNo} — ${v.description}` }))} />
                </Form.Item>
              </Col>
              <Col span={6}><Form.Item name="driver" label="Driver"><Input /></Form.Item></Col>
              <Col span={6}><Form.Item name="odometerReading" label="Odometer"><Input /></Form.Item></Col>
            </Row>
          ) : (
            <Form.Item name="destinationLocation" label="Destination Location" rules={[{ required: true }]}>
              <Select showSearch options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
            </Form.Item>
          )}

          <Form.Item name="purpose" label="Purpose / Destination"><Input /></Form.Item>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="issuingOfficer" label="Issuing Officer"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="receivingOfficer" label="Receiving Officer"><Input /></Form.Item></Col>
          </Row>
          <Form.Item name="recipientAcknowledged" label="Recipient Acknowledged?" valuePropName="checked" initialValue={false}>
            <Switch checkedChildren="Yes" unCheckedChildren="No" />
          </Form.Item>
          <Form.Item name="notes" label="Notes"><TextArea rows={2} maxLength={1000} /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
