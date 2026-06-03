import { useState, useCallback } from 'react';
import {
  Alert, Badge, Button, Card, Col, Descriptions, Divider, Drawer,
  Form, Input, Modal, Row, Select, Space, Statistic, Table, Tag, Tooltip, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, ReloadOutlined, CheckOutlined, CloseOutlined,
  CarOutlined, ToolOutlined, WarningOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { vehicleMaintenanceApi } from '../../api/vehicleMaintenance.api';
import ProgressLogSection from '../../components/shared/ProgressLogSection';
import { VM_STATUS_META, VM_TYPE_META, PRIORITY_META, OFFICE_LOCATIONS } from '../../types';
import type { VehicleMaintenance, VehicleMaintenanceStatus, RequestPriority } from '../../types';
import { useAuthStore } from '../../store/authStore';

dayjs.extend(relativeTime);

const { Title, Text } = Typography;
const { TextArea }    = Input;

const STATUS_TABS = [
  { key: '',           label: 'All'         },
  { key: 'Pending',    label: 'Pending'     },
  { key: 'Approved',   label: 'Approved'    },
  { key: 'InWorkshop', label: 'In Workshop' },
  { key: 'Completed',  label: 'Completed'   },
  { key: 'Rejected',   label: 'Rejected'    },
];

const VM_TYPES = ['Servicing','Repair','Inspection','Bodywork','TyreChange','Battery','Other'];

// ── Table columns ─────────────────────────────────────────────────────────────
function buildColumns(onView: (r: VehicleMaintenance) => void): ColumnsType<VehicleMaintenance> {
  return [
    {
      title: 'Ref #', dataIndex: 'requestNumber', key: 'requestNumber', width: 120,
      render: (v: string) => <Text code style={{ fontSize: 12 }}>{v}</Text>,
    },
    {
      title: 'Vehicle Reg', dataIndex: 'vehicleRegNo', key: 'vehicleRegNo', width: 120,
      render: (v: string) => <Tag icon={<CarOutlined />} color="blue">{v}</Tag>,
    },
    {
      title: 'Vehicle Type', dataIndex: 'vehicleType', key: 'vehicleType', width: 150, ellipsis: true,
    },
    {
      title: 'Maintenance', dataIndex: 'maintenanceType', key: 'maintenanceType', width: 130,
      render: (v: string) => {
        const m = VM_TYPE_META[v as keyof typeof VM_TYPE_META];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Status', dataIndex: 'status', key: 'status', width: 130,
      render: (v: VehicleMaintenanceStatus) => {
        const m = VM_STATUS_META[v];
        return <Badge status={m?.badge as any} text={m?.label ?? v} />;
      },
    },
    {
      title: 'Priority', dataIndex: 'priority', key: 'priority', width: 90,
      render: (v: RequestPriority) => {
        const m = PRIORITY_META[v];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Location', dataIndex: 'currentLocation', key: 'currentLocation', width: 150, ellipsis: true,
    },
    {
      title: 'Days Open', dataIndex: 'daysOpen', key: 'daysOpen', width: 95,
      render: (v: number, r) => {
        const isLong = r.status === 'InWorkshop' && (r.daysInWorkshop ?? 0) > 7;
        return (
          <Text type={isLong ? 'danger' : v > 14 ? 'warning' : 'secondary'} style={{ fontSize: 13 }}>
            {isLong && <WarningOutlined style={{ marginRight: 4 }} />}{v}d
          </Text>
        );
      },
    },
    {
      title: 'Raised By', dataIndex: 'requestedByName', key: 'requestedByName', width: 140, ellipsis: true,
      render: (v: string) => <Text style={{ fontSize: 13 }}>{v}</Text>,
    },
    {
      title: 'Workshop', dataIndex: 'workshopName', key: 'workshopName', width: 150, ellipsis: true,
      render: (v?: string) => v
        ? <Text style={{ fontSize: 13 }}>{v}</Text>
        : <Text type="secondary" style={{ fontSize: 12 }}>Not dispatched</Text>,
    },
    {
      title: '', key: 'action', width: 65,
      render: (_: unknown, r: VehicleMaintenance) => (
        <Button size="small" onClick={() => onView(r)}>View</Button>
      ),
    },
  ];
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function FleetPage() {
  const role = useAuthStore(s => s.user?.role);
  const qc   = useQueryClient();

  const [activeStatus, setActiveStatus] = useState('');
  const [search,       setSearch]       = useState('');
  const [page,         setPage]         = useState(1);
  const [createOpen,   setCreateOpen]   = useState(false);
  const [selected,     setSelected]     = useState<VehicleMaintenance | null>(null);
  const [drawerOpen,   setDrawerOpen]   = useState(false);

  const [rejectOpen,    setRejectOpen]    = useState(false);
  const [rejectReason,  setRejectReason]  = useState('');
  const [dispatchOpen,  setDispatchOpen]  = useState(false);
  const [workshopName,  setWorkshopName]  = useState('');
  const [workshopLoc,   setWorkshopLoc]   = useState('');
  const [completeOpen,  setCompleteOpen]  = useState(false);
  const [completeNotes, setCompleteNotes] = useState('');
  const [actionLoading, setActionLoading] = useState(false);
  const [actionError,   setActionError]   = useState<string | null>(null);

  const [createForm]    = Form.useForm();
  const [createLoading, setCreateLoading] = useState(false);
  const [createError,   setCreateError]   = useState<string | null>(null);
  const [locationSel,   setLocationSel]   = useState<string | null>(null);

  const isApprover = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';

  const refresh = useCallback(() => { qc.invalidateQueries({ queryKey: ['vm'] }); }, [qc]);

  const { data, isFetching } = useQuery({
    queryKey: ['vm', 'list', activeStatus, search, page],
    queryFn: () => vehicleMaintenanceApi.list({ status: activeStatus || undefined, search: search || undefined, page, pageSize: 15 }),
  });

  const { data: stats } = useQuery({
    queryKey: ['vm', 'stats'],
    queryFn:  vehicleMaintenanceApi.stats,
    refetchInterval: 30_000,
  });

  const openDetail = (r: VehicleMaintenance) => { setSelected(r); setDrawerOpen(true); setActionError(null); };

  const act = async (fn: () => Promise<unknown>) => {
    setActionLoading(true); setActionError(null);
    try {
      await fn(); refresh();
      if (selected) vehicleMaintenanceApi.getById(selected.id).then(setSelected).catch(() => {});
    } catch (e: unknown) {
      setActionError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Action failed.');
    } finally { setActionLoading(false); }
  };

  const handleCreate = async (values: Record<string, string>) => {
    setCreateLoading(true); setCreateError(null);
    try {
      const location = values.locationSelect === 'Other' ? (values.locationOther?.trim() ?? 'Other') : values.locationSelect;
      await vehicleMaintenanceApi.create({
        vehicleRegNo: values.vehicleRegNo.toUpperCase(), vehicleType: values.vehicleType,
        maintenanceType: values.maintenanceType, description: values.description,
        priority: values.priority, currentLocation: location,
      });
      createForm.resetFields(); setLocationSel(null); setCreateOpen(false); refresh();
    } catch (e: unknown) {
      setCreateError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to submit.');
    } finally { setCreateLoading(false); }
  };

  return (
    <div>
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>Vehicle Maintenance Tracker</Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            Track vehicle repair requests from Logistics through to workshop completion
          </Text>
        </Col>
        <Col>
          <Space>
            <Tooltip title="Refresh"><Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} /></Tooltip>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>New Request</Button>
          </Space>
        </Col>
      </Row>

      {(stats?.longStanding ?? 0) > 0 && (
        <Alert type="warning" showIcon icon={<WarningOutlined />} style={{ marginBottom: 16 }}
          message={
            <Text><strong>{stats!.longStanding} vehicle{stats!.longStanding > 1 ? 's' : ''}</strong>{' '}
            ha{stats!.longStanding > 1 ? 've' : 's'} been in the workshop for more than 7 days. Follow up required.</Text>
          }
          action={<Button size="small" onClick={() => setActiveStatus('InWorkshop')}>View</Button>}
        />
      )}

      <Row gutter={16} style={{ marginBottom: 20 }}>
        {[
          { label: 'Pending',         value: stats?.pending,            color: '#fa8c16', key: 'Pending'    },
          { label: 'Approved',        value: stats?.approved,           color: '#1677ff', key: 'Approved'   },
          { label: 'In Workshop',     value: stats?.inWorkshop,         color: '#722ed1', key: 'InWorkshop' },
          { label: 'Long-Standing',   value: stats?.longStanding,       color: '#f5222d', key: 'InWorkshop' },
          { label: 'Done This Month', value: stats?.completedThisMonth, color: '#52c41a', key: 'Completed'  },
        ].map(s => (
          <Col key={s.label} style={{ flex: '1 1 150px', minWidth: 140, marginBottom: 8 }}>
            <Card hoverable size="small" onClick={() => setActiveStatus(s.key)}
              styles={{ body: { padding: '14px 18px' } }}>
              <Statistic title={<Text style={{ fontSize: 12 }}>{s.label}</Text>}
                value={s.value ?? 0} valueStyle={{ color: s.color, fontSize: 26, fontWeight: 700 }} />
            </Card>
          </Col>
        ))}
      </Row>

      <Card styles={{ body: { padding: 0 } }}>
        <div style={{ padding: '0 24px', borderBottom: '1px solid #f0f0f0', display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {STATUS_TABS.map(t => (
            <Button key={t.key} type={activeStatus === t.key ? 'primary' : 'text'} size="small"
              style={{ margin: '8px 2px' }} onClick={() => { setActiveStatus(t.key); setPage(1); }}>
              {t.label}
            </Button>
          ))}
        </div>
        <div style={{ padding: '12px 24px', borderBottom: '1px solid #f0f0f0' }}>
          <Input.Search placeholder="Search by reg number, vehicle type, description…" style={{ width: 340 }}
            allowClear onSearch={v => { setSearch(v); setPage(1); }} onChange={e => !e.target.value && setSearch('')} />
        </div>
        <Table<VehicleMaintenance>
          columns={buildColumns(openDetail)} dataSource={data?.items ?? []} rowKey="id" loading={isFetching}
          pagination={{ current: page, pageSize: 15, total: data?.totalCount ?? 0, onChange: p => setPage(p),
            showTotal: (t, [f, to]) => `${f}–${to} of ${t} requests`, showSizeChanger: false }}
          onRow={r => ({ onClick: () => openDetail(r), style: { cursor: 'pointer' } })}
          size="middle" style={{ padding: '0 8px' }} scroll={{ x: 1100 }}
        />
      </Card>

      {/* ── Create modal ──────────────────────────────────────────────── */}
      <Modal title={<><CarOutlined /> New Vehicle Maintenance Request</>}
        open={createOpen}
        onOk={() => createForm.submit()}
        onCancel={() => { setCreateOpen(false); createForm.resetFields(); setLocationSel(null); setCreateError(null); }}
        okText="Submit Request" confirmLoading={createLoading} width={540} destroyOnClose>
        {createError && <Alert message={createError} type="error" showIcon style={{ marginBottom: 12 }} />}
        <Form form={createForm} layout="vertical" onFinish={handleCreate}>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="vehicleRegNo" label="Vehicle Reg Number"
                rules={[{ required: true, message: 'Enter registration number' }]}>
                <Input placeholder="e.g. LAG-342-TE" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="vehicleType" label="Vehicle Type"
                rules={[{ required: true, message: 'Enter vehicle type' }]}>
                <Input placeholder="e.g. Toyota Hilux" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="maintenanceType" label="Maintenance Type" rules={[{ required: true }]}>
                <Select placeholder="Select…"
                  options={VM_TYPES.map(t => ({ value: t, label: VM_TYPE_META[t as keyof typeof VM_TYPE_META]?.label ?? t }))} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="priority" label="Priority" initialValue="Normal">
                <Select options={Object.entries(PRIORITY_META).map(([k, m]) => ({
                  value: k, label: <Tag color={m.color}>{m.label}</Tag> }))} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="description" label="Fault / Work Description" rules={[{ required: true }]}>
            <TextArea rows={3} placeholder="Describe the fault, symptoms, or work required…" maxLength={2000} showCount />
          </Form.Item>
          <Form.Item name="locationSelect" label="Current Vehicle Location" rules={[{ required: true }]}>
            <Select placeholder="Where is the vehicle now?"
              onChange={(v: string) => setLocationSel(v)}
              options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
          </Form.Item>
          {locationSel === 'Other' && (
            <Form.Item name="locationOther" label="Specify Location" rules={[{ required: true }]}>
              <Input placeholder="Enter specific location…" />
            </Form.Item>
          )}
        </Form>
      </Modal>

      {/* ── Detail drawer ─────────────────────────────────────────────── */}
      {selected && (
        <Drawer
          title={
            <Space>
              <Tag icon={<CarOutlined />} color="blue">{selected.vehicleRegNo}</Tag>
              <span style={{ fontWeight: 600 }}>{selected.vehicleType}</span>
            </Space>
          }
          open={drawerOpen} onClose={() => setDrawerOpen(false)} width={540}
          extra={
            <Space wrap>
              {isApprover && selected.status === 'Pending' && (
                <>
                  <Button type="primary" size="small" icon={<CheckOutlined />} loading={actionLoading}
                    onClick={() => act(() => vehicleMaintenanceApi.approve(selected.id))}>Approve</Button>
                  <Button danger size="small" icon={<CloseOutlined />} onClick={() => setRejectOpen(true)}>Reject</Button>
                </>
              )}
              {isApprover && selected.status === 'Approved' && (
                <Button type="primary" size="small" icon={<ToolOutlined />}
                  onClick={() => setDispatchOpen(true)}>Send to Workshop</Button>
              )}
              {isApprover && selected.status === 'InWorkshop' && (
                <Button type="primary" size="small" icon={<CheckOutlined />}
                  onClick={() => setCompleteOpen(true)}>Mark Complete</Button>
              )}
            </Space>
          }
        >
          {actionError && (
            <Alert message={actionError} type="error" showIcon closable
              onClose={() => setActionError(null)} style={{ marginBottom: 12 }} />
          )}
          <Space wrap style={{ marginBottom: 16 }}>
            {(() => { const m = VM_STATUS_META[selected.status]; return <Badge status={m?.badge as any} text={<Text strong>{m?.label}</Text>} />; })()}
            {(() => { const m = VM_TYPE_META[selected.maintenanceType as keyof typeof VM_TYPE_META]; return <Tag color={m?.color}>{m?.label ?? selected.maintenanceType}</Tag>; })()}
            {(() => { const m = PRIORITY_META[selected.priority]; return <Tag color={m?.color}>{m?.label} Priority</Tag>; })()}
            {selected.status === 'InWorkshop' && (selected.daysInWorkshop ?? 0) > 7 && (
              <Tag color="red" icon={<WarningOutlined />}>Long-standing ({selected.daysInWorkshop}d)</Tag>
            )}
          </Space>

          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Request #">{selected.requestNumber}</Descriptions.Item>
            <Descriptions.Item label="Vehicle Reg">{selected.vehicleRegNo}</Descriptions.Item>
            <Descriptions.Item label="Vehicle Type">{selected.vehicleType}</Descriptions.Item>
            <Descriptions.Item label="Description">
              <Text style={{ whiteSpace: 'pre-wrap' }}>{selected.description}</Text>
            </Descriptions.Item>
            <Descriptions.Item label="Current Location">{selected.currentLocation}</Descriptions.Item>
            <Descriptions.Item label="Raised By">
              {selected.requestedByName} <Text type="secondary">({selected.requestedByEmail})</Text>
            </Descriptions.Item>
            <Descriptions.Item label="Raised">
              <Tooltip title={dayjs(selected.createdAt).format('D MMM YYYY, HH:mm')}>
                {dayjs(selected.createdAt).fromNow()} ({selected.daysOpen}d ago)
              </Tooltip>
            </Descriptions.Item>
          </Descriptions>

          {selected.workshopName && (
            <>
              <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Workshop</Divider>
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label="Workshop">{selected.workshopName}</Descriptions.Item>
                {selected.workshopLocation && <Descriptions.Item label="Location">{selected.workshopLocation}</Descriptions.Item>}
                {selected.sentToWorkshopAt && (
                  <Descriptions.Item label="Dispatched">
                    {dayjs(selected.sentToWorkshopAt).format('D MMM YYYY')}
                    {selected.daysInWorkshop !== undefined && <Text type="secondary"> ({selected.daysInWorkshop}d in workshop)</Text>}
                  </Descriptions.Item>
                )}
              </Descriptions>
            </>
          )}

          {selected.approvedByName && (
            <>
              <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>
                {selected.status === 'Rejected' ? 'Rejection' : 'Approval'}
              </Divider>
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label={selected.status === 'Rejected' ? 'Rejected By' : 'Approved By'}>
                  {selected.approvedByName}
                </Descriptions.Item>
                {selected.approvedAt && <Descriptions.Item label="Date">{dayjs(selected.approvedAt).format('D MMM YYYY, HH:mm')}</Descriptions.Item>}
                {selected.rejectionReason && <Descriptions.Item label="Reason"><Text type="danger">{selected.rejectionReason}</Text></Descriptions.Item>}
              </Descriptions>
            </>
          )}

          {selected.completedAt && (
            <>
              <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>Completion</Divider>
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label="Completed">{dayjs(selected.completedAt).format('D MMM YYYY, HH:mm')}</Descriptions.Item>
                {selected.notes && <Descriptions.Item label="Notes">{selected.notes}</Descriptions.Item>}
              </Descriptions>
            </>
          )}

          <ProgressLogSection
            module="Vehicle"
            entityId={selected.id}
            refNumber={selected.requestNumber}
            taskTitle={`${selected.vehicleRegNo} – ${selected.vehicleType}`}
          />
        </Drawer>
      )}

      {/* Reject modal */}
      <Modal title="Reject Request" open={rejectOpen}
        onOk={async () => { if (!rejectReason.trim() || !selected) return; await act(() => vehicleMaintenanceApi.reject(selected.id, rejectReason)); setRejectOpen(false); setRejectReason(''); }}
        onCancel={() => { setRejectOpen(false); setRejectReason(''); }}
        okText="Confirm Rejection" okButtonProps={{ danger: true, disabled: !rejectReason.trim() }} confirmLoading={actionLoading}>
        <p>Provide a reason for rejecting <strong>{selected?.requestNumber}</strong>:</p>
        <TextArea rows={3} value={rejectReason} onChange={e => setRejectReason(e.target.value)} placeholder="Reason for rejection…" maxLength={1000} showCount />
      </Modal>

      {/* Dispatch modal */}
      <Modal title={<><ToolOutlined /> Send to Workshop</>} open={dispatchOpen}
        onOk={async () => { if (!workshopName.trim() || !selected) return; await act(() => vehicleMaintenanceApi.dispatch(selected.id, workshopName, workshopLoc || undefined)); setDispatchOpen(false); setWorkshopName(''); setWorkshopLoc(''); }}
        onCancel={() => { setDispatchOpen(false); setWorkshopName(''); setWorkshopLoc(''); }}
        okText="Confirm Dispatch" okButtonProps={{ disabled: !workshopName.trim() }} confirmLoading={actionLoading}>
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          <div>
            <Text strong style={{ display: 'block', marginBottom: 6 }}>Workshop / Mechanic Name *</Text>
            <Input placeholder="e.g. AutoFix Garage, Apex Motors…" value={workshopName} onChange={e => setWorkshopName(e.target.value)} maxLength={200} />
          </div>
          <div>
            <Text strong style={{ display: 'block', marginBottom: 6 }}>Workshop Location (optional)</Text>
            <Input placeholder="e.g. Ilupeju, Lagos" value={workshopLoc} onChange={e => setWorkshopLoc(e.target.value)} maxLength={200} />
          </div>
        </Space>
      </Modal>

      {/* Complete modal */}
      <Modal title={<><CheckOutlined /> Mark as Completed</>} open={completeOpen}
        onOk={async () => { if (!selected) return; await act(() => vehicleMaintenanceApi.complete(selected.id, completeNotes || undefined)); setCompleteOpen(false); setCompleteNotes(''); }}
        onCancel={() => { setCompleteOpen(false); setCompleteNotes(''); }}
        okText="Confirm Completion" confirmLoading={actionLoading}>
        <p>Mark <strong>{selected?.requestNumber}</strong> ({selected?.vehicleRegNo}) as completed:</p>
        <TextArea rows={3} value={completeNotes} onChange={e => setCompleteNotes(e.target.value)}
          placeholder="Completion notes — work done, parts replaced, etc. (optional)" maxLength={2000} />
      </Modal>
    </div>
  );
}
