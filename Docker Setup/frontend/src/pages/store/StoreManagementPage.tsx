import { useState } from 'react';
import {
  Alert, Badge, Button, Card, Col, Descriptions, Divider, Drawer,
  Dropdown, Form, Input, InputNumber, message, Modal, Popconfirm, Row, Select,
  Space, Spin, Statistic, Table, Tag, Tabs, Tooltip, Typography,
} from 'antd';
import {
  AppstoreOutlined, CheckCircleOutlined, CloseCircleOutlined,
  DeleteOutlined, DownloadOutlined, EditOutlined,
  EyeOutlined, PlusOutlined, SearchOutlined, ShopOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';

import { storeItemsApi, storeRequisitionsApi } from '../../api/store.api';
import {
  exportInventory,
  exportStoreMovements,
  exportRequisitions,
} from '../../api/export.api';
import { useAuthStore } from '../../store/authStore';
import type {
  StoreItem,
  StoreRequisition,
  StoreRequisitionItem,
  StoreMovement,
  CreateStoreItemPayload,
  UpdateStoreItemPayload,
  RestockPayload,
  CreateStoreRequisitionPayload,
  IssueRequisitionPayload,
} from '../../types';
import { STORE_CATEGORIES, STORE_UNITS } from '../../types';


dayjs.extend(relativeTime);

const { Title, Text } = Typography;
const { Option } = Select;

// ─── Helpers ──────────────────────────────────────────────────────────────────

const fmt = (n: number) =>
  new Intl.NumberFormat('en-NG', { style: 'currency', currency: 'NGN', maximumFractionDigits: 0 }).format(n);

const STATUS_COLORS: Record<string, string> = {
  Pending:  'gold',
  Approved: 'blue',
  Issued:   'green',
  Rejected: 'red',
};

const PRIVILEGED_ROLES = ['SystemAdmin', 'DepartmentManager', 'Supervisor', 'StoreOfficer'];
const APPROVER_ROLES   = ['SystemAdmin', 'DepartmentManager', 'Supervisor'];
const ISSUER_ROLES     = ['SystemAdmin', 'StoreOfficer'];

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function StoreManagementPage() {
  const [activeTab, setActiveTab] = useState('inventory');

  return (
    <div style={{ padding: '24px' }}>
      <Title level={3} style={{ marginBottom: 0 }}>
        <ShopOutlined style={{ marginRight: 8 }} />
        Store Management
      </Title>
      <Text type="secondary" style={{ display: 'block', marginBottom: 24 }}>
        Inventory control, stock movements, and store requisitions
      </Text>

      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        size="large"
        items={[
          {
            key:      'inventory',
            label:    <span><AppstoreOutlined /> Inventory</span>,
            children: <InventoryTab />,
          },
          {
            key:      'requisitions',
            label:    <span><ShopOutlined /> Requisitions</span>,
            children: <RequisitionsTab />,
          },
        ]}
      />
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// TAB 1 — INVENTORY
// ═══════════════════════════════════════════════════════════════════════════════

function InventoryTab() {
  const { user } = useAuthStore();
  const qc = useQueryClient();

  const [search,      setSearch]     = useState('');
  const [filterCat,   setFilterCat]  = useState<string | undefined>();
  const [filterLow,   setFilterLow]  = useState(false);

  const [createOpen,  setCreateOpen]  = useState(false);
  const [editItem,    setEditItem]    = useState<StoreItem | null>(null);
  const [viewItem,    setViewItem]    = useState<StoreItem | null>(null);
  const [restockItem, setRestockItem] = useState<StoreItem | null>(null);
  const [movementsItem, setMovementsItem] = useState<StoreItem | null>(null);

  const [createForm]  = Form.useForm();
  const [editForm]    = Form.useForm();
  const [restockForm] = Form.useForm();

  const [page, setPage] = useState(1);

  const isPrivileged = PRIVILEGED_ROLES.includes(user?.role ?? '');
  const isAdmin      = ['SystemAdmin', 'DepartmentManager'].includes(user?.role ?? '');

  const { data, isLoading } = useQuery({
    queryKey: ['store-items', search, filterCat, filterLow, page],
    queryFn:  () => storeItemsApi.list({
      search:   search || undefined,
      category: filterCat,
      lowStock: filterLow || undefined,
      page,
      pageSize: 30,
    }),
  });

  const { data: movementsData, isLoading: movLoading } = useQuery({
    queryKey: ['store-movements', movementsItem?.id],
    queryFn:  () => storeItemsApi.movements(movementsItem!.id),
    enabled:  !!movementsItem,
  });

  const createMut = useMutation({
    mutationFn: (p: CreateStoreItemPayload) => storeItemsApi.create(p),
    onSuccess:  () => {
      message.success('Item created');
      qc.invalidateQueries({ queryKey: ['store-items'] });
      setCreateOpen(false);
      createForm.resetFields();
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Failed to create item'),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, p }: { id: string; p: UpdateStoreItemPayload }) =>
      storeItemsApi.update(id, p),
    onSuccess: () => {
      message.success('Item updated');
      qc.invalidateQueries({ queryKey: ['store-items'] });
      setEditItem(null);
      editForm.resetFields();
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Update failed'),
  });

  const restockMut = useMutation({
    mutationFn: ({ id, p }: { id: string; p: RestockPayload }) =>
      storeItemsApi.restock(id, p),
    onSuccess: () => {
      message.success('Stock restocked successfully');
      qc.invalidateQueries({ queryKey: ['store-items'] });
      setRestockItem(null);
      restockForm.resetFields();
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Restock failed'),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => storeItemsApi.delete(id),
    onSuccess: (res: any) => {
      message.success(res?.message ?? 'Item removed');
      qc.invalidateQueries({ queryKey: ['store-items'] });
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Delete failed'),
  });

  const columns: ColumnsType<StoreItem> = [
    {
      title: 'Code',
      dataIndex: 'itemCode',
      width: 90,
      render: (v) => <Text code style={{ fontSize: 11 }}>{v}</Text>,
    },
    {
      title: 'Name',
      dataIndex: 'name',
      render: (v, r) => (
        <div>
          <Text strong>{v}</Text>
          {r.isLowStock && (
            <Tag color="red" style={{ marginLeft: 6 }}>
              <WarningOutlined /> Low
            </Tag>
          )}
          {r.storeLocation && (
            <div><Text type="secondary" style={{ fontSize: 11 }}>{r.storeLocation}</Text></div>
          )}
        </div>
      ),
    },
    { title: 'Category',  dataIndex: 'category',       width: 140, render: v => <Tag>{v}</Tag> },
    { title: 'Unit',      dataIndex: 'unit',            width: 80  },
    {
      title: 'In Stock',
      dataIndex: 'quantityInStock',
      width: 90,
      align: 'right',
      render: (v, r) => (
        <Text strong style={{ color: r.isLowStock ? '#cf1322' : undefined }}>
          {v} {r.unit}
        </Text>
      ),
    },
    {
      title: 'Reorder',
      dataIndex: 'reorderLevel',
      width: 80,
      align: 'right',
      render: (v, r) => `${v} ${r.unit}`,
    },
    {
      title: 'Unit Cost',
      dataIndex: 'unitCostNaira',
      width: 110,
      align: 'right',
      render: v => fmt(v),
    },
    {
      title: 'Status',
      dataIndex: 'isActive',
      width: 80,
      render: v => v
        ? <Badge status="success" text="Active" />
        : <Badge status="default" text="Inactive" />,
    },
    {
      title: '',
      key: 'actions',
      width: 140,
      render: (_, r) => (
        <Space size={4}>
          <Tooltip title="View"><Button size="small" icon={<EyeOutlined />} onClick={() => setViewItem(r)} /></Tooltip>
          {isPrivileged && (
            <>
              <Tooltip title="Edit"><Button size="small" icon={<EditOutlined />} onClick={() => { setEditItem(r); editForm.setFieldsValue(r); }} /></Tooltip>
              <Tooltip title="Restock"><Button size="small" type="primary" onClick={() => setRestockItem(r)}>+Stock</Button></Tooltip>
            </>
          )}
          <Tooltip title="Movements"><Button size="small" onClick={() => setMovementsItem(r)}>Hist</Button></Tooltip>
          {isAdmin && (
            <Popconfirm title="Remove this item?" onConfirm={() => deleteMut.mutate(r.id)}>
              <Button size="small" danger icon={<DeleteOutlined />} />
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      {/* KPI cards */}
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col span={6}>
          <Card bordered={false}>
            <Statistic title="Total Items" value={data?.total ?? 0} prefix={<AppstoreOutlined />} />
          </Card>
        </Col>
        <Col span={6}>
          <Card bordered={false}>
            <Statistic title="Low Stock Alerts" value={data?.lowStockCount ?? 0} valueStyle={{ color: '#cf1322' }} prefix={<WarningOutlined />} />
          </Card>
        </Col>
        <Col span={12}>
          <Card bordered={false}>
            <Statistic title="Total Store Value" value={data?.totalStoreValueNaira ?? 0} formatter={v => fmt(Number(v))} />
          </Card>
        </Col>
      </Row>

      {/* Low-stock banner */}
      {(data?.lowStockCount ?? 0) > 0 && (
        <Alert
          type="warning"
          showIcon
          message={`${data!.lowStockCount} item(s) at or below reorder level`}
          action={<Button size="small" onClick={() => setFilterLow(true)}>Show Low Stock</Button>}
          style={{ marginBottom: 16 }}
        />
      )}

      {/* Filters */}
      <Row gutter={12} style={{ marginBottom: 16 }} align="middle">
        <Col flex="auto">
          <Input
            prefix={<SearchOutlined />}
            placeholder="Search by name, code or supplier..."
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }}
            allowClear
          />
        </Col>
        <Col>
          <Select
            placeholder="Category"
            style={{ width: 170 }}
            allowClear
            value={filterCat}
            onChange={v => { setFilterCat(v); setPage(1); }}
          >
            {STORE_CATEGORIES.map(c => <Option key={c} value={c}>{c}</Option>)}
          </Select>
        </Col>
        <Col>
          <Button
            type={filterLow ? 'primary' : 'default'}
            icon={<WarningOutlined />}
            onClick={() => setFilterLow(v => !v)}
          >
            Low Stock
          </Button>
        </Col>
        <Col>
          <Dropdown
            menu={{
              items: [
                {
                  key: 'excel',
                  label: 'Export as Excel (.xlsx)',
                  icon: <DownloadOutlined />,
                  onClick: () => exportInventory({ format: 'excel', category: filterCat, lowStock: filterLow || undefined }),
                },
                {
                  key: 'pdf',
                  label: 'Export as PDF',
                  icon: <DownloadOutlined />,
                  onClick: () => exportInventory({ format: 'pdf', category: filterCat, lowStock: filterLow || undefined }),
                },
                { type: 'divider' },
                {
                  key: 'movements',
                  label: 'Export Movement Log (Excel)',
                  icon: <DownloadOutlined />,
                  onClick: () => exportStoreMovements({}),
                },
              ],
            }}
          >
            <Button icon={<DownloadOutlined />}>Export</Button>
          </Dropdown>
        </Col>
        {isPrivileged && (
          <Col>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
              Add Item
            </Button>
          </Col>
        )}
      </Row>

      <Table
        rowKey="id"
        columns={columns}
        dataSource={data?.items ?? []}
        loading={isLoading}
        size="small"
        scroll={{ x: 'max-content' }}
        pagination={{
          current:   page,
          pageSize:  30,
          total:     data?.total,
          showTotal: (t) => `${t} items`,
          onChange:  setPage,
        }}
        rowClassName={r => r.isLowStock ? 'row-warning' : ''}
      />

      {/* ── Create drawer ──────────────────────────────────────────────────── */}
      <Drawer
        title="Add New Store Item"
        open={createOpen}
        onClose={() => { setCreateOpen(false); createForm.resetFields(); }}
        width={540}
        extra={
          <Button type="primary" onClick={() => createForm.submit()} loading={createMut.isPending}>
            Save Item
          </Button>
        }
      >
        <Form form={createForm} layout="vertical" onFinish={createMut.mutate}>
          <Row gutter={12}>
            <Col span={16}>
              <Form.Item name="name" label="Item Name" rules={[{ required: true }]}>
                <Input placeholder="e.g. Engine Oil Filter (Toyota)" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="unit" label="Unit" rules={[{ required: true }]} initialValue="Pieces">
                <Select>{STORE_UNITS.map(u => <Option key={u} value={u}>{u}</Option>)}</Select>
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="category" label="Category" rules={[{ required: true }]}>
            <Select>
              {STORE_CATEGORIES.map(c => <Option key={c} value={c}>{c}</Option>)}
            </Select>
          </Form.Item>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="quantityInStock" label="Opening Stock" rules={[{ required: true }]} initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="reorderLevel" label="Reorder Level" rules={[{ required: true }]} initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="unitCostNaira" label="Unit Cost (₦)" rules={[{ required: true }]} initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="storeLocation" label="Store Location">
            <Input placeholder="e.g. Store Room A — Shelf 3" />
          </Form.Item>
          <Form.Item name="supplier" label="Supplier">
            <Input placeholder="Supplier name" />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Drawer>

      {/* ── Edit drawer ────────────────────────────────────────────────────── */}
      <Drawer
        title={`Edit: ${editItem?.name}`}
        open={!!editItem}
        onClose={() => { setEditItem(null); editForm.resetFields(); }}
        width={540}
        extra={
          <Button
            type="primary"
            onClick={() => editForm.submit()}
            loading={updateMut.isPending}
          >
            Save Changes
          </Button>
        }
      >
        <Form
          form={editForm}
          layout="vertical"
          onFinish={v => updateMut.mutate({ id: editItem!.id, p: v as UpdateStoreItemPayload })}
        >
          <Row gutter={12}>
            <Col span={16}>
              <Form.Item name="name" label="Item Name" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="unit" label="Unit" rules={[{ required: true }]}>
                <Select>{STORE_UNITS.map(u => <Option key={u} value={u}>{u}</Option>)}</Select>
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="category" label="Category" rules={[{ required: true }]}>
            <Select>{STORE_CATEGORIES.map(c => <Option key={c} value={c}>{c}</Option>)}</Select>
          </Form.Item>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="reorderLevel" label="Reorder Level" rules={[{ required: true }]}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="unitCostNaira" label="Unit Cost (₦)" rules={[{ required: true }]}>
                <InputNumber min={0} style={{ width: '100%' }} formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="storeLocation" label="Store Location">
            <Input />
          </Form.Item>
          <Form.Item name="supplier" label="Supplier">
            <Input />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="isActive" label="Status" initialValue={true}>
            <Select>
              <Option value={true}>Active</Option>
              <Option value={false}>Inactive</Option>
            </Select>
          </Form.Item>
        </Form>
      </Drawer>

      {/* ── Restock modal ──────────────────────────────────────────────────── */}
      <Modal
        title={`Restock: ${restockItem?.name}`}
        open={!!restockItem}
        onCancel={() => { setRestockItem(null); restockForm.resetFields(); }}
        onOk={() => restockForm.submit()}
        confirmLoading={restockMut.isPending}
        okText="Confirm Restock"
      >
        {restockItem && (
          <div style={{ marginBottom: 16 }}>
            <Text>Current stock: </Text>
            <Text strong>{restockItem.quantityInStock} {restockItem.unit}</Text>
          </div>
        )}
        <Form
          form={restockForm}
          layout="vertical"
          onFinish={v => restockMut.mutate({ id: restockItem!.id, p: v as RestockPayload })}
        >
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="quantity" label="Quantity Received" rules={[{ required: true }]}>
                <InputNumber min={0.01} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="unitCostNaira" label="New Unit Cost (₦)" rules={[{ required: true }]}
                initialValue={restockItem?.unitCostNaira}>
                <InputNumber min={0} style={{ width: '100%' }} formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="reference" label="Reference (e.g. LPO number)">
            <Input placeholder="LPO/26/001" />
          </Form.Item>
          <Form.Item name="notes" label="Notes">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      {/* ── View drawer ────────────────────────────────────────────────────── */}
      <Drawer
        title={viewItem?.name}
        open={!!viewItem}
        onClose={() => setViewItem(null)}
        width={520}
      >
        {viewItem && (
          <Descriptions bordered column={1} size="small">
            <Descriptions.Item label="Item Code">{viewItem.itemCode}</Descriptions.Item>
            <Descriptions.Item label="Category"><Tag>{viewItem.category}</Tag></Descriptions.Item>
            <Descriptions.Item label="Unit">{viewItem.unit}</Descriptions.Item>
            <Descriptions.Item label="In Stock">
              <Text strong style={{ color: viewItem.isLowStock ? '#cf1322' : undefined }}>
                {viewItem.quantityInStock} {viewItem.unit}
                {viewItem.isLowStock && <Tag color="red" style={{ marginLeft: 8 }}><WarningOutlined /> Low Stock</Tag>}
              </Text>
            </Descriptions.Item>
            <Descriptions.Item label="Reorder Level">{viewItem.reorderLevel} {viewItem.unit}</Descriptions.Item>
            <Descriptions.Item label="Unit Cost">{fmt(viewItem.unitCostNaira)}</Descriptions.Item>
            <Descriptions.Item label="Total Value">{fmt(viewItem.totalValueNaira)}</Descriptions.Item>
            <Descriptions.Item label="Store Location">{viewItem.storeLocation || '—'}</Descriptions.Item>
            <Descriptions.Item label="Supplier">{viewItem.supplier || '—'}</Descriptions.Item>
            <Descriptions.Item label="Description">{viewItem.description || '—'}</Descriptions.Item>
            <Descriptions.Item label="Status">
              {viewItem.isActive ? <Badge status="success" text="Active" /> : <Badge status="default" text="Inactive" />}
            </Descriptions.Item>
            <Descriptions.Item label="Created By">{viewItem.createdByEmail}</Descriptions.Item>
            <Descriptions.Item label="Last Updated">{dayjs(viewItem.updatedAt).format('DD MMM YYYY HH:mm')}</Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>

      {/* ── Movements drawer ──────────────────────────────────────────────── */}
      <Drawer
        title={`Stock Movements: ${movementsItem?.name}`}
        open={!!movementsItem}
        onClose={() => setMovementsItem(null)}
        width={700}
      >
        {movLoading ? <Spin /> : (
          <Table
            rowKey="id"
            size="small"
            dataSource={movementsData ?? []}
            pagination={false}
            scroll={{ x: 'max-content' }}
            columns={[
              {
                title: 'Date',
                dataIndex: 'createdAt',
                width: 140,
                render: v => dayjs(v).format('DD MMM YY HH:mm'),
              },
              {
                title: 'Type',
                dataIndex: 'movementType',
                width: 100,
                render: v => (
                  <Tag color={v === 'Receipt' || v === 'Return' ? 'green' : v === 'Issue' ? 'red' : 'orange'}>{v}</Tag>
                ),
              },
              {
                title: 'Change',
                dataIndex: 'quantityChange',
                width: 90,
                align: 'right',
                render: v => (
                  <Text strong style={{ color: v >= 0 ? '#389e0d' : '#cf1322' }}>
                    {v >= 0 ? '+' : ''}{v}
                  </Text>
                ),
              },
              { title: 'After', dataIndex: 'quantityAfter', width: 80, align: 'right' },
              { title: 'Reference', dataIndex: 'reference', render: v => v || '—' },
              { title: 'By', dataIndex: 'movedByName', width: 120 },
            ] as ColumnsType<StoreMovement>}
          />
        )}
      </Drawer>
    </>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// TAB 2 — REQUISITIONS
// ═══════════════════════════════════════════════════════════════════════════════

function RequisitionsTab() {
  const { user } = useAuthStore();
  const qc = useQueryClient();

  const [filterStatus, setFilterStatus] = useState<string | undefined>();
  const [createOpen, setCreateOpen] = useState(false);
  const [viewReq,    setViewReq]    = useState<StoreRequisition | null>(null);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectTarget, setRejectTarget] = useState<StoreRequisition | null>(null);
  const [issueOpen, setIssueOpen]   = useState(false);
  const [issueTarget, setIssueTarget] = useState<StoreRequisition | null>(null);

  const [createForm]  = Form.useForm();
  const [rejectForm]  = Form.useForm();
  const [issueForm]   = Form.useForm();

  const [lineItems, setLineItems] = useState<Array<{ itemId: string; qty: number }>>([]);
  const [page, setPage] = useState(1);

  const isApprover = APPROVER_ROLES.includes(user?.role ?? '');
  const isIssuer   = ISSUER_ROLES.includes(user?.role ?? '');

  // Load all items for the create form dropdown
  const { data: allItemsData } = useQuery({
    queryKey: ['store-items-all'],
    queryFn:  () => storeItemsApi.list({ isActive: true, pageSize: 500 }),
  });

  const { data, isLoading } = useQuery({
    queryKey: ['store-requisitions', filterStatus, page],
    queryFn:  () => storeRequisitionsApi.list({ status: filterStatus, page, pageSize: 30 }),
  });

  const createMut = useMutation({
    mutationFn: (p: CreateStoreRequisitionPayload) => storeRequisitionsApi.create(p),
    onSuccess:  () => {
      message.success('Requisition submitted');
      qc.invalidateQueries({ queryKey: ['store-requisitions'] });
      setCreateOpen(false);
      createForm.resetFields();
      setLineItems([]);
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Submit failed'),
  });

  const approveMut = useMutation({
    mutationFn: (id: string) => storeRequisitionsApi.approve(id),
    onSuccess:  () => {
      message.success('Requisition approved');
      qc.invalidateQueries({ queryKey: ['store-requisitions'] });
      if (viewReq) {
        storeRequisitionsApi.getById(viewReq.id).then(setViewReq);
      }
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Approve failed'),
  });

  const rejectMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      storeRequisitionsApi.reject(id, reason),
    onSuccess: () => {
      message.success('Requisition rejected');
      qc.invalidateQueries({ queryKey: ['store-requisitions'] });
      setRejectOpen(false);
      rejectForm.resetFields();
      if (viewReq) storeRequisitionsApi.getById(viewReq.id).then(setViewReq);
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Reject failed'),
  });

  const issueMut = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: IssueRequisitionPayload }) =>
      storeRequisitionsApi.issue(id, payload),
    onSuccess: () => {
      message.success('Items issued successfully');
      qc.invalidateQueries({ queryKey: ['store-requisitions'] });
      qc.invalidateQueries({ queryKey: ['store-items'] });
      setIssueOpen(false);
      issueForm.resetFields();
      if (viewReq) storeRequisitionsApi.getById(viewReq.id).then(setViewReq);
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Issue failed'),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => storeRequisitionsApi.delete(id),
    onSuccess:  () => {
      message.success('Requisition deleted');
      qc.invalidateQueries({ queryKey: ['store-requisitions'] });
    },
    onError: (e: any) => message.error(e?.response?.data?.message ?? 'Delete failed'),
  });

  const columns: ColumnsType<StoreRequisition> = [
    {
      title: 'Number',
      dataIndex: 'requisitionNumber',
      width: 110,
      render: v => <Text code style={{ fontSize: 11 }}>{v}</Text>,
    },
    {
      title: 'Requested By',
      dataIndex: 'requestedByName',
      render: (v, r) => (
        <div>
          <Text strong>{v}</Text>
          <div><Text type="secondary" style={{ fontSize: 11 }}>{r.department}</Text></div>
        </div>
      ),
    },
    {
      title: 'Purpose',
      dataIndex: 'purpose',
      ellipsis: true,
    },
    { title: 'Items', key: 'items', width: 60, render: (_, r) => r.items.length },
    {
      title: 'Status',
      dataIndex: 'status',
      width: 90,
      render: v => <Tag color={STATUS_COLORS[v] ?? 'default'}>{v}</Tag>,
    },
    {
      title: 'Submitted',
      dataIndex: 'createdAt',
      width: 100,
      render: v => dayjs(v).fromNow(),
    },
    {
      title: '',
      key: 'actions',
      width: 160,
      render: (_, r) => (
        <Space size={4} wrap>
          <Tooltip title="View">
            <Button size="small" icon={<EyeOutlined />} onClick={() => setViewReq(r)} />
          </Tooltip>
          {r.status === 'Pending' && isApprover && (
            <>
              <Tooltip title="Approve">
                <Button size="small" type="primary" icon={<CheckCircleOutlined />}
                  onClick={() => approveMut.mutate(r.id)}
                  loading={approveMut.isPending}
                />
              </Tooltip>
              <Tooltip title="Reject">
                <Button size="small" danger icon={<CloseCircleOutlined />}
                  onClick={() => { setRejectTarget(r); setRejectOpen(true); }}
                />
              </Tooltip>
            </>
          )}
          {r.status === 'Approved' && isIssuer && (
            <Tooltip title="Issue Items">
              <Button size="small" type="primary" style={{ background: '#52c41a', borderColor: '#52c41a' }}
                onClick={() => { setIssueTarget(r); setIssueOpen(true); }}
              >
                Issue
              </Button>
            </Tooltip>
          )}
          {r.status === 'Pending' && (r.requestedByEmail === user?.email || isApprover) && (
            <Popconfirm title="Delete this requisition?" onConfirm={() => deleteMut.mutate(r.id)}>
              <Tooltip title="Delete">
                <Button size="small" danger icon={<DeleteOutlined />} />
              </Tooltip>
            </Popconfirm>
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      {/* Summary cards */}
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col span={6}>
          <Card bordered={false}>
            <Statistic title="Pending" value={data?.pendingCount ?? 0} valueStyle={{ color: '#d48806' }} />
          </Card>
        </Col>
        <Col span={6}>
          <Card bordered={false}>
            <Statistic title="Approved (awaiting issue)" value={data?.approvedCount ?? 0} valueStyle={{ color: '#1677ff' }} />
          </Card>
        </Col>
        <Col span={6}>
          <Card bordered={false}>
            <Statistic title="Total (this view)" value={data?.total ?? 0} />
          </Card>
        </Col>
        <Col span={6} style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 8 }}>
          <Dropdown
            menu={{
              items: [
                {
                  key: 'excel',
                  label: 'Export as Excel (.xlsx)',
                  icon: <DownloadOutlined />,
                  onClick: () => exportRequisitions({ format: 'excel', status: filterStatus }),
                },
                {
                  key: 'pdf',
                  label: 'Export as PDF',
                  icon: <DownloadOutlined />,
                  onClick: () => exportRequisitions({ format: 'pdf', status: filterStatus }),
                },
              ],
            }}
          >
            <Button icon={<DownloadOutlined />}>Export</Button>
          </Dropdown>
          <Button type="primary" icon={<PlusOutlined />} size="large" onClick={() => setCreateOpen(true)}>
            New Requisition
          </Button>
        </Col>
      </Row>

      {/* Filters */}
      <Row gutter={12} style={{ marginBottom: 16 }}>
        <Col>
          <Select
            placeholder="Filter by status"
            style={{ width: 160 }}
            allowClear
            value={filterStatus}
            onChange={v => { setFilterStatus(v); setPage(1); }}
          >
            {['Pending','Approved','Issued','Rejected'].map(s => (
              <Option key={s} value={s}><Tag color={STATUS_COLORS[s]}>{s}</Tag></Option>
            ))}
          </Select>
        </Col>
      </Row>

      <Table
        rowKey="id"
        columns={columns}
        dataSource={data?.items ?? []}
        loading={isLoading}
        size="small"
        scroll={{ x: 'max-content' }}
        pagination={{
          current:   page,
          pageSize:  30,
          total:     data?.total,
          showTotal: t => `${t} requisitions`,
          onChange:  setPage,
        }}
      />

      {/* ── Create Requisition drawer ──────────────────────────────────────── */}
      <Drawer
        title="New Store Requisition"
        open={createOpen}
        onClose={() => { setCreateOpen(false); createForm.resetFields(); setLineItems([]); }}
        width={680}
        extra={
          <Button type="primary" onClick={() => createForm.submit()} loading={createMut.isPending}>
            Submit Requisition
          </Button>
        }
      >
        <Form
          form={createForm}
          layout="vertical"
          onFinish={values => {
            const items = lineItems
              .filter(li => li.itemId && li.qty > 0)
              .map(li => ({ storeItemId: li.itemId, quantityRequested: li.qty }));

            if (items.length === 0) {
              message.error('Please add at least one item.');
              return;
            }

            createMut.mutate({
              purpose:         values.purpose,
              linkedReference: values.linkedReference || undefined,
              notes:           values.notes || undefined,
              items,
            });
          }}
        >
          <Form.Item name="purpose" label="Purpose / Reason" rules={[{ required: true }]}>
            <Input.TextArea rows={2} placeholder="What will these items be used for?" />
          </Form.Item>
          <Form.Item name="linkedReference" label="Linked Reference (optional)">
            <Input placeholder="e.g. E/26/005, V/26/002" />
          </Form.Item>

          <Divider titlePlacement="left">Items to Requisition</Divider>

          {lineItems.map((li, idx) => (
            <Row key={idx} gutter={8} align="middle" style={{ marginBottom: 8 }}>
              <Col flex="auto">
                <Select
                  showSearch
                  placeholder="Select item"
                  style={{ width: '100%' }}
                  value={li.itemId || undefined}
                  onChange={v => setLineItems(prev => {
                    const n = [...prev];
                    n[idx] = { ...n[idx], itemId: v };
                    return n;
                  })}
                  filterOption={(input, opt) =>
                    (opt?.label as string ?? '').toLowerCase().includes(input.toLowerCase())
                  }
                  options={allItemsData?.items.map(i => ({
                    value: i.id,
                    label: `${i.itemCode} — ${i.name} (${i.quantityInStock} ${i.unit} in stock)`,
                  }))}
                />
              </Col>
              <Col style={{ width: 100 }}>
                <InputNumber
                  min={1}
                  placeholder="Qty"
                  style={{ width: '100%' }}
                  value={li.qty || undefined}
                  onChange={v => setLineItems(prev => {
                    const n = [...prev];
                    n[idx] = { ...n[idx], qty: v ?? 0 };
                    return n;
                  })}
                />
              </Col>
              <Col>
                <Button
                  danger
                  icon={<DeleteOutlined />}
                  size="small"
                  onClick={() => setLineItems(prev => prev.filter((_, i) => i !== idx))}
                />
              </Col>
            </Row>
          ))}

          <Button
            type="dashed"
            block
            icon={<PlusOutlined />}
            onClick={() => setLineItems(prev => [...prev, { itemId: '', qty: 0 }])}
            style={{ marginBottom: 16 }}
          >
            Add Item
          </Button>

          <Form.Item name="notes" label="Notes">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Drawer>

      {/* ── Reject modal ──────────────────────────────────────────────────── */}
      <Modal
        title="Reject Requisition"
        open={rejectOpen}
        onCancel={() => { setRejectOpen(false); rejectForm.resetFields(); setRejectTarget(null); }}
        onOk={() => rejectForm.submit()}
        confirmLoading={rejectMut.isPending}
        okText="Reject"
        okButtonProps={{ danger: true }}
      >
        <Form
          form={rejectForm}
          layout="vertical"
          onFinish={v => rejectMut.mutate({ id: rejectTarget!.id, reason: v.reason })}
        >
          <Form.Item name="reason" label="Rejection Reason" rules={[{ required: true }]}>
            <Input.TextArea rows={3} placeholder="Explain why this requisition is being rejected" />
          </Form.Item>
        </Form>
      </Modal>

      {/* ── Issue modal ───────────────────────────────────────────────────── */}
      <Modal
        title={`Issue Items: ${issueTarget?.requisitionNumber}`}
        open={issueOpen}
        onCancel={() => { setIssueOpen(false); issueForm.resetFields(); setIssueTarget(null); }}
        onOk={() => issueForm.submit()}
        confirmLoading={issueMut.isPending}
        okText="Confirm Issue"
        width={640}
      >
        <Alert
          type="info"
          showIcon
          message="Set the actual quantity issued for each line. Stock will be decremented accordingly."
          style={{ marginBottom: 16 }}
        />
        <Form
          form={issueForm}
          layout="vertical"
          onFinish={v => {
            const items = (issueTarget?.items ?? []).map(li => ({
              storeRequisitionItemId: li.id,
              quantityIssued: v[`qty_${li.id}`] ?? 0,
            }));
            issueMut.mutate({
              id: issueTarget!.id,
              payload: { items, notes: v.notes },
            });
          }}
        >
          {(issueTarget?.items ?? []).map(li => (
            <Row key={li.id} gutter={12} align="middle" style={{ marginBottom: 8 }}>
              <Col flex="auto">
                <Text>
                  <Text code style={{ fontSize: 11, marginRight: 8 }}>{li.itemCode}</Text>
                  {li.itemName}
                  <Text type="secondary" style={{ marginLeft: 8 }}>
                    (Stock: {li.currentStock} {li.unit})
                  </Text>
                </Text>
              </Col>
              <Col style={{ width: 140 }}>
                <Form.Item
                  name={`qty_${li.id}`}
                  style={{ margin: 0 }}
                  initialValue={Math.min(li.quantityRequested, li.currentStock)}
                  rules={[
                    { required: true, message: 'Required' },
                    {
                      validator: (_, v) =>
                        v >= 0 && v <= li.quantityRequested
                          ? Promise.resolve()
                          : Promise.reject(`Max ${li.quantityRequested}`),
                    },
                  ]}
                >
                  <InputNumber
                    min={0}
                    max={li.quantityRequested}
                    addonBefore="Qty"
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              </Col>
            </Row>
          ))}
          <Form.Item name="notes" label="Notes" style={{ marginTop: 16 }}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      {/* ── View Requisition drawer ───────────────────────────────────────── */}
      <Drawer
        title={
          <Space>
            {viewReq?.requisitionNumber}
            {viewReq && <Tag color={STATUS_COLORS[viewReq.status]}>{viewReq.status}</Tag>}
          </Space>
        }
        open={!!viewReq}
        onClose={() => setViewReq(null)}
        width={680}
        extra={
          viewReq && (
            <Space>
              {viewReq.status === 'Pending' && isApprover && (
                <>
                  <Button
                    type="primary"
                    icon={<CheckCircleOutlined />}
                    loading={approveMut.isPending}
                    onClick={() => approveMut.mutate(viewReq.id)}
                  >
                    Approve
                  </Button>
                  <Button
                    danger
                    icon={<CloseCircleOutlined />}
                    onClick={() => { setRejectTarget(viewReq); setRejectOpen(true); }}
                  >
                    Reject
                  </Button>
                </>
              )}
              {viewReq.status === 'Approved' && isIssuer && (
                <Button
                  type="primary"
                  style={{ background: '#52c41a', borderColor: '#52c41a' }}
                  onClick={() => { setIssueTarget(viewReq); setIssueOpen(true); }}
                >
                  Issue Items
                </Button>
              )}
            </Space>
          )
        }
      >
        {viewReq && (
          <>
            <Descriptions bordered column={2} size="small" style={{ marginBottom: 16 }}>
              <Descriptions.Item label="Requested By" span={2}>{viewReq.requestedByName}</Descriptions.Item>
              <Descriptions.Item label="Department">{viewReq.department}</Descriptions.Item>
              <Descriptions.Item label="Submitted">{dayjs(viewReq.createdAt).format('DD MMM YYYY HH:mm')}</Descriptions.Item>
              <Descriptions.Item label="Purpose" span={2}>{viewReq.purpose}</Descriptions.Item>
              {viewReq.linkedReference && (
                <Descriptions.Item label="Linked Reference" span={2}>
                  <Text code>{viewReq.linkedReference}</Text>
                </Descriptions.Item>
              )}
              {viewReq.approvedByName && (
                <Descriptions.Item label="Approved By">
                  {viewReq.approvedByName} • {dayjs(viewReq.approvedAt).format('DD MMM YY')}
                </Descriptions.Item>
              )}
              {viewReq.issuedByName && (
                <Descriptions.Item label="Issued By">
                  {viewReq.issuedByName} • {dayjs(viewReq.issuedAt).format('DD MMM YY')}
                </Descriptions.Item>
              )}
              {viewReq.rejectionReason && (
                <Descriptions.Item label="Rejection Reason" span={2}>
                  <Text type="danger">{viewReq.rejectionReason}</Text>
                </Descriptions.Item>
              )}
              {viewReq.notes && (
                <Descriptions.Item label="Notes" span={2}>{viewReq.notes}</Descriptions.Item>
              )}
            </Descriptions>

            <Divider titlePlacement="left">Line Items</Divider>
            <Table
              rowKey="id"
              size="small"
              dataSource={viewReq.items}
              pagination={false}
              scroll={{ x: 'max-content' }}
              columns={[
                { title: 'Code',     dataIndex: 'itemCode',          width: 90,  render: v => <Text code style={{ fontSize: 11 }}>{v}</Text> },
                { title: 'Item',     dataIndex: 'itemName' },
                { title: 'Unit',     dataIndex: 'unit',              width: 70  },
                { title: 'Requested',dataIndex: 'quantityRequested', width: 90, align: 'right' },
                {
                  title: 'Issued',
                  dataIndex: 'quantityIssued',
                  width: 80,
                  align: 'right',
                  render: (v, r) => (
                    <Text style={{ color: v < r.quantityRequested && viewReq.status === 'Issued' ? '#d48806' : undefined }}>
                      {v}
                    </Text>
                  ),
                },
                { title: 'Unit Cost',   dataIndex: 'unitCostNaira', width: 100, align: 'right', render: v => fmt(v) },
                { title: 'Total Cost',  dataIndex: 'totalCost',     width: 110, align: 'right', render: v => fmt(v) },
              ] as ColumnsType<StoreRequisitionItem>}
              summary={() => (
                <Table.Summary.Row>
                  <Table.Summary.Cell index={0} colSpan={6} align="right">
                    <Text strong>Total Cost:</Text>
                  </Table.Summary.Cell>
                  <Table.Summary.Cell index={1} align="right">
                    <Text strong>{fmt(viewReq.totalCostNaira)}</Text>
                  </Table.Summary.Cell>
                </Table.Summary.Row>
              )}
            />
          </>
        )}
      </Drawer>
    </>
  );
}
