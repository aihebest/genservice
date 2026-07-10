import { useState } from 'react';
import {
  Alert, Button, Card, Col, Form, Input, InputNumber, Modal, Row,
  Select, Space, Table, Tag, Tabs, Typography, DatePicker, message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined, ReloadOutlined, CarOutlined, FileProtectOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { vehicleRegistryApi } from '../../api/vehicleRegistry.api';
import {
  VEHICLE_OPERATIONAL_STATUSES, VEHICLE_OPERATIONAL_STATUS_META,
  VEHICLE_DOCUMENT_TYPES, VEHICLE_DOCUMENT_TYPE_META, VEHICLE_DOCUMENT_STATUS_META,
  OFFICE_LOCATIONS,
} from '../../types';
import type {
  VehicleRegistryRecord, VehicleDocument, VehicleOperationalStatus, VehicleDocumentStatus, VehicleDocumentType,
} from '../../types';

const { Title, Text } = Typography;
const { TextArea } = Input;

export default function VehicleRegistryPage() {
  const qc = useQueryClient();
  const [tab, setTab] = useState('vehicles');
  const [vehicleModal, setVehicleModal] = useState(false);
  const [docModal, setDocModal]         = useState(false);
  const [renewDoc, setRenewDoc]         = useState<VehicleDocument | null>(null);
  const [saving, setSaving]             = useState(false);
  const [vPage, setVPage]               = useState(1);
  const [dPage, setDPage]               = useState(1);
  const [docStatus, setDocStatus]       = useState<string | undefined>();
  const [vForm]     = Form.useForm();
  const [dForm]     = Form.useForm();
  const [renewForm] = Form.useForm();

  const { data: summary } = useQuery({ queryKey: ['vreg', 'summary'], queryFn: vehicleRegistryApi.summary, refetchInterval: 60_000 });
  const { data: vehicles, isFetching: vFetch } = useQuery({
    queryKey: ['vreg', 'vehicles', vPage],
    queryFn: () => vehicleRegistryApi.listVehicles({ page: vPage }),
    enabled: tab === 'vehicles',
  });
  const { data: docs, isFetching: dFetch } = useQuery({
    queryKey: ['vreg', 'documents', docStatus, dPage],
    queryFn: () => vehicleRegistryApi.listDocuments({ status: docStatus, page: dPage }),
    enabled: tab === 'documents',
  });
  const { data: allVehicles } = useQuery({
    queryKey: ['vreg', 'vehicles', 'all'],
    queryFn: () => vehicleRegistryApi.listVehicles({ page: 1 }),
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['vreg'] });

  const saveVehicle = async (v: Record<string, unknown>) => {
    setSaving(true);
    try {
      await vehicleRegistryApi.createVehicle({
        fleetNumber:        v.fleetNumber,
        registrationNumber: v.registrationNumber,
        vehicleType:        v.vehicleType,
        makeModel:          v.makeModel,
        yearOfManufacture:  v.yearOfManufacture,
        engineNumber:       v.engineNumber,
        chassisNumber:      v.chassisNumber,
        colour:             v.colour,
        assignedLocation:   v.assignedLocation,
        assignedDriver:     v.assignedDriver,
        acquisitionDate:    v.acquisitionDate ? (v.acquisitionDate as dayjs.Dayjs).toISOString() : undefined,
        operationalStatus:  v.operationalStatus,
        notes:              v.notes,
      });
      message.success('Vehicle registered');
      vForm.resetFields(); setVehicleModal(false); refresh();
    } catch (e: unknown) {
      message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save');
    } finally { setSaving(false); }
  };

  const saveDoc = async (v: Record<string, unknown>) => {
    setSaving(true);
    try {
      await vehicleRegistryApi.createDocument({
        vehicleId:        v.vehicleId as string,
        documentType:     v.documentType as string,
        expiryDate:       (v.expiryDate as dayjs.Dayjs).toISOString(),
        issueDate:        v.issueDate ? (v.issueDate as dayjs.Dayjs).toISOString() : undefined,
        issuingAuthority: v.issuingAuthority as string | undefined,
        renewalCostNaira: v.renewalCostNaira as number | undefined,
        notes:            v.notes as string | undefined,
      });
      message.success('Document added');
      dForm.resetFields(); setDocModal(false); refresh();
    } catch (e: unknown) {
      message.error((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save');
    } finally { setSaving(false); }
  };

  const handleRenewDoc = async (v: Record<string, unknown>) => {
    if (!renewDoc) return;
    setSaving(true);
    try {
      await vehicleRegistryApi.renewDocument(renewDoc.id, {
        expiryDate:       (v.expiryDate as dayjs.Dayjs).toISOString(),
        issueDate:        v.issueDate ? (v.issueDate as dayjs.Dayjs).toISOString() : undefined,
        renewalCostNaira: v.renewalCostNaira as number | undefined,
        issuingAuthority: v.issuingAuthority as string | undefined,
      });
      message.success('Document renewed');
      renewForm.resetFields(); setRenewDoc(null); refresh();
    } catch { message.error('Failed to renew'); }
    finally { setSaving(false); }
  };

  const vehicleColumns: ColumnsType<VehicleRegistryRecord> = [
    { title: 'Fleet No.', dataIndex: 'fleetNumber', width: 100 },
    { title: 'Reg. No.', dataIndex: 'registrationNumber', width: 130, render: (v: string) => <Text strong>{v}</Text> },
    { title: 'Type', dataIndex: 'vehicleType', width: 110 },
    { title: 'Make / Model', dataIndex: 'makeModel', ellipsis: true, render: (v?: string) => v ?? '—' },
    { title: 'Location', dataIndex: 'assignedLocation', width: 130, render: (v?: string) => v ?? '—' },
    { title: 'Driver', dataIndex: 'assignedDriver', width: 130, ellipsis: true, render: (v?: string) => v ?? '—' },
    { title: 'Status', dataIndex: 'operationalStatus', width: 120,
      render: (v: VehicleOperationalStatus) => { const m = VEHICLE_OPERATIONAL_STATUS_META[v]; return <Tag color={m?.color}>{m?.label ?? v}</Tag>; } },
    { title: 'Docs', key: 'docs', width: 130,
      render: (_: unknown, r: VehicleRegistryRecord) => (
        <Space size={4}>
          <Tag>{r.documentCount}</Tag>
          {r.expiringDocumentCount > 0 && <Tag color="orange">{r.expiringDocumentCount} exp.</Tag>}
          {r.expiredDocumentCount > 0 && <Tag color="red">{r.expiredDocumentCount} overdue</Tag>}
        </Space>
      ) },
  ];

  const docColumns: ColumnsType<VehicleDocument> = [
    { title: 'Vehicle', dataIndex: 'vehicleRegNo', width: 130, render: (v: string) => <Text strong>{v}</Text> },
    { title: 'Document', dataIndex: 'documentType', width: 180,
      render: (v: VehicleDocumentType) => VEHICLE_DOCUMENT_TYPE_META[v]?.label ?? v },
    { title: 'Issued', dataIndex: 'issueDate', width: 110, render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Expires', dataIndex: 'expiryDate', width: 110, render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Days Left', dataIndex: 'daysToExpiry', width: 100,
      render: (v: number) => <Text type={v < 0 ? 'danger' : v <= 14 ? 'warning' : undefined}>{v < 0 ? `${Math.abs(v)}d ago` : `${v}d`}</Text> },
    { title: 'Authority', dataIndex: 'issuingAuthority', ellipsis: true, render: (v?: string) => v ?? '—' },
    { title: 'Cost', dataIndex: 'renewalCostNaira', width: 110, render: (v?: number) => v != null ? `₦${v.toLocaleString()}` : '—' },
    { title: 'Status', dataIndex: 'status', width: 110,
      render: (v: VehicleDocumentStatus) => { const m = VEHICLE_DOCUMENT_STATUS_META[v]; return <Tag color={m?.color}>{m?.label ?? v}</Tag>; } },
    { title: '', key: 'act', width: 90,
      render: (_: unknown, r: VehicleDocument) => (
        <Button size="small" onClick={() => { setRenewDoc(r); renewForm.setFieldsValue({ issuingAuthority: r.issuingAuthority }); }}>Renew</Button>
      ) },
  ];

  return (
    <div>
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Vehicle Registry &amp; Documents</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>Master fleet register with statutory document expiry tracking</Text>
        </Col>
        <Col>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={refresh} />
            {tab === 'vehicles'
              ? <Button type="primary" icon={<PlusOutlined />} onClick={() => setVehicleModal(true)}>Add Vehicle</Button>
              : <Button type="primary" icon={<PlusOutlined />} onClick={() => setDocModal(true)}>Add Document</Button>}
          </Space>
        </Col>
      </Row>

      {/* Summary cards */}
      <Row gutter={[12, 12]} style={{ marginBottom: 20 }}>
        {[
          { label: 'Total Vehicles', value: summary?.totalVehicles, color: '#1677ff' },
          { label: 'Active', value: summary?.activeVehicles, color: '#52c41a' },
          { label: 'Documents', value: summary?.totalDocuments, color: '#722ed1' },
          { label: 'Expiring (≤14d)', value: summary?.expiringDocuments, color: '#fa8c16' },
          { label: 'Expired', value: summary?.expiredDocuments, color: '#ff4d4f' },
        ].map(s => (
          <Col xs={12} sm={8} lg={4} key={s.label}>
            <Card styles={{ body: { padding: '12px 16px' } }}>
              <div style={{ fontSize: 11, color: '#8c8c8c' }}>{s.label}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: s.color }}>{s.value ?? '—'}</div>
            </Card>
          </Col>
        ))}
      </Row>

      {(summary?.expiredDocuments ?? 0) > 0 && (
        <Alert type="error" showIcon style={{ marginBottom: 16 }}
          message={`${summary?.expiredDocuments} vehicle document(s) have EXPIRED — renew immediately to stay compliant.`} />
      )}

      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '0 16px', borderBottom: '1px solid #f0f0f0' }}>
          <Tabs activeKey={tab} onChange={setTab} size="small"
            items={[
              { key: 'vehicles',  label: <Space><CarOutlined />Vehicles</Space> },
              { key: 'documents', label: <Space><FileProtectOutlined />Documents</Space> },
            ]} />
        </div>

        {tab === 'vehicles' && (
          <Table<VehicleRegistryRecord>
            columns={vehicleColumns} dataSource={vehicles?.items ?? []} rowKey="id" loading={vFetch}
            pagination={{ current: vPage, pageSize: 20, total: vehicles?.totalCount ?? 0, onChange: setVPage, showSizeChanger: false }}
            size="middle" scroll={{ x: 1100 }} style={{ padding: '0 8px' }} />
        )}

        {tab === 'documents' && (
          <>
            <div style={{ padding: '12px 16px' }}>
              <Select allowClear placeholder="Filter by status" style={{ width: 180 }} value={docStatus}
                onChange={v => { setDocStatus(v); setDPage(1); }}
                options={Object.entries(VEHICLE_DOCUMENT_STATUS_META).map(([k, m]) => ({ value: k, label: m.label }))} />
            </div>
            <Table<VehicleDocument>
              columns={docColumns} dataSource={docs?.items ?? []} rowKey="id" loading={dFetch}
              pagination={{ current: dPage, pageSize: 20, total: docs?.totalCount ?? 0, onChange: setDPage, showSizeChanger: false }}
              size="middle" scroll={{ x: 1150 }} style={{ padding: '0 8px' }} />
          </>
        )}
      </Card>

      {/* Add Vehicle modal */}
      <Modal title="Register Vehicle" open={vehicleModal} onOk={() => vForm.submit()}
        onCancel={() => { setVehicleModal(false); vForm.resetFields(); }} confirmLoading={saving} okText="Save" width={640} destroyOnClose>
        <Form form={vForm} layout="vertical" onFinish={saveVehicle} initialValues={{ operationalStatus: 'Active' }}>
          <Row gutter={12}>
            <Col span={8}><Form.Item name="fleetNumber" label="Fleet No." rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="registrationNumber" label="Registration No." rules={[{ required: true }]}><Input /></Form.Item></Col>
            <Col span={8}><Form.Item name="vehicleType" label="Type" rules={[{ required: true }]}><Input placeholder="Pickup, SUV, Bus…" /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="makeModel" label="Make / Model"><Input /></Form.Item></Col>
            <Col span={6}><Form.Item name="yearOfManufacture" label="Year"><InputNumber style={{ width: '100%' }} min={1970} max={2100} /></Form.Item></Col>
            <Col span={6}><Form.Item name="colour" label="Colour"><Input /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="engineNumber" label="Engine No."><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="chassisNumber" label="Chassis No."><Input /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="assignedLocation" label="Assigned Location"><Select showSearch allowClear options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} /></Form.Item></Col>
            <Col span={12}><Form.Item name="assignedDriver" label="Assigned Driver"><Input /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="acquisitionDate" label="Acquisition Date"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="operationalStatus" label="Status"><Select options={VEHICLE_OPERATIONAL_STATUSES.map(s => ({ value: s, label: VEHICLE_OPERATIONAL_STATUS_META[s].label }))} /></Form.Item></Col>
          </Row>
          <Form.Item name="notes" label="Notes"><TextArea rows={2} maxLength={1000} /></Form.Item>
        </Form>
      </Modal>

      {/* Add Document modal */}
      <Modal title="Add Statutory Document" open={docModal} onOk={() => dForm.submit()}
        onCancel={() => { setDocModal(false); dForm.resetFields(); }} confirmLoading={saving} okText="Save" width={520} destroyOnClose>
        <Form form={dForm} layout="vertical" onFinish={saveDoc}>
          <Form.Item name="vehicleId" label="Vehicle" rules={[{ required: true }]}>
            <Select showSearch optionFilterProp="label"
              options={(allVehicles?.items ?? []).map(v => ({ value: v.id, label: `${v.registrationNumber} — ${v.fleetNumber}` }))} />
          </Form.Item>
          <Form.Item name="documentType" label="Document Type" rules={[{ required: true }]}>
            <Select options={VEHICLE_DOCUMENT_TYPES.map(t => ({ value: t, label: VEHICLE_DOCUMENT_TYPE_META[t].label }))} />
          </Form.Item>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="issueDate" label="Issue Date"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="expiryDate" label="Expiry Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="issuingAuthority" label="Issuing Authority"><Input /></Form.Item></Col>
            <Col span={12}><Form.Item name="renewalCostNaira" label="Renewal Cost (₦)"><InputNumber style={{ width: '100%' }} min={0} /></Form.Item></Col>
          </Row>
          <Form.Item name="notes" label="Notes"><TextArea rows={2} maxLength={1000} /></Form.Item>
        </Form>
      </Modal>

      {/* Renew Document modal */}
      <Modal title={`Renew — ${renewDoc ? VEHICLE_DOCUMENT_TYPE_META[renewDoc.documentType]?.label : ''} (${renewDoc?.vehicleRegNo ?? ''})`}
        open={!!renewDoc} onOk={() => renewForm.submit()}
        onCancel={() => { setRenewDoc(null); renewForm.resetFields(); }} confirmLoading={saving} okText="Renew" width={440} destroyOnClose>
        <Form form={renewForm} layout="vertical" onFinish={handleRenewDoc}>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="issueDate" label="New Issue Date"><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
            <Col span={12}><Form.Item name="expiryDate" label="New Expiry Date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} /></Form.Item></Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}><Form.Item name="renewalCostNaira" label="Renewal Cost (₦)"><InputNumber style={{ width: '100%' }} min={0} /></Form.Item></Col>
            <Col span={12}><Form.Item name="issuingAuthority" label="Issuing Authority"><Input /></Form.Item></Col>
          </Row>
        </Form>
      </Modal>
    </div>
  );
}
