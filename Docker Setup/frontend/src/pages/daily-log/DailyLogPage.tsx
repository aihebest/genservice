import { useState } from 'react';
import {
  Card, Table, Button, Form, Input, InputNumber, Select,
  DatePicker, Switch, Drawer, Descriptions, Tag, Space,
  Row, Col, Statistic, Alert, Divider, message, Tooltip,
  Badge, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, EditOutlined, EyeOutlined,
  ThunderboltOutlined, ExperimentOutlined, TeamOutlined,
  WarningOutlined, CheckCircleOutlined, ReloadOutlined,
} from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import dailyParameterLogApi from '../../api/dailyParameterLog.api';
import type {
  DailyParameterLog,
  CreateDailyParameterLogPayload,
} from '../../types';
import { OFFICE_LOCATIONS } from '../../types';


const { Title, Text } = Typography;
const { Option } = Select;
const { TextArea } = Input;

const GENERATOR_STATUS_OPTIONS = ['Running', 'Standby', 'Fault', 'Off'];
const WATER_SOURCE_OPTIONS      = ['Municipal', 'Borehole', 'Both', 'None'];
const WATER_STATUS_OPTIONS      = ['Adequate', 'Low', 'Critical', 'Refilled'];
const SECURITY_STATUS_OPTIONS   = ['Normal', 'Incident Reported'];

const genStatusColor = (s?: string) => {
  if (!s) return 'default';
  return { Running: 'green', Standby: 'blue', Fault: 'red', Off: 'default' }[s] ?? 'default';
};

const waterStatusColor = (s?: string) => {
  if (!s) return 'default';
  return { Adequate: 'green', Low: 'gold', Critical: 'red', Refilled: 'cyan' }[s] ?? 'default';
};

const securityColor = (s?: string) =>
  s === 'Incident Reported' ? 'red' : 'green';

