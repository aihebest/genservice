import { useState } from 'react';
import {
  Card, Table, Button, Form, Input, InputNumber, Select, DatePicker, Drawer,
  Descriptions, Tag, Space, Row, Col, Statistic, message, Tooltip, Popconfirm, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, EditOutlined, EyeOutlined, DeleteOutlined, ReloadOutlined,
  HomeOutlined, TeamOutlined, CoffeeOutlined, WalletOutlined,
} from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import accommodationApi from '../../api/accommodation.api';
import { useAuthStore } from '../../store/authStore';
import {
  OFFICE_LOCATIONS, MEAL_PLAN_OPTIONS, ACCOMMODATION_STATUSES, ACCOMMODATION_STATUS_META,
} from '../../types';
import type { AccommodationLog, CreateAccommodationPayload } from '../../types';

const { Title, Text } = Typography;
const { TextArea } = Input;

const naira = (v?: number) => v != null ? `₦${Number(v).toLocaleString()}` : '—';

export default function FeedingAccommodationPage() {
  const qc = useQueryClient();
  // Only managers/admins may correct existing records (enforced server-side too).
  const role = useAuthStore(s => s.user?.role);
  const canEdit = role === 'DepartmentManager' || role === 'SystemAdmin';

  const [filterHouse,  setFilterHouse]  = useState<string | undefined>();
  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [filterFrom,   setFilterFrom]   = useState<string | undefined>();
  const [filterTo,     setFilterTo]     = useState<string | undefined>();
  const [search,       setSearch]       = useState<string | undefined>();
  const [page,         setPage]         = useState(1);

  const [createOpen, setCreateOpen] = useState(false);
  const [editRecord, setEditRecord] = useState<AccommodationLog | null>(null);
  const [viewRecord, setViewRecord] = useState<AccommodationLog | null>(null);

  const [createForm] = Form.useForm();
  const [editForm]   = Form.useForm();

  const { data: stats } = useQuery({
    queryKey: ['accommodation-stats'],
    queryFn:  accommodationApi.stats,
    refetchInterval: 60_000,
  });

  const { data: listData, isLoading } = useQuery({
    queryKey: ['accommodation', filterHouse, filterStatus, filterFrom, filterTo, search, page],
    queryFn:  () => accommodationApi.list({
      guestHouse: filterHouse, status: filterStatus, from: filterFrom, to: filterTo,
      search, page, pageSize: 20,
    }),
  });

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['accommodation'] });
    qc.invalidateQueries({ queryKey: ['accommodation-stats'] });
  };

  const createMutation = useMutation({
    mutationFn: (payload: CreateAccommodationPayload) => accommodationApi.create(payload),
    onSuccess: () => { message.success('Guest record added'); invalidate(); setCreateOpen(false); createForm.resetFields(); setPage(1); },
    onError: (e: { response?: { data?: { message?: string } } }) => message.error(e?.response?.data?.message ?? 'Failed to save'),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: Partial<CreateAccommodationPayload> }) => accommodationApi.update(id, payload),
    onSuccess: () => { message.success('Record updated'); invalidate(); setEditRecord(null); },
    onError: (e: { response?: { data?: { message?: string } } }) => message.error(e?.response?.data?.message ?? 'Failed to update'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => accommodationApi.delete(id),
    onSuccess: () => { message.success('Record deleted'); invalidate(); },
    onError: () => message.error('Failed to delete'),
  });

  const buildPayload = (vals: Record<string, unknown>): CreateAccommodationPayload => ({
    guestName:              (vals.guestName as string)?.trim(),
    guestHouse:             vals.guestHouse as string,
    checkInDate:            (vals.checkInDate as dayjs.Dayjs).format('YYYY-MM-DD'),
    department:             (vals.department as string | undefined)?.trim() || undefined,
    purpose:                (vals.purpose as string | undefined)?.trim() || undefined,
    checkOutDate:           vals.checkOutDate ? (vals.checkOutDate as dayjs.Dayjs).format('YYYY-MM-DD') : undefined,
    nights:                 vals.nights as number | undefined,
    mealPlan:               vals.mealPlan as string | undefined,
    numberOfMeals:          vals.numberOfMeals as number | undefined,
    feedingCostNaira:       vals.feedingCostNaira as number | undefined,
    accommodationCostNaira: vals.accommodationCostNaira as number | undefined,
    status:                 vals.status as string | undefined,
    notes:                  (vals.notes as string | undefined)?.trim() || undefined,
  });

  const handleCreate = async () => {
    try { const vals = await createForm.validateFields(); createMutation.mutate(buildPayload(vals)); }
    catch { /* validation */ }
  };

  const handleOpenEdit = (r: AccommodationLog) => {
    setEditRecord(r);
    editForm.setFieldsValue({
      guestName: r.guestName, department: r.department, guestHouse: r.guestHouse, purpose: r.purpose,
      checkInDate: r.checkInDate ? dayjs(r.checkInDate) : undefined,
      checkOutDate: r.checkOutDate ? dayjs(r.checkOutDate) : undefined,
      nights: r.nights, mealPlan: r.mealPlan, numberOfMeals: r.numberOfMeals,
      feedingCostNaira: r.feedingCostNaira, accommodationCostNaira: r.accommodationCostNaira,
      status: r.status, notes: r.notes,
    });
  };

  const handleUpdate = async () => {
    if (!editRecord) return;
    try {
      const vals = await editForm.validateFields();
      updateMutation.mutate({ id: editRecord.id, payload: buildPayload(vals) });
    } catch { /* validation */ }
  };

  const columns: ColumnsType<AccommodationLog> = [
    { title: 'Ref', dataIndex: 'reference', width: 100, render: (v: string) => <Text strong>{v}</Text> },
    { title: 'Guest', dataIndex: 'guestName', width: 160, ellipsis: true,
      render: (v: string, r) => <span>{v}{r.department ? <><br /><Text type="secondary" style={{ fontSize: 11 }}>{r.department}</Text></> : null}</span> },
    { title: 'Guest House', dataIndex: 'guestHouse', width: 150, ellipsis: true, render: (v: string) => <Tag color="blue">{v}</Tag> },
    { title: 'Check-In', dataIndex: 'checkInDate', width: 110, render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Check-Out', dataIndex: 'checkOutDate', width: 110, render: (v?: string) => v ? dayjs(v).format('D MMM YY') : '—' },
    { title: 'Nights', dataIndex: 'nights', width: 75, render: (v?: number) => v ?? '—' },
    { title: 'Meal Plan', dataIndex: 'mealPlan', width: 110, render: (v?: string) => v ?? '—' },
    { title: 'Feeding (₦)', dataIndex: 'feedingCostNaira', width: 115, render: (v?: number) => naira(v) },
    { title: 'Accommodation (₦)', dataIndex: 'accommodationCostNaira', width: 140, render: (v?: number) => naira(v) },
    { title: 'Total (₦)', dataIndex: 'totalCostNaira', width: 120,
      render: (v?: number) => v != null ? <Text strong style={{ color: '#389e0d' }}>{naira(v)}</Text> : <Text type="secondary">—</Text> },
    { title: 'Status', dataIndex: 'status', width: 115,
      render: (v: string) => { const m = ACCOMMODATION_STATUS_META[v]; return <Tag color={m?.color}>{m?.label ?? v}</Tag>; } },
    { title: 'Logged By', dataIndex: 'loggedByName', width: 130, ellipsis: true, render: (v: string) => <Text type="secondary">{v}</Text> },
    { title: 'Actions', key: 'actions', width: 120, fixed: 'right' as const,
      render: (_: unknown, r: AccommodationLog) => (
        <Space size={4}>
          <Tooltip title="View"><Button size="small" icon={<EyeOutlined />} onClick={() => setViewRecord(r)} /></Tooltip>
          {canEdit && (
            <>
              <Tooltip title="Edit record"><Button size="small" icon={<EditOutlined />} onClick={() => handleOpenEdit(r)} /></Tooltip>
              <Popconfirm title="Delete this record?" okText="Delete" okButtonProps={{ danger: true }} onConfirm={() => deleteMutation.mutate(r.id)}>
                <Button size="small" danger icon={<DeleteOutlined />} />
              </Popconfirm>
            </>
          )}
        </Space>
      ) },
  ];

  const FormFields = () => (
    <>
      <Row gutter={16}>
        <Col span={12}>
          <Form.Item name="guestName" label="Guest / Staff Name" rules={[{ required: true, message: 'Guest name is required' }]}>
            <Input placeholder="Full name of staff on transit" />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name="department" label="Department / Unit">
            <Input placeholder="e.g. Logistics, Engineering" />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={16}>
        <Col span={12}>
          <Form.Item name="guestHouse" label="Guest House" rules={[{ required: true, message: 'Guest house is required' }]}>
            <Select showSearch placeholder="Select guest house / location"
              options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name="purpose" label="Purpose / Destination">
            <Input placeholder="e.g. In transit to Bonny" />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item name="checkInDate" label="Check-In Date" rules={[{ required: true, message: 'Check-in date is required' }]}>
            <DatePicker style={{ width: '100%' }} format="D MMM YYYY" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="checkOutDate" label="Check-Out Date">
            <DatePicker style={{ width: '100%' }} format="D MMM YYYY" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="nights" label="Nights" tooltip="Auto-calculated from dates if left blank.">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item name="mealPlan" label="Meal Plan">
            <Select allowClear placeholder="Select" options={MEAL_PLAN_OPTIONS.map(m => ({ value: m, label: m }))} />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="numberOfMeals" label="Number of Meals">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="status" label="Status" initialValue="CheckedIn">
            <Select options={ACCOMMODATION_STATUSES.map(s => ({ value: s, label: ACCOMMODATION_STATUS_META[s]?.label ?? s }))} />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={16}>
        <Col span={12}>
          <Form.Item name="feedingCostNaira" label="Feeding Cost (₦)">
            <InputNumber style={{ width: '100%' }} min={0} placeholder="Total feeding cost"
              formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
              parser={(v: string | undefined) => parseFloat(v?.replace(/₦\s?|(,*)/g, '') ?? '0') as 0} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name="accommodationCostNaira" label="Accommodation Cost (₦)">
            <InputNumber style={{ width: '100%' }} min={0} placeholder="Total accommodation cost"
              formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
              parser={(v: string | undefined) => parseFloat(v?.replace(/₦\s?|(,*)/g, '') ?? '0') as 0} />
          </Form.Item>
        </Col>
      </Row>
      <Form.Item name="notes" label="Notes">
        <TextArea rows={2} maxLength={2000} placeholder="Any remarks…" />
      </Form.Item>
    </>
  );

  return (
    <div style={{ padding: 24 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <Title level={3} style={{ margin: 0 }}>Feeding / Accommodation</Title>
          <Text type="secondary">Guest house log for staff on transit — feeding, stays and costs</Text>
        </div>
        <Button type="primary" icon={<PlusOutlined />}
          onClick={() => { createForm.resetFields(); createForm.setFieldsValue({ checkInDate: dayjs(), status: 'CheckedIn' }); setCreateOpen(true); }}>
          New Guest Record
        </Button>
      </div>

      <Row gutter={16} style={{ marginBottom: 20 }}>
        <Col span={5}><Card size="small"><Statistic title="Guests This Month" value={stats?.guestsThisMonth ?? 0} prefix={<TeamOutlined style={{ color: '#1677ff' }} />} /></Card></Col>
        <Col span={5}><Card size="small"><Statistic title="Currently Checked In" value={stats?.currentlyCheckedIn ?? 0} prefix={<HomeOutlined style={{ color: '#52c41a' }} />} /></Card></Col>
        <Col span={5}><Card size="small"><Statistic title="Feeding (Month)" value={stats?.feedingCostThisMonth ?? 0} prefix={<CoffeeOutlined style={{ color: '#fa8c16' }} />} formatter={v => `₦${Number(v).toLocaleString()}`} /></Card></Col>
        <Col span={5}><Card size="small"><Statistic title="Accommodation (Month)" value={stats?.accommodationCostThisMonth ?? 0} prefix={<HomeOutlined style={{ color: '#eb2f96' }} />} formatter={v => `₦${Number(v).toLocaleString()}`} /></Card></Col>
        <Col span={4}><Card size="small"><Statistic title="Total (Month)" value={stats?.totalCostThisMonth ?? 0} prefix={<WalletOutlined style={{ color: '#722ed1' }} />} formatter={v => `₦${Number(v).toLocaleString()}`} /></Card></Col>
      </Row>

      <Card size="small" style={{ marginBottom: 16 }}>
        <Row gutter={12} align="middle">
          <Col>
            <Input.Search allowClear placeholder="Search guest / ref" style={{ width: 200 }}
              onSearch={v => { setSearch(v || undefined); setPage(1); }} />
          </Col>
          <Col>
            <Select allowClear placeholder="Guest house" style={{ width: 180 }} value={filterHouse}
              onChange={v => { setFilterHouse(v); setPage(1); }}
              options={OFFICE_LOCATIONS.map(l => ({ value: l, label: l }))} />
          </Col>
          <Col>
            <Select allowClear placeholder="Status" style={{ width: 150 }} value={filterStatus}
              onChange={v => { setFilterStatus(v); setPage(1); }}
              options={ACCOMMODATION_STATUSES.map(s => ({ value: s, label: ACCOMMODATION_STATUS_META[s]?.label ?? s }))} />
          </Col>
          <Col>
            <DatePicker placeholder="From" onChange={d => { setFilterFrom(d ? d.format('YYYY-MM-DD') : undefined); setPage(1); }} />
          </Col>
          <Col>
            <DatePicker placeholder="To" onChange={d => { setFilterTo(d ? d.format('YYYY-MM-DD') : undefined); setPage(1); }} />
          </Col>
          <Col>
            <Button icon={<ReloadOutlined />}
              onClick={() => { setFilterHouse(undefined); setFilterStatus(undefined); setFilterFrom(undefined); setFilterTo(undefined); setSearch(undefined); setPage(1); }}>
              Reset
            </Button>
          </Col>
        </Row>
      </Card>

      <Table
        columns={columns} dataSource={listData?.items ?? []} rowKey="id" loading={isLoading}
        scroll={{ x: 1500 }} size="small"
        pagination={{ current: page, pageSize: 20, total: listData?.totalCount ?? 0, onChange: setPage, showTotal: t => `${t} records` }}
      />

      {/* Create drawer */}
      <Drawer title="New Guest Record" open={createOpen} onClose={() => setCreateOpen(false)} width={760}
        footer={<div style={{ textAlign: 'right' }}>
          <Button onClick={() => setCreateOpen(false)} style={{ marginRight: 8 }}>Cancel</Button>
          <Button type="primary" loading={createMutation.isPending} onClick={handleCreate}>Save</Button>
        </div>}>
        <Form form={createForm} layout="vertical"><FormFields /></Form>
      </Drawer>

      {/* Edit drawer */}
      <Drawer title={editRecord ? `Edit — ${editRecord.reference} (${editRecord.guestName})` : 'Edit'} open={!!editRecord}
        onClose={() => setEditRecord(null)} width={760}
        footer={<div style={{ textAlign: 'right' }}>
          <Button onClick={() => setEditRecord(null)} style={{ marginRight: 8 }}>Cancel</Button>
          <Button type="primary" loading={updateMutation.isPending} onClick={handleUpdate}>Save Changes</Button>
        </div>}>
        <Form form={editForm} layout="vertical"><FormFields /></Form>
      </Drawer>

      {/* View drawer */}
      <Drawer title={viewRecord ? `${viewRecord.reference} — ${viewRecord.guestName}` : 'Details'} open={!!viewRecord}
        onClose={() => setViewRecord(null)} width={640}
        extra={<Button icon={<EditOutlined />} onClick={() => { if (viewRecord) { const r = viewRecord; setViewRecord(null); handleOpenEdit(r); } }}>Edit</Button>}>
        {viewRecord && (
          <Descriptions column={2} size="small" bordered>
            <Descriptions.Item label="Reference">{viewRecord.reference}</Descriptions.Item>
            <Descriptions.Item label="Status"><Tag color={ACCOMMODATION_STATUS_META[viewRecord.status]?.color}>{ACCOMMODATION_STATUS_META[viewRecord.status]?.label ?? viewRecord.status}</Tag></Descriptions.Item>
            <Descriptions.Item label="Guest">{viewRecord.guestName}</Descriptions.Item>
            <Descriptions.Item label="Department">{viewRecord.department ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Guest House">{viewRecord.guestHouse}</Descriptions.Item>
            <Descriptions.Item label="Purpose">{viewRecord.purpose ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Check-In">{dayjs(viewRecord.checkInDate).format('D MMM YYYY')}</Descriptions.Item>
            <Descriptions.Item label="Check-Out">{viewRecord.checkOutDate ? dayjs(viewRecord.checkOutDate).format('D MMM YYYY') : '—'}</Descriptions.Item>
            <Descriptions.Item label="Nights">{viewRecord.nights ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Meal Plan">{viewRecord.mealPlan ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Number of Meals">{viewRecord.numberOfMeals ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Feeding Cost">{naira(viewRecord.feedingCostNaira)}</Descriptions.Item>
            <Descriptions.Item label="Accommodation Cost">{naira(viewRecord.accommodationCostNaira)}</Descriptions.Item>
            <Descriptions.Item label="Total Cost"><Text strong style={{ color: '#389e0d' }}>{naira(viewRecord.totalCostNaira)}</Text></Descriptions.Item>
            <Descriptions.Item label="Notes" span={2}>{viewRecord.notes ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Logged By" span={2}>{viewRecord.loggedByName} · {dayjs(viewRecord.createdAt).format('D MMM YYYY HH:mm')}</Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>
    </div>
  );
}
