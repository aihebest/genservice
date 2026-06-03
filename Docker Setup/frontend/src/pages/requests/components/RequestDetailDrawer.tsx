import { useState } from 'react';
import {
  Drawer, Descriptions, Tag, Badge, Button, Space, Divider,
  Modal, Input, Select, Typography, Alert, Tooltip,
} from 'antd';
import {
  CheckOutlined, CloseOutlined, UserAddOutlined, EditOutlined,
  SwapOutlined, HourglassOutlined, DollarOutlined,
} from '@ant-design/icons';
import { requestsApi } from '../../../api/requests.api';
import { CATEGORY_META, STATUS_META, PRIORITY_META, REASSIGN_TYPES } from '../../../types';
import ProgressLogSection   from '../../../components/shared/ProgressLogSection';
import AuditHistorySection from '../../../components/shared/AuditHistorySection';
import type { ServiceRequest } from '../../../types';
import { useAuthStore } from '../../../store/authStore';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';

dayjs.extend(relativeTime);

const { TextArea } = Input;
const { Text }     = Typography;

// ── Staff list for assignment (demo) ──────────────────────────────────────────
const STAFF_OPTIONS = [
  { email: 'technician@demo.local', name: 'Chukwudi Nwosu', role: 'Technician' },
  { email: 'tech2@demo.local',      name: 'Grace Obi',       role: 'Technician' },
  { email: 'driver@demo.local',     name: 'Bola Adeyemi',    role: 'Driver'     },
  { email: 'driver2@demo.local',    name: 'Kwame Asante',    role: 'Driver'     },
];

interface Props {
  request:  ServiceRequest | null;
  open:     boolean;
  onClose:  () => void;
  onUpdate: () => void;
}

