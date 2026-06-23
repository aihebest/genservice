import { useState } from 'react';
import {
  Alert, Badge, Button, Card, Col, Descriptions, Drawer,
  Form, Input, Modal, Popconfirm, Row, Select, Space,
  Statistic, Table, Tag, Tooltip, Typography, message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  UserAddOutlined, EditOutlined, LockOutlined,
  CheckCircleOutlined, StopOutlined, DeleteOutlined,
  SearchOutlined, ReloadOutlined, TeamOutlined,
  CopyOutlined, EyeInvisibleOutlined, EyeOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import usersApi from '../../api/users.api';
import type { UserSummary,
  AppUserRecord, CreateUserPayload, UpdateUserPayload,
  AppUserRole, ResetPasswordResponse,
} from '../../types';
import { ALL_ROLES, ROLE_META } from '../../types';

dayjs.extend(relativeTime);

const { Title, Text, Paragraph } = Typography;
const { Option } = Select;
const { Password } = Input;

const DEPARTMENTS = [
  'General Service', 'Finance', 'HR', 'IT',
  'Operations', 'Procurement', 'Legal', 'Admin', 'Other',
];

// ── Component ─────────────────────────────────────────────────────────────────
export default function UserManagementPage() {
  const qc = useQueryClient();

  // Filters
  const [filterRole,     setFilterRole]     = useState<string | undefined>();
  const [filterActive,   setFilterActive]   = useState<boolean | undefined>();
  const [searchText,     setSearchText]     = useState('');
  const [page,           setPage]           = useState(1);

  // UI state
  const [createOpen,     setCreateOpen]     = useState(false);
  const [editRecord,     setEditRecord]     = useState<AppUserRecord | null>(null);
  const [viewRecord,     setViewRecord]     = useState<AppUserRecord | null>(null);
  const [resetResult,    setResetResult]    = useState<ResetPasswordResponse | null>(null);
  const [pwVisible,      setPwVisible]      = useState(false);

  const [createForm] = Form.useForm();
  const [editForm]   = Form.useForm();

  // ── Queries ────────────────────────────────────────────────────────────────
  const { data: summary } = useQuery<UserSummary>({
    queryKey: ['users-summary'],
    queryFn:  usersApi.summary,
  });

  const { data: listData, isLoading } = useQuery({
    queryKey: ['users', filterRole, filterActive, searchText, page],
    queryFn:  () => usersApi.list({
      role:     filterRole,
      isActive: filterActive,
      search:   searchText || undefined,
      page,
      pageSize: 20,
    }),
  });

  // ── Mutations ──────────────────────────────────────────────────────────────
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['users'] });
    qc.invalidateQueries({ queryKey: ['users-summary'] });
  };

  const createMut = useMutation({
    mutationFn: (p: CreateUserPayload) => usersApi.create(p),
    onSuccess: () => {
      message.success('User created successfully.');
      invalidate();
      setCreateOpen(false);
      createForm.resetFields();
    },
    onError: (e: { response?: { data?: { message?: string } } }) =>
      message.error(e?.response?.data?.message ?? 'Failed to create user.'),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, p }: { id: string; p: UpdateUserPayload }) => usersApi.update(id, p),
    onSuccess: () => {
      message.success('User updated.');
      invalidate();
      setEditRecord(null);
    },
    onError: (e: { response?: { data?: { message?: string } } }) =>
      message.error(e?.response?.data?.message ?? 'Failed to update user.'),
  });

  const deactivateMut = useMutation({
    mutationFn: (id: string) => usersApi.deactivate(id),
    onSuccess: () => { message.success('User deactivated.'); invalidate(); },
    onError:   (e: { response?: { data?: { message?: string } } }) =>
      message.error(e?.response?.data?.message ?? 'Failed to deactivate user.'),
  });

  const activateMut = useMutation({
    mutationFn: (id: string) => usersApi.activate(id),
    onSuccess: () => { message.success('User activated.'); invalidate(); },
    onError:   () => message.error('Failed to activate user.'),
  });

  const resetPwMut = useMutation({
    mutationFn: (id: string) => usersApi.resetPassword(id),
    onSuccess: (data) => { setResetResult(data as ResetPasswordResponse); invalidate(); },
    onError:   () => message.error('Failed to reset password.'),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => usersApi.delete(id),
    onSuccess: () => { message.success('User permanently deleted.'); invalidate(); },
    onError:   (e: { response?: { data?: { message?: string } } }) =>
      message.error(e?.response?.data?.message ?? 'Failed to delete user.'),
  });

  // ── Handlers ───────────────────────────────────────────────────────────────
  const handleCreate = async () => {
    try {
      const v = await createForm.validateFields();
      createMut.mutate(v as CreateUserPayload);
    } catch { /* validation */ }
  };

  const handleOpenEdit = (r: AppUserRecord) => {
    setEditRecord(r);
    editForm.setFieldsValue({ fullName: r.fullName, role: r.role, department: r.department });
  };

  const handleUpdate = async () => {
    if (!editRecord) return;
    try {
      const v = await editForm.validateFields();
      updateMut.mutate({ id: editRecord.id, p: v as UpdateUserPayload });
    } catch { /* validation */ }
  };

  // ── Table columns ──────────────────────────────────────────────────────────
  const columns: ColumnsType<AppUserRecord> = [
    {
      title: 'Name',
      key: 'name',
      width: 180,
      render: (_, r) => (
        <Space>
          <div style={{
            width: 32, height: 32, borderRadius: '50%',
            background: ROLE_META[r.role]?.color === 'default' ? '#d9d9d9' : undefined,
            backgroundColor: r.isActive ? '#1677ff18' : '#f5f5f5',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 13, fontWeight: 700,
            color: r.isActive ? '#1677ff' : '#8c8c8c',
            flexShrink: 0,
          }}>
            {r.fullName.charAt(0).toUpperCase()}
          </div>
          <div>
            <div style={{ fontWeight: 500, lineHeight: 1.2 }}>{r.fullName}</div>
            <div style={{ fontSize: 11, color: '#8c8c8c' }}>{r.email}</div>
          </div>
        </Space>
      ),
    },
    {
      title: 'Role',
      dataIndex: 'role',
      width: 150,
      render: (v: AppUserRole) => {
        const m = ROLE_META[v] ?? { label: v, color: 'default' };
        return <Tag color={m.color}>{m.label}</Tag>;
      },
    },
    {
      title: 'Department',
      dataIndex: 'department',
      width: 140,
    },
    {
      title: 'Status',
      dataIndex: 'isActive',
      width: 100,
      render: (v: boolean) => v
        ? <Badge status="success" text={<Text style={{ color: 'green' }}>Active</Text>} />
        : <Badge status="error"   text={<Text style={{ color: 'red'   }}>Inactive</Text>} />,
    },
    {
      title: 'Last Login',
      dataIndex: 'lastLoginAt',
      width: 130,
      render: (v?: string) => v
        ? <Tooltip title={dayjs(v).format('DD MMM YYYY HH:mm')}>
            <Text type="secondary">{dayjs(v).fromNow()}</Text>
          </Tooltip>
        : <Text type="secondary">Never</Text>,
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      width: 110,
      render: (v: string) => <Text type="secondary">{dayjs(v).format('DD MMM YYYY')}</Text>,
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 160,
      fixed: 'right' as const,
      render: (_, r) => (
        <Space size={4}>
          <Tooltip title="View">
            <Button size="small" icon={<UserOutlined />}
              onClick={() => setViewRecord(r)} />
          </Tooltip>
          <Tooltip title="Edit">
            <Button size="small" icon={<EditOutlined />}
              onClick={() => handleOpenEdit(r)} />
          </Tooltip>
          <Tooltip title="Reset password">
            <Button size="small" icon={<LockOutlined />}
              onClick={() => resetPwMut.mutate(r.id)}
              loading={resetPwMut.isPending} />
          </Tooltip>
          {r.isActive ? (
            <Popconfirm
              title={`Deactivate ${r.fullName}?`}
              description="They will not be able to log in until reactivated."
              onConfirm={() => deactivateMut.mutate(r.id)}
              okText="Deactivate" okButtonProps={{ danger: true }}
            >
              <Tooltip title="Deactivate">
                <Button size="small" danger icon={<StopOutlined />} />
              </Tooltip>
            </Popconfirm>
          ) : (
            <Tooltip title="Activate">
              <Button size="small" icon={<CheckCircleOutlined />}
                style={{ color: 'green', borderColor: 'green' }}
                onClick={() => activateMut.mutate(r.id)} />
            </Tooltip>
          )}
          <Popconfirm
            title={`Permanently delete ${r.fullName}?`}
            description="This cannot be undone."
            onConfirm={() => deleteMut.mutate(r.id)}
            okText="Delete" okButtonProps={{ danger: true }}
          >
            <Tooltip title="Delete">
              <Button size="small" danger icon={<DeleteOutlined />} />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div style={{ padding: 24 }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <Title level={3} style={{ margin: 0 }}>User Management</Title>
          <Text type="secondary">Manage platform accounts, roles, and access</Text>
        </div>
        <Button type="primary" icon={<UserAddOutlined />} onClick={() => {
          createForm.resetFields();
          setCreateOpen(true);
        }}>
          Add User
        </Button>
      </div>

      {/* Summary cards */}
      {summary && (
        <Row gutter={12} style={{ marginBottom: 24 }}>
          <Col span={4}>
            <Card size="small">
              <Statistic title="Total Users" value={summary.total}
                prefix={<TeamOutlined />} />
            </Card>
          </Col>
          <Col span={4}>
            <Card size="small">
              <Statistic title="Active" value={summary.active}
                valueStyle={{ color: 'green' }}
                prefix={<CheckCircleOutlined />} />
            </Card>
          </Col>
          <Col span={4}>
            <Card size="small">
              <Statistic title="Inactive" value={summary.inactive}
                valueStyle={{ color: summary.inactive > 0 ? '#cf1322' : 'inherit' }}
                prefix={<StopOutlined />} />
            </Card>
          </Col>
          {summary.byRole.slice(0, 3).map((r: { role: string; count: number }) => (
            <Col key={r.role} span={4}>
              <Card size="small">
                <Statistic
                  title={ROLE_META[r.role as AppUserRole]?.label ?? r.role}
                  value={r.count}
                />
              </Card>
            </Col>
          ))}
        </Row>
      )}

      {/* Filters */}
      <Card size="small" style={{ marginBottom: 16 }}>
        <Row gutter={12} align="middle">
          <Col>
            <Input
              prefix={<SearchOutlined />}
              placeholder="Search name or email…"
              style={{ width: 220 }}
              value={searchText}
              onChange={e => { setSearchText(e.target.value); setPage(1); }}
              allowClear
            />
          </Col>
          <Col>
            <Select
              allowClear placeholder="Filter by role"
              style={{ width: 170 }}
              value={filterRole}
              onChange={v => { setFilterRole(v); setPage(1); }}
            >
              {ALL_ROLES.map(r => (
                <Option key={r} value={r}>
                  <Tag color={ROLE_META[r].color}>{ROLE_META[r].label}</Tag>
                </Option>
              ))}
            </Select>
          </Col>
          <Col>
            <Select
              allowClear placeholder="Status"
              style={{ width: 120 }}
              value={filterActive === undefined ? undefined : String(filterActive)}
              onChange={v => { setFilterActive(v === undefined ? undefined : v === 'true'); setPage(1); }}
            >
              <Option value="true">Active</Option>
              <Option value="false">Inactive</Option>
            </Select>
          </Col>
          <Col>
            <Button icon={<ReloadOutlined />}
              onClick={() => { setFilterRole(undefined); setFilterActive(undefined); setSearchText(''); setPage(1); }}>
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
        scroll={{ x: 1000 }}
        pagination={{
          current:   page,
          pageSize:  20,
          total:     listData?.totalCount ?? 0,
          onChange:  p => setPage(p),
          showTotal: t => `${t} users`,
        }}
        size="small"
      />

      {/* ── Create user drawer ────────────────────────────────────────── */}
      <Drawer
        title="Add New User"
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        width={480}
        footer={
          <div style={{ textAlign: 'right' }}>
            <Button onClick={() => setCreateOpen(false)} style={{ marginRight: 8 }}>Cancel</Button>
            <Button type="primary" loading={createMut.isPending} onClick={handleCreate}>
              Create User
            </Button>
          </div>
        }
      >
        <Form form={createForm} layout="vertical">
          <Form.Item name="email" label="Email Address"
            rules={[
              { required: true, message: 'Email is required' },
              { type: 'email', message: 'Enter a valid email address' },
            ]}>
            <Input placeholder="user@desicon.com" autoComplete="off" />
          </Form.Item>
          <Form.Item name="fullName" label="Full Name"
            rules={[{ required: true, message: 'Full name is required' }]}>
            <Input placeholder="First Last" />
          </Form.Item>
          <Form.Item name="role" label="Role"
            rules={[{ required: true, message: 'Role is required' }]}>
            <Select placeholder="Select a role">
              {ALL_ROLES.map(r => (
                <Option key={r} value={r}>
                  <Tag color={ROLE_META[r].color}>{ROLE_META[r].label}</Tag>
                </Option>
              ))}
            </Select>
          </Form.Item>
          <Form.Item name="department" label="Department"
            rules={[{ required: true, message: 'Department is required' }]}>
            <Select placeholder="Select department" showSearch>
              {DEPARTMENTS.map(d => <Option key={d} value={d}>{d}</Option>)}
            </Select>
          </Form.Item>
          <Form.Item name="password" label="Initial Password"
            rules={[
              { required: true, message: 'Password is required' },
              { min: 8, message: 'Minimum 8 characters' },
            ]}
            extra="Share this password securely. The user should change it after first login.">
            <Password placeholder="Minimum 8 characters" autoComplete="new-password" />
          </Form.Item>
        </Form>
      </Drawer>

      {/* ── Edit user drawer ──────────────────────────────────────────── */}
      <Drawer
        title={editRecord ? `Edit — ${editRecord.fullName}` : 'Edit User'}
        open={!!editRecord}
        onClose={() => setEditRecord(null)}
        width={420}
        footer={
          <div style={{ textAlign: 'right' }}>
            <Button onClick={() => setEditRecord(null)} style={{ marginRight: 8 }}>Cancel</Button>
            <Button type="primary" loading={updateMut.isPending} onClick={handleUpdate}>
              Save Changes
            </Button>
          </div>
        }
      >
        {editRecord && (
          <Form form={editForm} layout="vertical">
            <Alert
              type="info" showIcon
              message={editRecord.email}
              style={{ marginBottom: 16 }}
            />
            <Form.Item name="fullName" label="Full Name"
              rules={[{ required: true, message: 'Full name is required' }]}>
              <Input />
            </Form.Item>
            <Form.Item name="role" label="Role"
              rules={[{ required: true, message: 'Role is required' }]}>
              <Select>
                {ALL_ROLES.map(r => (
                  <Option key={r} value={r}>
                    <Tag color={ROLE_META[r].color}>{ROLE_META[r].label}</Tag>
                  </Option>
                ))}
              </Select>
            </Form.Item>
            <Form.Item name="department" label="Department"
              rules={[{ required: true, message: 'Department is required' }]}>
              <Select showSearch>
                {DEPARTMENTS.map(d => <Option key={d} value={d}>{d}</Option>)}
              </Select>
            </Form.Item>
          </Form>
        )}
      </Drawer>

      {/* ── View user drawer ──────────────────────────────────────────── */}
      <Drawer
        title={viewRecord ? viewRecord.fullName : 'User Details'}
        open={!!viewRecord}
        onClose={() => setViewRecord(null)}
        width={420}
        extra={
          viewRecord && (
            <Button icon={<EditOutlined />}
              onClick={() => { setViewRecord(null); handleOpenEdit(viewRecord); }}>
              Edit
            </Button>
          )
        }
      >
        {viewRecord && (
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            {/* Avatar */}
            <div style={{ textAlign: 'center', padding: '16px 0 8px' }}>
              <div style={{
                width: 64, height: 64, borderRadius: '50%',
                background: '#1677ff18', margin: '0 auto 8px',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 28, fontWeight: 700, color: '#1677ff',
              }}>
                {viewRecord.fullName.charAt(0).toUpperCase()}
              </div>
              <Title level={4} style={{ margin: 0 }}>{viewRecord.fullName}</Title>
              <Text type="secondary">{viewRecord.email}</Text>
              <div style={{ marginTop: 8 }}>
                <Tag color={ROLE_META[viewRecord.role]?.color ?? 'default'}>
                  {ROLE_META[viewRecord.role]?.label ?? viewRecord.role}
                </Tag>
                {viewRecord.isActive
                  ? <Badge status="success" text="Active" />
                  : <Badge status="error"   text="Inactive" />}
              </div>
            </div>

            <Descriptions bordered size="small" column={1}>
              <Descriptions.Item label="Department">{viewRecord.department}</Descriptions.Item>
              <Descriptions.Item label="Last Login">
                {viewRecord.lastLoginAt
                  ? dayjs(viewRecord.lastLoginAt).format('DD MMM YYYY HH:mm')
                  : 'Never logged in'}
              </Descriptions.Item>
              <Descriptions.Item label="Account Created">
                {dayjs(viewRecord.createdAt).format('DD MMM YYYY')}
              </Descriptions.Item>
              {viewRecord.createdByEmail && (
                <Descriptions.Item label="Created By">
                  {viewRecord.createdByEmail}
                </Descriptions.Item>
              )}
            </Descriptions>

            <div style={{ display: 'flex', gap: 8 }}>
              <Button
                icon={<LockOutlined />}
                style={{ flex: 1 }}
                loading={resetPwMut.isPending}
                onClick={() => { setViewRecord(null); resetPwMut.mutate(viewRecord.id); }}
              >
                Reset Password
              </Button>
              {viewRecord.isActive ? (
                <Popconfirm
                  title={`Deactivate ${viewRecord.fullName}?`}
                  onConfirm={() => { setViewRecord(null); deactivateMut.mutate(viewRecord.id); }}
                  okText="Deactivate" okButtonProps={{ danger: true }}
                >
                  <Button danger icon={<StopOutlined />} style={{ flex: 1 }}>
                    Deactivate
                  </Button>
                </Popconfirm>
              ) : (
                <Button
                  icon={<CheckCircleOutlined />}
                  style={{ flex: 1, color: 'green', borderColor: 'green' }}
                  onClick={() => { setViewRecord(null); activateMut.mutate(viewRecord.id); }}
                >
                  Activate
                </Button>
              )}
            </div>
          </Space>
        )}
      </Drawer>

      {/* ── Reset password result modal ───────────────────────────────── */}
      <Modal
        title={<Space><LockOutlined style={{ color: '#1677ff' }} /> Password Reset</Space>}
        open={!!resetResult}
        onCancel={() => { setResetResult(null); setPwVisible(false); }}
        footer={
          <Button type="primary" onClick={() => { setResetResult(null); setPwVisible(false); }}>
            Done
          </Button>
        }
      >
        {resetResult && (
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <Alert
              type="success" showIcon
              message={`Password reset for ${resetResult.email}`}
              description="Share this temporary password securely. The user should change it immediately after logging in."
            />
            <Card size="small">
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                <div>
                  <Text type="secondary" style={{ fontSize: 11 }}>Temporary Password</Text>
                  <div style={{
                    fontFamily: 'monospace', fontSize: 20, fontWeight: 700,
                    letterSpacing: 2, color: '#1677ff',
                    filter: pwVisible ? 'none' : 'blur(6px)',
                    transition: 'filter 0.2s',
                    userSelect: 'all',
                  }}>
                    {resetResult.temporaryPassword}
                  </div>
                </div>
                <Space direction="vertical" size={4}>
                  <Tooltip title={pwVisible ? 'Hide' : 'Show'}>
                    <Button
                      icon={pwVisible ? <EyeInvisibleOutlined /> : <EyeOutlined />}
                      onClick={() => setPwVisible(v => !v)}
                      size="small"
                    />
                  </Tooltip>
                  <Tooltip title="Copy to clipboard">
                    <Button
                      icon={<CopyOutlined />}
                      size="small"
                      onClick={() => {
                        navigator.clipboard.writeText(resetResult.temporaryPassword);
                        message.success('Copied to clipboard');
                      }}
                    />
                  </Tooltip>
                </Space>
              </div>
            </Card>
            <Paragraph type="secondary" style={{ margin: 0, fontSize: 12 }}>
              ⚠️ This password will not be shown again. Make sure to share it securely before closing this dialog.
            </Paragraph>
          </Space>
        )}
      </Modal>
    </div>
  );
}
