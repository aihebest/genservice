/**
 * DieselRequisitionsTab — full CRUD + workflow tab for diesel fuel requisitions.
 * Embedded inside FuelPage as the "Diesel Requisitions" tab.
 *
 * Workflow:  Pending → Approved → Dispensed
 *                    ↘ Rejected
 */
import { useState } from 'react';
import {
  Alert, Badge, Button, Col, Descriptions, Divider, Drawer, Form, InputNumber,
  Modal, Row, Select, Space, Statistic, Table, Tag, Tooltip, Typography,
  message, Input, Dropdown,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckCircleOutlined, CloseCircleOutlined, DeleteOutlined, DownloadOutlined,
  EyeOutlined, FileProtectOutlined, PlusOutlined, ThunderboltOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '../../store/authStore';
import { dieselRequisitionsApi } from '../../api/dieselRequisitions.api';
import { exportDieselRequisitions } from '../../api/export.api';
import type {
  DieselRequisition,
  CreateDieselRequisitionPayload,
  DispenseDieselPayload,
} from '../../types';
import { DIESEL_REQUISITION_STATUSES } from '../../types';
import dayjs from 'dayjs';

const { Text } = Typography;

// ── Constants ─────────────────────────────────────────────────────────────────

const EQUIPMENT_TYPES = ['Generator', 'Vehicle', 'Fuel Store', 'Other'] as const;

const APPROVER_ROLES = ['SystemAdmin', 'DepartmentManager', 'Supervisor'];
const DISPENSER_ROLES = ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'StoreOfficer'];

function statusColor(s: string) {
  switch (s) {
    case 'Pending':   return 'gold';
    case 'Approved':  return 'blue';
    case 'Dispensed': return 'green';
    case 'Rejected':  return 'red';
    default:          return 'default';
  }
}

const fmt = (d?: string | null) => d ? dayjs(d).format('DD MMM YYYY HH:mm') : '—';
const fmtShort = (d?: string | null) => d ? dayjs(d).format('DD MMM YYYY') : '—';
const fmtN = (n?: number | null, dp = 0) =>
  n != null ? n.toLocaleString('en-NG', { minimumFractionDigits: dp, maximumFractionDigits: dp }) : '—';
