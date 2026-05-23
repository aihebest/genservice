import { useState } from 'react';
import {
  Drawer, Descriptions, Tag, Badge, Button, Space, Divider,
  Modal, Input, Typography, Alert, Tooltip,
} from 'antd';
import {
  CheckOutlined, CloseOutlined, UserAddOutlined, EditOutlined,
} from '@ant-design/icons';
import { requestsApi } from '../../../api/requests.api';
import { CATEGORY_META, STATUS_META, PRIORITY_META } from '../../../types';
import type { ServiceRequest } from '../../../types';
import { useAuthStore } from '../../../store/authStore';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';

dayjs.extend(relativeTime);

const { TextArea } = Input;
const { Text }     = Typography;

// ── Staff list for assignment (demo — would come from API in production) ─────
const STAFF_OPTIONS = [
  { email: 'technician@demo.local', name: 'Chukwudi Nwosu',  role: 'Technician' },
  { email: 'tech2@demo.local',      name: 'Grace Obi',        role: 'Technician' },
  { email: 'driver@demo.local',     name: 'Bola Adeyemi',     role: 'Driver'     },
  { email: 'driver2@demo.local',    name: 'Kwame Asante',     role: 'Driver'     },
];

interface Props {
  request:  ServiceRequest | null;
  open:     boolean;
  onClose:  () => void;
  onUpdate: () => void;
}

export default function RequestDetailDrawer({ request, open, onClose, onUpdate }: Props) {
  const role = useAuthStore(s => s.user?.role);
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState<string | null>(null);
  const [rejectNote, setRejectNote] = useState('');
  const [rejectOpen, setRejectOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [assignee,   setAssignee]   = useState('');

  if (!request) return null;

  const isApprover = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';
  const isTech     = role === 'Technician';
  const catMeta    = CATEGORY_META[request.category];
  const statusMeta = STATUS_META[request.status];
  const prioMeta   = PRIORITY_META[request.priority];

  const act = async (fn: () => Promise<unknown>) => {
    setLoading(true); setError(null);
    try { await fn(); onUpdate(); }
    catch (e: unknown) {
      setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Action failed.');
    } finally { setLoading(false); }
  };

  const doApprove = () => act(() => requestsApi.approve(request.id));

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

  const doMarkInProgress = () => act(() => requestsApi.updateStatus(request.id, 'InProgress'));
  const doMarkComplete   = () => act(() => requestsApi.updateStatus(request.id, 'Completed'));

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
        width={540}
        extra={
          <Space>
            {/* Approver actions */}
            {isApprover && request.status === 'PendingApproval' && (
              <>
                <Button
                  type="primary" icon={<CheckOutlined />} size="small"
                  loading={loading} onClick={doApprove}
                >Approve</Button>
                <Button
                  danger icon={<CloseOutlined />} size="small"
                  onClick={() => setRejectOpen(true)}
                >Reject</Button>
              </>
            )}
            {/* Assignment */}
            {isApprover && (request.status === 'Open' || request.status === 'Approved') && (
              <Button icon={<UserAddOutlined />} size="small"
                onClick={() => setAssignOpen(true)}>
                Assign
              </Button>
            )}
            {/* Technician status updates */}
            {isTech && request.status === 'InProgress' && (
              <Button type="primary" size="small" loading={loading}
                onClick={doMarkComplete} icon={<CheckOutlined />}>
                Mark Complete
              </Button>
            )}
            {isTech && (request.status === 'Open' || request.status === 'Approved') && (
              <Button size="small" loading={loading}
                onClick={doMarkInProgress} icon={<EditOutlined />}>
                Start Working
              </Button>
            )}
          </Space>
        }
      >
        {error && (
          <Alert message={error} type="error" showIcon closable
            onClose={() => setError(null)} style={{ marginBottom: 16 }} />
        )}

        {/* Status + priority badges */}
        <Space style={{ marginBottom: 16 }}>
          <Badge status={statusMeta.color as any} text={
            <Text strong>{statusMeta.label}</Text>
          } />
          <Tag color={prioMeta.color}>{prioMeta.label} Priority</Tag>
          <Tag color={catMeta.color}>{catMeta.label}</Tag>
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
            {request.requestedByName} <Text type="secondary">({request.requestedByEmail})</Text>
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
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>
              Assignment
            </Divider>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label="Assigned To">
                {request.assignedToName} <Text type="secondary">({request.assignedToEmail})</Text>
              </Descriptions.Item>
            </Descriptions>
          </>
        )}

        {/* Approval info */}
        {(request.approvedByName || request.rejectionReason) && (
          <>
            <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>
              {request.status === 'Rejected' ? 'Rejection Details' : 'Approval Details'}
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
      </Drawer>

      {/* Reject modal */}
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
          Please provide a reason for rejecting <strong>{request.ticketNumber}</strong>.
        </p>
        <TextArea
          rows={3}
          value={rejectNote}
          onChange={e => setRejectNote(e.target.value)}
          placeholder="Explain why this request is being rejected…"
          maxLength={1000}
          showCount
        />
      </Modal>

      {/* Assign modal */}
      <Modal
        title="Assign Request"
        open={assignOpen}
        onOk={doAssign}
        onCancel={() => { setAssignOpen(false); setAssignee(''); }}
        okText="Assign"
        okButtonProps={{ disabled: !assignee }}
        confirmLoading={loading}
      >
        <p style={{ marginBottom: 12 }}>
          Assign <strong>{request.ticketNumber}</strong> to a staff member:
        </p>
        <Space direction="vertical" style={{ width: '100%' }}>
          {STAFF_OPTIONS.map(s => (
            <Button
              key={s.email}
              block
              type={assignee === s.email ? 'primary' : 'default'}
              onClick={() => setAssignee(s.email)}
            >
              {s.name} — <Text type="secondary" style={{ fontSize: 12 }}>{s.role}</Text>
            </Button>
          ))}
        </Space>
      </Modal>
    </>
  );
}