// ── Component ─────────────────────────────────────────────────────────────────
export default function DailyLogPage() {
  const qc = useQueryClient();

  const [filterLocation, setFilterLocation] = useState<string | undefined>();
  const [filterFrom,     setFilterFrom]     = useState<string | undefined>();
  const [filterTo,       setFilterTo]       = useState<string | undefined>();
  const [page,           setPage]           = useState(1);

  const [createOpen,  setCreateOpen]  = useState(false);
  const [editRecord,  setEditRecord]  = useState<DailyParameterLog | null>(null);
  const [viewRecord,  setViewRecord]  = useState<DailyParameterLog | null>(null);

  const [createForm] = Form.useForm();
  const [editForm]   = Form.useForm();

  // ── Queries ────────────────────────────────────────────────────────────────
  const { data: statsData } = useQuery({
    queryKey: ['daily-log-stats'],
    queryFn:  () => dailyParameterLogApi.stats(),
  });

  const { data: listData, isLoading } = useQuery({
    queryKey: ['daily-logs', filterLocation, filterFrom, filterTo, page],
    queryFn:  () => dailyParameterLogApi.list({
      location: filterLocation,
      from:     filterFrom,
      to:       filterTo,
      page,
      pageSize: 20,
    }),
  });

  // ── Mutations ──────────────────────────────────────────────────────────────
  const createMutation = useMutation({
    mutationFn: (payload: CreateDailyParameterLogPayload) =>
      dailyParameterLogApi.create(payload),
    onSuccess: () => {
      message.success('Daily log created successfully.');
      qc.invalidateQueries({ queryKey: ['daily-logs'] });
      qc.invalidateQueries({ queryKey: ['daily-log-stats'] });
      setCreateOpen(false);
      createForm.resetFields();
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      message.error(err?.response?.data?.message ?? 'Failed to create log.');
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: Partial<CreateDailyParameterLogPayload> }) =>
      dailyParameterLogApi.update(id, payload),
    onSuccess: () => {
      message.success('Log updated successfully.');
      qc.invalidateQueries({ queryKey: ['daily-logs'] });
      qc.invalidateQueries({ queryKey: ['daily-log-stats'] });
      setEditRecord(null);
    },
    onError: () => message.error('Failed to update log.'),
  });

  // ── Handlers ───────────────────────────────────────────────────────────────
  const handleCreate = async () => {
    try {
      const vals = await createForm.validateFields();
      const payload: CreateDailyParameterLogPayload = {
        logDate:               vals.logDate.format('YYYY-MM-DD'),
        location:              vals.location,
        nepaHoursAvailable:    vals.nepaHoursAvailable,
        generatorHoursRun:     vals.generatorHoursRun,
        dieselConsumedLitres:  vals.dieselConsumedLitres,
        dieselBalanceLitres:   vals.dieselBalanceLitres,
        generatorStatus:       vals.generatorStatus,
        generatorRunHourMeter: vals.generatorRunHourMeter,
        waterSource:           vals.waterSource,
        waterTankLevelPercent: vals.waterTankLevelPercent,
        waterStatus:           vals.waterStatus,
        staffPresent:          vals.staffPresent,
        expectedStaff:         vals.expectedStaff,
        visitorCount:          vals.visitorCount,
        cleaningDone:          vals.cleaningDone ?? false,
        wasteDisposed:         vals.wasteDisposed ?? false,
        securityStatus:        vals.securityStatus,
        maintenanceIssues:     vals.maintenanceIssues,
        actionsTaken:          vals.actionsTaken,
        pendingActions:        vals.pendingActions,
        generalRemarks:        vals.generalRemarks,
      };
      createMutation.mutate(payload);
    } catch { /* validation */ }
  };

  const handleOpenEdit = (record: DailyParameterLog) => {
    setEditRecord(record);
    editForm.setFieldsValue({
      nepaHoursAvailable:    record.nepaHoursAvailable,
      generatorHoursRun:     record.generatorHoursRun,
      dieselConsumedLitres:  record.dieselConsumedLitres,
      dieselBalanceLitres:   record.dieselBalanceLitres,
      generatorStatus:       record.generatorStatus,
      generatorRunHourMeter: record.generatorRunHourMeter,
      waterSource:           record.waterSource,
      waterTankLevelPercent: record.waterTankLevelPercent,
      waterStatus:           record.waterStatus,
      staffPresent:          record.staffPresent,
      expectedStaff:         record.expectedStaff,
      visitorCount:          record.visitorCount,
      cleaningDone:          record.cleaningDone,
      wasteDisposed:         record.wasteDisposed,
      securityStatus:        record.securityStatus,
      maintenanceIssues:     record.maintenanceIssues,
      actionsTaken:          record.actionsTaken,
      pendingActions:        record.pendingActions,
      generalRemarks:        record.generalRemarks,
    });
  };

  const handleUpdate = async () => {
    if (!editRecord) return;
    try {
      const vals = await editForm.validateFields();
      updateMutation.mutate({ id: editRecord.id, payload: vals });
    } catch { /* validation */ }
  };

  // ── Table columns ──────────────────────────────────────────────────────────
  const columns: ColumnsType<DailyParameterLog> = [
    {
      title: 'Date',
      dataIndex: 'logDate',
      width: 110,
      render: (v: string) => <strong>{dayjs(v).format('DD MMM YYYY')}</strong>,
      sorter: (a, b) => a.logDate.localeCompare(b.logDate),
    },
    {
      title: 'Location',
      dataIndex: 'location',
      width: 160,
      render: (v: string) => <Tag color="blue">{v}</Tag>,
    },
    {
      title: '⚡ NEPA (hrs)',
      dataIndex: 'nepaHoursAvailable',
      width: 100,
      render: (v?: number) => v != null ? `${v}h` : '—',
    },
    {
      title: '🔧 Gen (hrs)',
      dataIndex: 'generatorHoursRun',
      width: 100,
      render: (v?: number) => v != null ? `${v}h` : '—',
    },
    {
      title: 'Gen Status',
      dataIndex: 'generatorStatus',
      width: 110,
      render: (v?: string) => v
        ? <Tag color={genStatusColor(v)}>{v}</Tag>
        : '—',
    },
    {
      title: '⛽ Diesel Used (L)',
      dataIndex: 'dieselConsumedLitres',
      width: 120,
      render: (v?: number) =>
        v != null
          ? <span style={{ color: v > 80 ? '#cf1322' : 'inherit' }}>{v}L</span>
          : '—',
    },
    {
      title: '💧 Water',
      dataIndex: 'waterStatus',
      width: 100,
      render: (v?: string) => v
        ? <Tag color={waterStatusColor(v)}>{v}</Tag>
        : '—',
    },
    {
      title: '👥 Staff',
      key: 'staff',
      width: 90,
      render: (_, r) =>
        r.staffPresent != null && r.expectedStaff != null
          ? `${r.staffPresent}/${r.expectedStaff}`
          : r.staffPresent != null ? String(r.staffPresent) : '—',
    },
    {
      title: '✅ Checks',
      key: 'checks',
      width: 100,
      render: (_, r) => (
        <Space size={4}>
          <Tooltip title="Cleaning done">
            {r.cleaningDone
              ? <CheckCircleOutlined style={{ color: 'green' }} />
              : <WarningOutlined     style={{ color: '#faad14' }} />}
          </Tooltip>
          <Tooltip title="Waste disposed">
            {r.wasteDisposed
              ? <CheckCircleOutlined style={{ color: 'green' }} />
              : <WarningOutlined     style={{ color: '#faad14' }} />}
          </Tooltip>
          <Tooltip title={`Security: ${r.securityStatus ?? 'N/A'}`}>
            <Badge
              color={securityColor(r.securityStatus)}
              style={{ marginLeft: 2 }}
            />
          </Tooltip>
        </Space>
      ),
    },
    {
      title: 'Logged By',
      dataIndex: 'loggedByName',
      width: 130,
      render: (v: string) => <Text type="secondary">{v}</Text>,
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 90,
      fixed: 'right' as const,
      render: (_, r) => (
        <Space>
          <Tooltip title="View details">
            <Button size="small" icon={<EyeOutlined />}
              onClick={() => setViewRecord(r)} />
          </Tooltip>
          <Tooltip title="Edit">
            <Button size="small" icon={<EditOutlined />}
              onClick={() => handleOpenEdit(r)} />
          </Tooltip>
        </Space>
      ),
    },
  ];

  // ── Shared form sections ───────────────────────────────────────────────────
  const PowerSection = () => (
    <>
      <Divider titlePlacement="left"><ThunderboltOutlined /> Power Supply</Divider>
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item name="nepaHoursAvailable" label="NEPA Hours Available">
            <InputNumber min={0} max={24} step={0.5} style={{ width: '100%' }} addonAfter="hrs" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="generatorHoursRun" label="Generator Hours Run">
            <InputNumber min={0} max={24} step={0.5} style={{ width: '100%' }} addonAfter="hrs" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="generatorStatus" label="Generator Status">
            <Select placeholder="Select status" allowClear>
              {GENERATOR_STATUS_OPTIONS.map(s => <Option key={s} value={s}>{s}</Option>)}
            </Select>
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item name="generatorRunHourMeter" label="Hour Meter Reading">
            <InputNumber min={0} style={{ width: '100%' }} addonAfter="hrs" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="dieselConsumedLitres" label="Diesel Consumed">
            <InputNumber min={0} style={{ width: '100%' }} addonAfter="L" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="dieselBalanceLitres" label="Diesel Balance">
            <InputNumber min={0} style={{ width: '100%' }} addonAfter="L" />
          </Form.Item>
        </Col>
      </Row>
    </>
  );

  const WaterSection = () => (
    <>
      <Divider titlePlacement="left"><ExperimentOutlined /> Water Supply</Divider>
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item name="waterSource" label="Water Source">
            <Select placeholder="Select source" allowClear>
              {WATER_SOURCE_OPTIONS.map(s => <Option key={s} value={s}>{s}</Option>)}
            </Select>
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="waterTankLevelPercent" label="Tank Level (%)">
            <InputNumber min={0} max={100} style={{ width: '100%' }} addonAfter="%" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="waterStatus" label="Water Status">
            <Select placeholder="Select status" allowClear>
              {WATER_STATUS_OPTIONS.map(s => <Option key={s} value={s}>{s}</Option>)}
            </Select>
          </Form.Item>
        </Col>
      </Row>
    </>
  );

  const StaffSection = () => (
    <>
      <Divider titlePlacement="left"><TeamOutlined /> Staff & Occupancy</Divider>
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item name="staffPresent" label="Staff Present">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="expectedStaff" label="Expected Staff">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="visitorCount" label="Visitors">
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Col>
      </Row>
    </>
  );

  const FacilitySection = () => (
    <>
      <Divider titlePlacement="left">🏢 Facility Checks</Divider>
      <Row gutter={16}>
        <Col span={8}>
          <Form.Item name="cleaningDone" label="Cleaning Done" valuePropName="checked">
            <Switch checkedChildren="Yes" unCheckedChildren="No" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="wasteDisposed" label="Waste Disposed" valuePropName="checked">
            <Switch checkedChildren="Yes" unCheckedChildren="No" />
          </Form.Item>
        </Col>
        <Col span={8}>
          <Form.Item name="securityStatus" label="Security Status">
            <Select placeholder="Select status" allowClear>
              {SECURITY_STATUS_OPTIONS.map(s => <Option key={s} value={s}>{s}</Option>)}
            </Select>
          </Form.Item>
        </Col>
      </Row>
    </>
  );

  const ObservationsSection = () => (
    <>
      <Divider titlePlacement="left">📝 Observations & Actions</Divider>
      <Form.Item name="maintenanceIssues" label="Maintenance Issues Noticed">
        <TextArea rows={2} placeholder="Any faults or issues observed today..." />
      </Form.Item>
      <Form.Item name="actionsTaken" label="Actions Taken Today">
        <TextArea rows={2} placeholder="What was done to address issues..." />
      </Form.Item>
      <Form.Item name="pendingActions" label="Pending Actions / Follow-up">
        <TextArea rows={2} placeholder="Outstanding items to be actioned..." />
      </Form.Item>
      <Form.Item name="generalRemarks" label="General Remarks">
        <TextArea rows={2} placeholder="Any other remarks for today..." />
      </Form.Item>
    </>
  );

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div style={{ padding: 24 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={3} style={{ margin: 0 }}>Daily Routine Parameter Check</Title>
          <Text type="secondary">Log daily operational readings by location</Text>
        </div>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          onClick={() => {
            createForm.resetFields();
            createForm.setFieldValue('logDate', dayjs());
            createForm.setFieldValue('cleaningDone', false);
            createForm.setFieldValue('wasteDisposed', false);
            createForm.setFieldValue('securityStatus', 'Normal');
            setCreateOpen(true);
          }}
        >
          New Daily Log
        </Button>
      </div>

      {/* Stats cards */}
      {statsData && (
        <Row gutter={16} style={{ marginBottom: 24 }}>
          <Col span={5}>
            <Card size="small">
              <Statistic
                title="Logs This Month"
                value={statsData.logsThisMonth}
                prefix={<CheckCircleOutlined style={{ color: 'green' }} />}
              />
            </Card>
          </Col>
          <Col span={5}>
            <Card size="small">
              <Statistic
                title="Avg NEPA Hours / Day"
                value={statsData.avgNepaHoursThisMonth?.toFixed(1) ?? '—'}
                suffix="hrs"
                prefix={<ThunderboltOutlined style={{ color: '#faad14' }} />}
              />
            </Card>
          </Col>
          <Col span={5}>
            <Card size="small">
              <Statistic
                title="Avg Gen Runtime / Day"
                value={statsData.avgGeneratorHoursThisMonth?.toFixed(1) ?? '—'}
                suffix="hrs"
                prefix={<ThunderboltOutlined style={{ color: '#1890ff' }} />}
              />
            </Card>
          </Col>
          <Col span={5}>
            <Card size="small">
              <Statistic
                title="Total Diesel This Month"
                value={statsData.totalDieselThisMonth?.toFixed(0) ?? '—'}
                suffix="L"
                prefix="⛽"
              />
            </Card>
          </Col>
          <Col span={4}>
            <Card size="small">
              <Statistic
                title="Locations Logged"
                value={statsData.locationsLogged}
                prefix="📍"
              />
            </Card>
          </Col>
        </Row>
      )}

      {/* Filters */}
      <Card size="small" style={{ marginBottom: 16 }}>
        <Row gutter={12} align="middle">
          <Col>
            <Select
              allowClear
              placeholder="Filter by location"
              style={{ width: 200 }}
              value={filterLocation}
              onChange={v => { setFilterLocation(v); setPage(1); }}
            >
              {OFFICE_LOCATIONS.map(l => <Option key={l} value={l}>{l}</Option>)}
            </Select>
          </Col>
          <Col>
            <DatePicker
              placeholder="From date"
              onChange={d => { setFilterFrom(d ? d.format('YYYY-MM-DD') : undefined); setPage(1); }}
            />
          </Col>
          <Col>
            <DatePicker
              placeholder="To date"
              onChange={d => { setFilterTo(d ? d.format('YYYY-MM-DD') : undefined); setPage(1); }}
            />
          </Col>
          <Col>
            <Button
              icon={<ReloadOutlined />}
              onClick={() => { setFilterLocation(undefined); setFilterFrom(undefined); setFilterTo(undefined); setPage(1); }}
            >
              Reset
            </Button>
          </Col>
        </Row>
      </Card>

      {/* Table */}
      <Table
        columns={columns}
        dataSource={listData?.items ?? []}
        rowKey="id"
        loading={isLoading}
        scroll={{ x: 1100 }}
        pagination={{
          current:   page,
          pageSize:  20,
          total:     listData?.totalCount ?? 0,
          onChange:  p => setPage(p),
          showTotal: (t) => `${t} records`,
        }}
        size="small"
      />

      {/* ── Create modal ─────────────────────────────────────────────────── */}
      <Drawer
        title="New Daily Parameter Log"
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        width={800}
        footer={
          <div style={{ textAlign: 'right' }}>
            <Button onClick={() => setCreateOpen(false)} style={{ marginRight: 8 }}>Cancel</Button>
            <Button type="primary" loading={createMutation.isPending} onClick={handleCreate}>
              Submit Log
            </Button>
          </div>
        }
      >
        <Form form={createForm} layout="vertical">
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="logDate" label="Date" rules={[{ required: true, message: 'Date is required' }]}>
                <DatePicker style={{ width: '100%' }} disabledDate={d => d.isAfter(dayjs())} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="location" label="Location" rules={[{ required: true, message: 'Location is required' }]}>
                <Select placeholder="Select location" showSearch>
                  {OFFICE_LOCATIONS.map(l => <Option key={l} value={l}>{l}</Option>)}
                </Select>
              </Form.Item>
            </Col>
          </Row>
          <PowerSection />
          <WaterSection />
          <StaffSection />
          <FacilitySection />
          <ObservationsSection />
        </Form>
      </Drawer>

      {/* ── Edit drawer ──────────────────────────────────────────────────── */}
      <Drawer
        title={editRecord
          ? `Edit Log — ${editRecord.location} · ${dayjs(editRecord.logDate).format('DD MMM YYYY')}`
          : 'Edit Log'}
        open={!!editRecord}
        onClose={() => setEditRecord(null)}
        width={800}
        footer={
          <div style={{ textAlign: 'right' }}>
            <Button onClick={() => setEditRecord(null)} style={{ marginRight: 8 }}>Cancel</Button>
            <Button type="primary" loading={updateMutation.isPending} onClick={handleUpdate}>
              Save Changes
            </Button>
          </div>
        }
      >
        <Form form={editForm} layout="vertical">
          <Alert
            message={`Editing log for ${editRecord?.location} on ${editRecord ? dayjs(editRecord.logDate).format('DD MMM YYYY') : ''}`}
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
          />
          <PowerSection />
          <WaterSection />
          <StaffSection />
          <FacilitySection />
          <ObservationsSection />
        </Form>
      </Drawer>

      {/* ── View drawer ──────────────────────────────────────────────────── */}
      <Drawer
        title={viewRecord
          ? `Daily Log — ${viewRecord.location} · ${dayjs(viewRecord.logDate).format('DD MMM YYYY')}`
          : 'Log Details'}
        open={!!viewRecord}
        onClose={() => setViewRecord(null)}
        width={700}
        extra={
          <Button
            icon={<EditOutlined />}
            onClick={() => { if (viewRecord) { setViewRecord(null); handleOpenEdit(viewRecord); } }}
          >
            Edit
          </Button>
        }
      >
        {viewRecord && (
          <Space direction="vertical" style={{ width: '100%' }} size={0}>
            {/* Power section */}
            <Card
              title={<><ThunderboltOutlined /> Power Supply</>}
              size="small"
              style={{ marginBottom: 12 }}
            >
              <Descriptions size="small" column={2} bordered>
                <Descriptions.Item label="NEPA Hours">
                  {viewRecord.nepaHoursAvailable != null ? `${viewRecord.nepaHoursAvailable} hrs` : '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Generator Hours">
                  {viewRecord.generatorHoursRun != null ? `${viewRecord.generatorHoursRun} hrs` : '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Generator Status">
                  {viewRecord.generatorStatus
                    ? <Tag color={genStatusColor(viewRecord.generatorStatus)}>{viewRecord.generatorStatus}</Tag>
                    : '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Hour Meter">
                  {viewRecord.generatorRunHourMeter != null ? `${viewRecord.generatorRunHourMeter} hrs` : '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Diesel Consumed">
                  {viewRecord.dieselConsumedLitres != null ? `${viewRecord.dieselConsumedLitres} L` : '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Diesel Balance">
                  {viewRecord.dieselBalanceLitres != null
                    ? <span style={{ color: viewRecord.dieselBalanceLitres < 100 ? '#cf1322' : 'inherit' }}>
                        {viewRecord.dieselBalanceLitres} L
                        {viewRecord.dieselBalanceLitres < 100 ? ' ⚠️' : ''}
                      </span>
                    : '—'}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            {/* Water section */}
            <Card
              title={<><ExperimentOutlined /> Water Supply</>}
              size="small"
              style={{ marginBottom: 12 }}
            >
              <Descriptions size="small" column={3} bordered>
                <Descriptions.Item label="Water Source">
                  {viewRecord.waterSource ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Tank Level">
                  {viewRecord.waterTankLevelPercent != null ? `${viewRecord.waterTankLevelPercent}%` : '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Water Status">
                  {viewRecord.waterStatus
                    ? <Tag color={waterStatusColor(viewRecord.waterStatus)}>{viewRecord.waterStatus}</Tag>
                    : '—'}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            {/* Staff section */}
            <Card
              title={<><TeamOutlined /> Staff & Occupancy</>}
              size="small"
              style={{ marginBottom: 12 }}
            >
              <Descriptions size="small" column={3} bordered>
                <Descriptions.Item label="Staff Present">
                  {viewRecord.staffPresent ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Expected Staff">
                  {viewRecord.expectedStaff ?? '—'}
                </Descriptions.Item>
                <Descriptions.Item label="Visitors">
                  {viewRecord.visitorCount ?? '—'}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            {/* Facility checks */}
            <Card title="🏢 Facility Checks" size="small" style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={3} bordered>
                <Descriptions.Item label="Cleaning">
                  <Tag color={viewRecord.cleaningDone ? 'green' : 'red'}>
                    {viewRecord.cleaningDone ? 'Done' : 'Pending'}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label="Waste Disposal">
                  <Tag color={viewRecord.wasteDisposed ? 'green' : 'red'}>
                    {viewRecord.wasteDisposed ? 'Done' : 'Pending'}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label="Security">
                  <Tag color={securityColor(viewRecord.securityStatus)}>
                    {viewRecord.securityStatus ?? 'N/A'}
                  </Tag>
                </Descriptions.Item>
              </Descriptions>
            </Card>

            {/* Observations */}
            {(viewRecord.maintenanceIssues || viewRecord.actionsTaken || viewRecord.pendingActions || viewRecord.generalRemarks) && (
              <Card title="📝 Observations & Actions" size="small" style={{ marginBottom: 12 }}>
                {viewRecord.maintenanceIssues && (
                  <>
                    <Text strong>Maintenance Issues:</Text>
                    <p style={{ marginTop: 4, whiteSpace: 'pre-wrap' }}>{viewRecord.maintenanceIssues}</p>
                  </>
                )}
                {viewRecord.actionsTaken && (
                  <>
                    <Text strong>Actions Taken:</Text>
                    <p style={{ marginTop: 4, whiteSpace: 'pre-wrap' }}>{viewRecord.actionsTaken}</p>
                  </>
                )}
                {viewRecord.pendingActions && (
                  <Alert
                    message="Pending Actions"
                    description={viewRecord.pendingActions}
                    type="warning"
                    showIcon
                    style={{ marginBottom: 8 }}
                  />
                )}
                {viewRecord.generalRemarks && (
                  <>
                    <Text strong>General Remarks:</Text>
                    <p style={{ marginTop: 4, whiteSpace: 'pre-wrap' }}>{viewRecord.generalRemarks}</p>
                  </>
                )}
              </Card>
            )}

            <Text type="secondary" style={{ fontSize: 12 }}>
              Logged by <strong>{viewRecord.loggedByName}</strong> ({viewRecord.loggedByEmail})
              &nbsp;·&nbsp;
              {dayjs(viewRecord.createdAt).format('DD MMM YYYY HH:mm')}
            </Text>
          </Space>
        )}
      </Drawer>
    </div>
  );
}