const fmtMoney = (n?: number | null) =>
  n != null ? `₦${n.toLocaleString('en-NG', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : '—';

// ── Columns ───────────────────────────────────────────────────────────────────

function buildColumns(
  userRole: string | undefined,
  onView:     (r: DieselRequisition) => void,
  onApprove:  (r: DieselRequisition) => void,
  onReject:   (r: DieselRequisition) => void,
  onDispense: (r: DieselRequisition) => void,
  onDelete:   (r: DieselRequisition) => void,
): ColumnsType<DieselRequisition> {
  const canApprove  = APPROVER_ROLES.includes(userRole ?? '');
  const canDispense = DISPENSER_ROLES.includes(userRole ?? '');

  return [
    {
      title:  'Req. No.',
      dataIndex: 'requisitionNumber',
      width: 110,
      render: (v: string) => <Text code style={{ fontSize: 12 }}>{v}</Text>,
    },
    {
      title:  'Purpose',
      dataIndex: 'purpose',
      ellipsis: true,
    },
    {
      title:    'Equip. Type',
      dataIndex: 'equipmentType',
      width: 110,
      render: (v: string) => {
        const icon = v === 'Generator' ? <ThunderboltOutlined /> : null;
        return <Space size={4}>{icon}{v}</Space>;
      },
    },
    {
      title:  'Reference',
      dataIndex: 'equipmentReference',
      width: 120,
      render: (v?: string) => v ?? <Text type="secondary">—</Text>,
    },
    {
      title:  'Location',
      dataIndex: 'location',
      width: 130,
    },
    {
      title:     'Qty Req. (L)',
      dataIndex: 'quantityRequestedLitres',
      width: 110,
      align:     'right',
      render: (v: number) => <Text>{fmtN(v)}</Text>,
    },
    {
      title:  'Requested By',
      dataIndex: 'requestedByName',
      width: 130,
    },
    {
      title:  'Status',
      dataIndex: 'status',
      width: 100,
      render: (s: string) => <Tag color={statusColor(s)}>{s}</Tag>,
    },
    {
      title:  'Date',
      dataIndex: 'createdAt',
      width: 110,
      render: (v: string) => fmtShort(v),
    },
    {
      title:  'Actions',
      key:    'actions',
      width:  150,
      fixed:  'right',
      render: (_, row) => (
        <Space size={4} wrap>
          <Tooltip title="View details">
            <Button size="small" icon={<EyeOutlined />} onClick={() => onView(row)} />
          </Tooltip>

          {/* Approve — only visible for Pending */}
          {row.status === 'Pending' && canApprove && (
            <Tooltip title="Approve">
              <Button
                size="small"
                type="primary"
                icon={<CheckCircleOutlined />}
                style={{ background: '#52c41a', borderColor: '#52c41a' }}
                onClick={() => onApprove(row)}
              />
            </Tooltip>
          )}

          {/* Reject — only visible for Pending */}
          {row.status === 'Pending' && canApprove && (
            <Tooltip title="Reject">
              <Button
                size="small"
                danger
                icon={<CloseCircleOutlined />}
                onClick={() => onReject(row)}
              />
            </Tooltip>
          )}

          {/* Dispense — only visible for Approved */}
          {row.status === 'Approved' && canDispense && (
            <Tooltip title="Dispense Diesel">
              <Button
                size="small"
                type="primary"
                icon={<FileProtectOutlined />}
                onClick={() => onDispense(row)}
              />
            </Tooltip>
          )}

          {/* Delete — only for Pending */}
          {row.status === 'Pending' && (
            <Tooltip title="Delete">
              <Button
                size="small"
                danger
                ghost
                icon={<DeleteOutlined />}
                onClick={() => onDelete(row)}
              />
            </Tooltip>
          )}
        </Space>
      ),
    },
  ];
}

// ── Main Component ────────────────────────────────────────────────────────────

export default function DieselRequisitionsTab() {
  const user     = useAuthStore(s => s.user);
  const role     = user?.role;
  const qc       = useQueryClient();

  // Filters
  const [statusFilter,   setStatusFilter]   = useState<string | undefined>();
  const [equipFilter,    setEquipFilter]    = useState<string | undefined>();
  const [page,           setPage]           = useState(1);

  // Drawers / modals
  const [createOpen,     setCreateOpen]     = useState(false);
  const [viewTarget,     setViewTarget]     = useState<DieselRequisition | null>(null);
  const [approveTarget,  setApproveTarget]  = useState<DieselRequisition | null>(null);
  const [rejectTarget,   setRejectTarget]   = useState<DieselRequisition | null>(null);
  const [dispenseTarget, setDispenseTarget] = useState<DieselRequisition | null>(null);

  // Forms
  const [createForm]  = Form.useForm<CreateDieselRequisitionPayload>();
  const [rejectForm]  = Form.useForm<{ reason: string }>();
  const [dispenseForm] = Form.useForm<DispenseDieselPayload>();

  // ── Query ────────────────────────────────────────────────────────────────────
  const { data, isFetching } = useQuery({
    queryKey: ['diesel', 'requisitions', statusFilter, equipFilter, page],
    queryFn:  () =>
      dieselRequisitionsApi.list({ status: statusFilter, equipType: equipFilter, page, pageSize: 15 }),
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['diesel', 'requisitions'] });

  // ── Mutations ─────────────────────────────────────────────────────────────────
  const createMut = useMutation({
    mutationFn: (payload: CreateDieselRequisitionPayload) => dieselRequisitionsApi.create(payload),
    onSuccess: () => { message.success('Diesel requisition submitted.'); setCreateOpen(false); createForm.resetFields(); refresh(); },
    onError: () => message.error('Failed to submit requisition.'),
  });

  const approveMut = useMutation({
    mutationFn: ({ id, notes }: { id: string; notes?: string }) => dieselRequisitionsApi.approve(id, notes),
    onSuccess: () => { message.success('Requisition approved.'); setApproveTarget(null); refresh(); },
    onError: () => message.error('Failed to approve.'),
  });

  const rejectMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => dieselRequisitionsApi.reject(id, reason),
    onSuccess: () => { message.success('Requisition rejected.'); setRejectTarget(null); rejectForm.resetFields(); refresh(); },
    onError: () => message.error('Failed to reject.'),
  });

  const dispenseMut = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: DispenseDieselPayload }) =>
      dieselRequisitionsApi.dispense(id, payload),
    onSuccess: () => {
      message.success('Diesel dispensed. Fuel record created.');
      setDispenseTarget(null);
      dispenseForm.resetFields();
      refresh();
    },
    onError: () => message.error('Failed to record dispensing.'),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => dieselRequisitionsApi.delete(id),
    onSuccess: () => { message.success('Requisition deleted.'); refresh(); },
    onError: () => message.error('Failed to delete requisition.'),
  });

  // ── Delete confirm ────────────────────────────────────────────────────────────
  const handleDelete = (r: DieselRequisition) => {
    Modal.confirm({
      title:   'Delete Requisition',
      content: `Delete ${r.requisitionNumber}? This action cannot be undone.`,
      okType:  'danger',
      okText:  'Delete',
      onOk:    () => deleteMut.mutate(r.id),
    });
  };

  // ── Columns ───────────────────────────────────────────────────────────────────
  const columns = buildColumns(
    role,
    setViewTarget,
    setApproveTarget,
    setRejectTarget,
    setDispenseTarget,
    handleDelete,
  );

  // ── KPI summary ───────────────────────────────────────────────────────────────
  const pending   = data?.pendingCount  ?? 0;
  const approved  = data?.approvedCount ?? 0;
  const litres    = data?.totalDispensedLitresThisMonth ?? 0;
  const cost      = data?.totalCostThisMonth ?? 0;

  // ── Render ────────────────────────────────────────────────────────────────────
  return (
    <div style={{ padding: 20 }}>
      {/* KPIs */}
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={12} sm={6}>
          <div style={{ background: '#fffbe6', border: '1px solid #ffe58f', borderRadius: 8, padding: '12px 16px', textAlign: 'center' }}>
            <Badge count={pending} offset={[8, -2]}>
              <Text strong style={{ fontSize: 13, color: '#d48806' }}>Pending</Text>
            </Badge>
            <div style={{ fontSize: 24, fontWeight: 700, color: '#d48806', marginTop: 4 }}>{pending}</div>
          </div>
        </Col>
        <Col xs={12} sm={6}>
          <div style={{ background: '#e6f4ff', border: '1px solid #91caff', borderRadius: 8, padding: '12px 16px', textAlign: 'center' }}>
            <Text strong style={{ fontSize: 13, color: '#0958d9' }}>Approved</Text>
            <div style={{ fontSize: 24, fontWeight: 700, color: '#0958d9', marginTop: 4 }}>{approved}</div>
          </div>
        </Col>
        <Col xs={12} sm={6}>
          <div style={{ background: '#f6ffed', border: '1px solid #b7eb8f', borderRadius: 8, padding: '12px 16px', textAlign: 'center' }}>
            <Text strong style={{ fontSize: 13, color: '#389e0d' }}>Dispensed (month)</Text>
            <div style={{ fontSize: 22, fontWeight: 700, color: '#389e0d', marginTop: 4 }}>{fmtN(litres)} L</div>
          </div>
        </Col>
        <Col xs={12} sm={6}>
          <div style={{ background: '#fff0f6', border: '1px solid #ffadd2', borderRadius: 8, padding: '12px 16px', textAlign: 'center' }}>
            <Text strong style={{ fontSize: 13, color: '#c41d7f' }}>Cost (month)</Text>
            <div style={{ fontSize: 20, fontWeight: 700, color: '#c41d7f', marginTop: 4 }}>{fmtMoney(cost)}</div>
          </div>
        </Col>
      </Row>

      {/* Pending alert */}
      {pending > 0 && (
        <Alert
          type="warning"
          showIcon
          message={`${pending} diesel requisition${pending > 1 ? 's' : ''} awaiting approval`}
          style={{ marginBottom: 16 }}
        />
      )}

      {/* Toolbar */}
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
        <Select
          placeholder="Filter by status"
          allowClear
          style={{ width: 150 }}
          value={statusFilter}
          onChange={v => { setStatusFilter(v); setPage(1); }}
          options={DIESEL_REQUISITION_STATUSES.map(s => ({ value: s, label: s }))}
        />
        <Select
          placeholder="Equipment type"
          allowClear
          style={{ width: 150 }}
          value={equipFilter}
          onChange={v => { setEquipFilter(v); setPage(1); }}
          options={EQUIPMENT_TYPES.map(e => ({ value: e, label: e }))}
        />

        <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <Dropdown
            menu={{
              items: [
                { key: 'excel', label: 'Export Excel', onClick: () => exportDieselRequisitions({ format: 'excel', status: statusFilter }) },
                { key: 'pdf',   label: 'Export PDF',   onClick: () => exportDieselRequisitions({ format: 'pdf',   status: statusFilter }) },
              ],
            }}
          >
            <Button icon={<DownloadOutlined />}>Export</Button>
          </Dropdown>

          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => { createForm.resetFields(); setCreateOpen(true); }}
          >
            New Request
          </Button>
        </div>
      </div>

      {/* Table */}
      <Table<DieselRequisition>
        columns={columns}
        dataSource={data?.items ?? []}
        rowKey="id"
        loading={isFetching}
        size="middle"
        scroll={{ x: 1100 }}
        pagination={{
          current:   page,
          pageSize:  15,
          total:     data?.total ?? 0,
          onChange:  p => setPage(p),
          showTotal: (total, [from, to]) => `${from}–${to} of ${total} requisitions`,
          showSizeChanger: false,
        }}
      />

      {/* ── Create drawer ─────────────────────────────────────────── */}
      <Drawer
        title="New Diesel Requisition"
        width={520}
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        footer={
          <Space style={{ float: 'right' }}>
            <Button onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button
              type="primary"
              loading={createMut.isPending}
              onClick={() =>
                createForm.validateFields().then(v => createMut.mutate(v))
              }
            >
              Submit Requisition
            </Button>
          </Space>
        }
      >
        <Form form={createForm} layout="vertical">
          <Form.Item
            name="purpose"
            label="Purpose / Reason for Request"
            rules={[{ required: true, message: 'Please describe the purpose.' }]}
          >
            <Input.TextArea rows={3} placeholder="e.g. Weekly refuel for CAT 350KVA generator at DR Building" />
          </Form.Item>

          <Form.Item
            name="equipmentType"
            label="Equipment Type"
            rules={[{ required: true }]}
            initialValue="Generator"
          >
            <Select options={EQUIPMENT_TYPES.map(e => ({ value: e, label: e }))} />
          </Form.Item>

          <Form.Item
            name="equipmentReference"
            label="Equipment Reference (Asset No. / Vehicle Reg.)"
            tooltip="Leave blank if not applicable"
          >
            <Input placeholder="e.g. CAT-350KVA-DR or PHC 185 AM" />
          </Form.Item>

          <Form.Item
            name="location"
            label="Location"
            rules={[{ required: true, message: 'Please specify location.' }]}
          >
            <Input placeholder="e.g. DR Building, PHC Office, HQ" />
          </Form.Item>

          <Form.Item
            name="quantityRequestedLitres"
            label="Quantity Requested (Litres)"
            rules={[
              { required: true, type: 'number', min: 1, message: 'Enter a positive quantity.' },
            ]}
          >
            <InputNumber
              min={1}
              style={{ width: '100%' }}
              addonAfter="Litres"
              placeholder="e.g. 400"
            />
          </Form.Item>

          <Form.Item name="notes" label="Notes (optional)">
            <Input.TextArea rows={2} placeholder="Additional context or instructions" />
          </Form.Item>
        </Form>
      </Drawer>

      {/* ── View drawer ───────────────────────────────────────────── */}
      <Drawer
        title={`Requisition ${viewTarget?.requisitionNumber ?? ''}`}
        width={560}
        open={!!viewTarget}
        onClose={() => setViewTarget(null)}
        footer={
          <Button onClick={() => setViewTarget(null)}>Close</Button>
        }
      >
        {viewTarget && (
          <>
            <Row gutter={[12, 12]} style={{ marginBottom: 20 }}>
              <Col span={12}>
                <Statistic title="Status" valueRender={() =>
                  <Tag color={statusColor(viewTarget.status)} style={{ fontSize: 14 }}>
                    {viewTarget.status}
                  </Tag>
                } />
              </Col>
              <Col span={12}>
                <Statistic title="Qty Requested" value={viewTarget.quantityRequestedLitres} suffix="L" />
              </Col>
              {viewTarget.quantityDispensedLitres != null && (
                <Col span={12}>
                  <Statistic title="Qty Dispensed" value={viewTarget.quantityDispensedLitres} suffix="L" />
                </Col>
              )}
              {viewTarget.totalCostNaira != null && (
                <Col span={12}>
                  <Statistic title="Total Cost" prefix="₦" value={viewTarget.totalCostNaira} precision={2} />
                </Col>
              )}
            </Row>

            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Requisition No.">{viewTarget.requisitionNumber}</Descriptions.Item>
              <Descriptions.Item label="Purpose">{viewTarget.purpose}</Descriptions.Item>
              <Descriptions.Item label="Equipment Type">{viewTarget.equipmentType}</Descriptions.Item>
              {viewTarget.equipmentReference && (
                <Descriptions.Item label="Reference">{viewTarget.equipmentReference}</Descriptions.Item>
              )}
              <Descriptions.Item label="Location">{viewTarget.location}</Descriptions.Item>
              <Descriptions.Item label="Department">{viewTarget.department}</Descriptions.Item>
              <Descriptions.Item label="Requested By">{viewTarget.requestedByName}</Descriptions.Item>
              <Descriptions.Item label="Date Submitted">{fmt(viewTarget.createdAt)}</Descriptions.Item>
            </Descriptions>

            {/* Approval section */}
            {viewTarget.approvedByName && (
              <>
                <Divider>Approval</Divider>
                <Descriptions column={1} size="small" bordered>
                  <Descriptions.Item label="Approved By">{viewTarget.approvedByName}</Descriptions.Item>
                  <Descriptions.Item label="Approved At">{fmt(viewTarget.approvedAt)}</Descriptions.Item>
                </Descriptions>
              </>
            )}

            {/* Rejection section */}
            {viewTarget.status === 'Rejected' && (
              <>
                <Divider>Rejection</Divider>
                <Alert
                  type="error"
                  message={`Rejected: ${viewTarget.rejectionReason}`}
                  description={`At ${fmt(viewTarget.rejectedAt)}`}
                />
              </>
            )}

            {/* Dispense section */}
            {viewTarget.status === 'Dispensed' && viewTarget.dispensedAt && (
              <>
                <Divider>Dispense Details</Divider>
                <Descriptions column={2} size="small" bordered>
                  <Descriptions.Item label="Dispensed By">{viewTarget.dispensedByName}</Descriptions.Item>
                  <Descriptions.Item label="Dispensed At">{fmt(viewTarget.dispensedAt)}</Descriptions.Item>
                  <Descriptions.Item label="Qty Dispensed">{fmtN(viewTarget.quantityDispensedLitres)} L</Descriptions.Item>
                  <Descriptions.Item label="Unit Cost">{fmtMoney(viewTarget.unitCostPerLitreNaira)}/L</Descriptions.Item>
                  <Descriptions.Item label="Tank Before">{fmtN(viewTarget.tankLevelBeforeLitres)} L</Descriptions.Item>
                  <Descriptions.Item label="Tank After">{fmtN(viewTarget.tankLevelAfterLitres)} L</Descriptions.Item>
                  <Descriptions.Item label="Total Cost" span={2}>
                    <Text strong style={{ color: '#c41d7f' }}>{fmtMoney(viewTarget.totalCostNaira)}</Text>
                  </Descriptions.Item>
                </Descriptions>
              </>
            )}

            {viewTarget.notes && (
              <>
                <Divider>Notes</Divider>
                <Text type="secondary">{viewTarget.notes}</Text>
              </>
            )}
          </>
        )}
      </Drawer>

      {/* ── Approve modal ─────────────────────────────────────────── */}
      <Modal
        title={`Approve Requisition — ${approveTarget?.requisitionNumber}`}
        open={!!approveTarget}
        onCancel={() => setApproveTarget(null)}
        onOk={() => {
          if (!approveTarget) return;
          approveMut.mutate({ id: approveTarget.id });
        }}
        confirmLoading={approveMut.isPending}
        okText="Approve"
        okButtonProps={{ style: { background: '#52c41a', borderColor: '#52c41a' } }}
      >
        {approveTarget && (
          <Space direction="vertical" style={{ width: '100%' }}>
            <Text>Approving <strong>{approveTarget.requisitionNumber}</strong> for:</Text>
            <Text type="secondary">{approveTarget.purpose}</Text>
            <Descriptions column={2} size="small" bordered style={{ marginTop: 8 }}>
              <Descriptions.Item label="Requested">{fmtN(approveTarget.quantityRequestedLitres)} L</Descriptions.Item>
              <Descriptions.Item label="Location">{approveTarget.location}</Descriptions.Item>
              <Descriptions.Item label="Requested By">{approveTarget.requestedByName}</Descriptions.Item>
              <Descriptions.Item label="Equip. Type">{approveTarget.equipmentType}</Descriptions.Item>
            </Descriptions>
            <Text type="warning">
              Once approved, the Store Officer can proceed to dispense diesel.
            </Text>
          </Space>
        )}
      </Modal>

      {/* ── Reject modal ──────────────────────────────────────────── */}
      <Modal
        title={`Reject Requisition — ${rejectTarget?.requisitionNumber}`}
        open={!!rejectTarget}
        onCancel={() => { setRejectTarget(null); rejectForm.resetFields(); }}
        onOk={() =>
          rejectForm.validateFields().then(v => {
            if (!rejectTarget) return;
            rejectMut.mutate({ id: rejectTarget.id, reason: v.reason });
          })
        }
        confirmLoading={rejectMut.isPending}
        okText="Reject Requisition"
        okButtonProps={{ danger: true }}
      >
        <Form form={rejectForm} layout="vertical">
          {rejectTarget && (
            <Alert
              type="warning"
              showIcon
              message={`${rejectTarget.requisitionNumber} — ${rejectTarget.purpose}`}
              style={{ marginBottom: 12 }}
            />
          )}
          <Form.Item
            name="reason"
            label="Rejection Reason"
            rules={[{ required: true, message: 'Please provide a rejection reason.' }]}
          >
            <Input.TextArea rows={3} placeholder="Explain why this requisition is being rejected" />
          </Form.Item>
        </Form>
      </Modal>

      {/* ── Dispense modal ────────────────────────────────────────── */}
      <Modal
        title={`Dispense Diesel — ${dispenseTarget?.requisitionNumber}`}
        open={!!dispenseTarget}
        width={520}
        onCancel={() => { setDispenseTarget(null); dispenseForm.resetFields(); }}
        onOk={() =>
          dispenseForm.validateFields().then(v => {
            if (!dispenseTarget) return;
            dispenseMut.mutate({ id: dispenseTarget.id, payload: v });
          })
        }
        confirmLoading={dispenseMut.isPending}
        okText="Confirm Dispense"
        okButtonProps={{ type: 'primary' }}
      >
        {dispenseTarget && (
          <>
            <Alert
              type="info"
              showIcon
              message={`${dispenseTarget.requisitionNumber} — ${fmtN(dispenseTarget.quantityRequestedLitres)} L requested`}
              description={`${dispenseTarget.purpose} · ${dispenseTarget.location}`}
              style={{ marginBottom: 16 }}
            />
            <Form form={dispenseForm} layout="vertical">
              <Form.Item
                name="quantityDispensedLitres"
                label="Quantity Dispensed (Litres)"
                initialValue={dispenseTarget.quantityRequestedLitres}
                rules={[{ required: true, type: 'number', min: 0.1, message: 'Enter a positive quantity.' }]}
              >
                <InputNumber min={0.1} style={{ width: '100%' }} addonAfter="L" />
              </Form.Item>

              <Form.Item
                name="tankLevelBeforeLitres"
                label="Tank Level Before Dispensing (Litres)"
                rules={[{ required: true, type: 'number', min: 0, message: 'Enter current tank level.' }]}
              >
                <InputNumber min={0} style={{ width: '100%' }} addonAfter="L" />
              </Form.Item>

              <Form.Item
                name="unitCostPerLitreNaira"
                label="Unit Cost (₦ per Litre)"
                rules={[{ required: true, type: 'number', min: 1, message: 'Enter unit cost.' }]}
              >
                <InputNumber min={1} style={{ width: '100%' }} addonBefore="₦" addonAfter="/L" />
              </Form.Item>

              <Form.Item name="notes" label="Notes (optional)">
                <Input.TextArea rows={2} placeholder="Any additional remarks" />
              </Form.Item>
            </Form>
            <Text type="secondary" style={{ fontSize: 12 }}>
              A diesel record will be automatically created and linked to this requisition.
            </Text>
          </>
        )}
      </Modal>
    </div>
  );
}