export default function RequestDetailDrawer({ request, open, onClose, onUpdate }: Props) {
  const role = useAuthStore(s => s.user?.role);

  const [loading,        setLoading]        = useState(false);
  const [error,          setError]          = useState<string | null>(null);

  // Reject modal
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectNote, setRejectNote] = useState('');

  // Assign modal
  const [assignOpen, setAssignOpen] = useState(false);
  const [assignee,   setAssignee]   = useState('');

  // Reassign modal
  const [reassignOpen,     setReassignOpen]     = useState(false);
  const [reassignType,     setReassignType]     = useState('');
  const [reassignToName,   setReassignToName]   = useState('');
  const [reassignNotes,    setReassignNotes]    = useState('');

  if (!request) return null;

  const isApprover = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';
  const isTech     = role === 'Technician';
  const catMeta    = CATEGORY_META[request.category];
  const statusMeta = STATUS_META[request.status] ?? { label: request.status, color: 'default' };
  const prioMeta   = PRIORITY_META[request.priority];

  const act = async (fn: () => Promise<unknown>) => {
    setLoading(true); setError(null);
    try { await fn(); onUpdate(); }
    catch (e: unknown) {
      setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Action failed.');
    } finally { setLoading(false); }
  };

  const doLineApprove     = () => act(() => requestsApi.lineApprove(request.id));
  const doApprove         = () => act(() => requestsApi.approve(request.id));
  const doMarkInProgress  = () => act(() => requestsApi.updateStatus(request.id, 'InProgress'));
  const doMarkMaterialAwaited = () => act(() => requestsApi.updateStatus(request.id, 'MaterialAwaited'));
  const doMarkComplete    = () => act(() => requestsApi.updateStatus(request.id, 'Completed'));

  const doReject = async () => {
    if (!rejectNote.trim()) return;
    await act(() => requestsApi.reject(request.id, rejectNote));
    setRejectOpen(false); setRejectNote('');
  };

  const doAssign = async () => {
    const staff = STAFF_OPTIONS.find(s => s.email === assignee);
    if (!staff) return;
    await act(() => requestsApi.assign(request.id, staff.email, staff.name));
    setAssignOpen(false); setAssignee('');
  };

  const doReassign = async () => {
    if (!reassignType || !reassignToName.trim()) return;
    await act(() => requestsApi.reassign(request.id, reassignType, reassignToName, reassignNotes || undefined));
    setReassignOpen(false); setReassignType(''); setReassignToName(''); setReassignNotes('');
  };

  // Which actions are available
  const canLineApprove = isApprover && request.status === 'PendingLineManager';
  const canApprove     = isApprover && request.status === 'PendingApproval';
  const canAssign      = isApprover && (request.status === 'Open' || request.status === 'Approved');
  const canReassign   = isApprover && !['Completed', 'Cancelled', 'Rejected'].includes(request.status);
  const canStart      = isTech && (request.status === 'Open' || request.status === 'Approved' || request.status === 'InProgress');
  const canMaterialAwaited = isApprover && request.status === 'InProgress';
  const canAwaitingFunds   = isApprover && request.status === 'InProgress';
  const canComplete   = (isTech || isApprover) && (request.status === 'InProgress' || request.status === 'MaterialAwaited');

  return (
    <>
      <Drawer
        title={
          <Space>
            <Tag>{request.ticketNumber}</Tag>
            <span style={{ fontWeight: 600, fontSize: 14 }}>{request.title}</span>
          </Space>
        }
        open={open}
        onClose={onClose}
        width={560}
        extra={
          <Space wrap>
            {canLineApprove && (
              <>
                <Button type="primary" icon={<CheckOutlined />} size="small"
                  loading={loading} onClick={doLineApprove}>LM Approve</Button>
                <Button danger icon={<CloseOutlined />} size="small"
                  onClick={() => setRejectOpen(true)}>LM Reject</Button>
              </>
            )}
            {canApprove && (
              <>
                <Button type="primary" icon={<CheckOutlined />} size="small"
                  loading={loading} onClick={doApprove}>GS Approve</Button>
                <Button danger icon={<CloseOutlined />} size="small"
                  onClick={() => setRejectOpen(true)}>GS Reject</Button>
              </>
            )}
            {canAssign && (
              <Button icon={<UserAddOutlined />} size="small"
                onClick={() => setAssignOpen(true)}>Assign</Button>
            )}
            {canReassign && (
              <Button icon={<SwapOutlined />} size="small"
                onClick={() => setReassignOpen(true)}>Reassign</Button>
            )}
            {canMaterialAwaited && (
              <Button icon={<HourglassOutlined />} size="small"
                loading={loading} onClick={doMarkMaterialAwaited}>Awaiting Spares</Button>
            )}
            {canAwaitingFunds && (
              <Button icon={<DollarOutlined />} size="small"
                loading={loading}
                onClick={() => act(() => requestsApi.updateStatus(request.id, 'AwaitingFunds'))}>
                Awaiting Funds
              </Button>
            )}
            {canStart && request.status !== 'InProgress' && (
              <Button size="small" loading={loading}
                onClick={doMarkInProgress} icon={<EditOutlined />}>Start Working</Button>
            )}
            {canComplete && (
              <Button type="primary" size="small" loading={loading}
                onClick={doMarkComplete} icon={<CheckOutlined />}>Mark Complete</Button>
            )}
          </Space>
        }
      >
        {error && (
          <Alert message={error} type="error" showIcon closable
            onClose={() => setError(null)} style={{ marginBottom: 16 }} />
        )}

        {/* Status + badges */}
        <Space wrap style={{ marginBottom: 16 }}>
          <Badge status={statusMeta.color as any} text={<Text strong>{statusMeta.label}</Text>} />
          <Tag color={prioMeta.color}>{prioMeta.label} Priority</Tag>
          <Tag color={catMeta?.color}>{catMeta?.label ?? request.category}</Tag>
          {request.requiresApproval && <Tag color="volcano">Approval Required</Tag>}
        </Space>

        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label="Ticket #">{request.ticketNumber}</Descriptions.Item>
          <Descriptions.Item label="Title">{request.title}</Descriptions.Item>
          <Descriptions.Item label="Description">
            <Text style={{ whiteSpace: 'pre-wrap' }}>{request.description}</Text>
          </Descriptions.Item>
          <Descriptions.Item label="Location">{request.location || '—'}</Descriptions.Item>
          <Descriptions.Item label="Raised By">
            {request.requestedByName}{' '}
            <Text type="secondary">({request.requestedByEmail})</Text>
          </Descriptions.Item>
          <Descriptions.Item label="Submitted">
            <Tooltip title={dayjs(request.createdAt).format('D MMM YYYY, HH:mm')}>
              {dayjs(request.createdAt).fromNow()}
            </Tooltip>
          </Descriptions.Item>
        </Descriptions>

        {/* Assignment info */}
        {(request.assignedToName || request.assignedToEmail) && (
          <>
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Assignment</Divider>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Assigned To">
                {request.assignedToName}{' '}
                <Text type="secondary">({request.assignedToEmail})</Text>
              </Descriptions.Item>
            </Descriptions>
          </>
        )}

        {/* Reassignment info */}
        {request.reassignedToName && (
          <>
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Reassignment</Divider>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Reassigned To">
                <Tag color="purple">{request.reassignedToType}</Tag> {request.reassignedToName}
              </Descriptions.Item>
              {request.reassignedAt && (
                <Descriptions.Item label="Date">
                  {dayjs(request.reassignedAt).format('D MMM YYYY, HH:mm')}
                </Descriptions.Item>
              )}
              {request.reassignedNotes && (
                <Descriptions.Item label="Notes">{request.reassignedNotes}</Descriptions.Item>
              )}
            </Descriptions>
          </>
        )}

        {/* Line Manager approval trail */}
        {request.lineManagerName && (
          <>
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Line Manager Review</Divider>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Approved By">{request.lineManagerName}</Descriptions.Item>
              {request.lineManagerApprovedAt && (
                <Descriptions.Item label="Date">
                  {dayjs(request.lineManagerApprovedAt).format('D MMM YYYY, HH:mm')}
                </Descriptions.Item>
              )}
            </Descriptions>
          </>
        )}

        {/* GS Approval / Rejection info */}
        {(request.approvedByName || request.rejectionReason) && (
          <>
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>
              {request.status === 'Rejected' ? 'Rejection Details' : 'GS Approval'}
            </Divider>
            <Descriptions column={1} size="small" bordered>
              {request.approvedByName && (
                <Descriptions.Item label={request.status === 'Rejected' ? 'Rejected By' : 'Approved By'}>
                  {request.approvedByName}
                </Descriptions.Item>
              )}
              {request.approvedAt && (
                <Descriptions.Item label="Date">
                  {dayjs(request.approvedAt).format('D MMM YYYY, HH:mm')}
                </Descriptions.Item>
              )}
              {request.rejectionReason && (
                <Descriptions.Item label="Reason">
                  <Text type="danger">{request.rejectionReason}</Text>
                </Descriptions.Item>
              )}
            </Descriptions>
          </>
        )}

        {request.notes && (
          <>
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Notes</Divider>
            <Text type="secondary" style={{ fontSize: 13 }}>{request.notes}</Text>
          </>
        )}

        {/* Daily progress log */}
        <ProgressLogSection
          module="Requests"
          entityId={request.id}
          refNumber={request.ticketNumber}
          taskTitle={request.title}
        />

        {/* Audit trail */}
        <AuditHistorySection entityType="Request" entityId={request.id} />
      </Drawer>

      {/* ── Reject modal ──────────────────────────────────────────────────── */}
      <Modal
        title="Reject Request"
        open={rejectOpen}
        onOk={doReject}
        onCancel={() => { setRejectOpen(false); setRejectNote(''); }}
        okText="Confirm Rejection"
        okButtonProps={{ danger: true, disabled: !rejectNote.trim() }}
        confirmLoading={loading}
      >
        <p style={{ marginBottom: 12 }}>
          Provide a reason for rejecting <strong>{request.ticketNumber}</strong>:
        </p>
        <TextArea rows={3} value={rejectNote} onChange={e => setRejectNote(e.target.value)}
          placeholder="Explain why this request is being rejected…" maxLength={1000} showCount />
      </Modal>

      {/* ── Assign modal ──────────────────────────────────────────────────── */}
      <Modal
        title="Assign to Staff Member"
        open={assignOpen}
        onOk={doAssign}
        onCancel={() => { setAssignOpen(false); setAssignee(''); }}
        okText="Assign"
        okButtonProps={{ disabled: !assignee }}
        confirmLoading={loading}
      >
        <p style={{ marginBottom: 12 }}>
          Assign <strong>{request.ticketNumber}</strong> to:
        </p>
        <Space direction="vertical" style={{ width: '100%' }}>
          {STAFF_OPTIONS.map(s => (
            <Button key={s.email} block
              type={assignee === s.email ? 'primary' : 'default'}
              onClick={() => setAssignee(s.email)}>
              {s.name} — <Text type="secondary" style={{ fontSize: 12 }}>{s.role}</Text>
            </Button>
          ))}
        </Space>
      </Modal>

      {/* ── Reassign modal ────────────────────────────────────────────────── */}
      <Modal
        title="Reassign Request"
        open={reassignOpen}
        onOk={doReassign}
        onCancel={() => {
          setReassignOpen(false);
          setReassignType(''); setReassignToName(''); setReassignNotes('');
        }}
        okText="Confirm Reassignment"
        okButtonProps={{ disabled: !reassignType || !reassignToName.trim() }}
        confirmLoading={loading}
        width={480}
      >
        <p style={{ marginBottom: 16 }}>
          Reassign <strong>{request.ticketNumber}</strong> to an external team or vendor:
        </p>
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <div>
            <Text strong style={{ display: 'block', marginBottom: 6 }}>Reassign To</Text>
            <Select
              style={{ width: '100%' }}
              placeholder="Select department / team type…"
              value={reassignType || undefined}
              onChange={setReassignType}
              options={REASSIGN_TYPES.map(t => ({ value: t.value, label: t.label }))}
            />
          </div>
          <div>
            <Text strong style={{ display: 'block', marginBottom: 6 }}>Name / Team / Vendor</Text>
            <Input
              placeholder="e.g. Apex Logistics, ABC Vendors, Procurement Dept…"
              value={reassignToName}
              onChange={e => setReassignToName(e.target.value)}
              maxLength={200}
            />
          </div>
          <div>
            <Text strong style={{ display: 'block', marginBottom: 6 }}>Notes (optional)</Text>
            <TextArea
              rows={3}
              placeholder="Reason for reassignment or instructions…"
              value={reassignNotes}
              onChange={e => setReassignNotes(e.target.value)}
              maxLength={2000}
            />
          </div>
        </Space>
      </Modal>
    </>
  );
}
