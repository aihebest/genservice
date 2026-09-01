import { useState } from 'react';
import {
  Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Row,
  Space, Statistic, Table, Tabs, Tag, Tooltip, Typography, message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, EditOutlined, DeleteOutlined, ReloadOutlined,
  DownloadOutlined, SettingOutlined, CoffeeOutlined, TeamOutlined, WalletOutlined,
} from '@ant-design/icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { useAuthStore } from '../../store/authStore';
import { feedingLogApi, downloadFeedingLogExport } from '../../api/feedingLog.api';
import type {
  FeedingLogEntry, FeedingLogPayload, FeedingSummaryRow, MealRate,
} from '../../api/feedingLog.api';

const { Text } = Typography;
const { TextArea } = Input;

const naira = (v?: number) => v != null ? `₦${Number(v).toLocaleString()}` : '—';

// Meal item columns, in the same order as their spreadsheet.
const ITEMS: { key: keyof FeedingLogPayload; field: keyof FeedingLogEntry; label: string }[] = [
  { key: 'breakfast', field: 'breakfast', label: 'Breakfast'  },
  { key: 'softDrink', field: 'softDrink', label: 'Soft Drink' },
  { key: 'water',     field: 'water',     label: 'Water'      },
  { key: 'juice',     field: 'juice',     label: 'Juice'      },
  { key: 'lunch',     field: 'lunch',     label: 'Lunch'      },
  { key: 'dinner',    field: 'dinner',    label: 'Dinner'     },
  { key: 'tea',       field: 'tea',       label: 'Tea'        },
  { key: 'snacks',    field: 'snacks',    label: 'Snacks'     },
];

export default function FeedingLogTab() {
  const qc = useQueryClient();
  const role = useAuthStore(s => s.user?.role);
  const canEdit = role === 'DepartmentManager' || role === 'SystemAdmin';

  const [from, setFrom]         = useState<string | undefined>();
  const [to, setTo]             = useState<string | undefined>();
  const [search, setSearch]     = useState<string | undefined>();
  const [page, setPage]         = useState(1);
  const [open, setOpen]         = useState(false);
  const [editing, setEditing]   = useState<FeedingLogEntry | null>(null);
  const [ratesOpen, setRatesOpen] = useState(false);
  const [exporting, setExporting] = useState(false);

  const [form]      = Form.useForm();
  const [ratesForm] = Form.useForm();

  const params = { from, to, search, page, pageSize: 50 };

  const { data: stats }   = useQuery({ queryKey: ['feeding-log', 'stats'], queryFn: feedingLogApi.stats, refetchInterval: 60_000 });
  const { data: rates }   = useQuery({ queryKey: ['feeding-log', 'rates'], queryFn: feedingLogApi.rates });
  const { data, isFetching } = useQuery({
    queryKey: ['feeding-log', 'list', from, to, search, page],
    queryFn:  () => feedingLogApi.list(params),
  });
  const { data: summary } = useQuery({
    queryKey: ['feeding-log', 'summary', from, to, search],
    queryFn:  () => feedingLogApi.summary({ from, to, search }),
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ['feeding-log'] });

  const saveMut = useMutation({
    mutationFn: (p: { id?: string; payload: FeedingLogPayload }) =>
      p.id ? feedingLogApi.update(p.id, p.payload) : feedingLogApi.create(p.payload),
    onSuccess: () => {
      message.success(editing ? 'Entry updated' : 'Feeding entry recorded');
      invalidate(); setOpen(false); setEditing(null); form.resetFields();
    },
    onError: (e: { response?: { data?: { message?: string } } }) =>
      message.error(e?.response?.data?.message ?? 'Failed to save'),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => feedingLogApi.remove(id),
    onSuccess: () => { message.success('Entry deleted'); invalidate(); },
    onError: () => message.error('Failed to delete'),
  });

  const ratesMut = useMutation({
    mutationFn: (r: { itemKey: string; unitPriceNaira: number }[]) => feedingLogApi.updateRates(r),
    onSuccess: () => { message.success('Meal rates updated'); invalidate(); setRatesOpen(false); },
    onError: (e: { response?: { data?: { message?: string } } }) =>
      message.error(e?.response?.data?.message ?? 'Failed to update rates'),
  });

  // Live cost preview while filling the form, using current rates.
  const watched = Form.useWatch([], form) as Record<string, unknown> | undefined;
  const livePreview = (() => {
    if (!rates || !watched) return 0;
    return ITEMS.reduce((sum, it) => {
      const qty  = Number(watched[it.key] ?? 0);
      const rate = rates.find(r => r.itemKey.toLowerCase() === String(it.key).toLowerCase())?.unitPriceNaira ?? 0;
      return sum + (qty > 0 ? qty * rate : 0);
    }, 0);
  })();

  const openCreate = () => {
    setEditing(null); form.resetFields();
    form.setFieldsValue({ entryDate: dayjs() });
    setOpen(true);
  };

  const openEdit = (r: FeedingLogEntry) => {
    setEditing(r);
    form.setFieldsValue({
      entryDate: dayjs(r.entryDate), staffName: r.staffName, projectCostCode: r.projectCostCode,
      breakfast: r.breakfast, softDrink: r.softDrink, water: r.water, juice: r.juice,
      lunch: r.lunch, dinner: r.dinner, tea: r.tea, snacks: r.snacks, notes: r.notes,
    });
    setOpen(true);
  };

  const handleSave = async () => {
    try {
      const v = await form.validateFields();
      const payload: FeedingLogPayload = {
        entryDate:       (v.entryDate as dayjs.Dayjs).format('YYYY-MM-DD'),
        staffName:       (v.staffName as string).trim(),
        projectCostCode: (v.projectCostCode as string | undefined)?.trim() || undefined,
        breakfast: v.breakfast ?? 0, softDrink: v.softDrink ?? 0,
        water:     v.water     ?? 0, juice:     v.juice     ?? 0,
        lunch:     v.lunch     ?? 0, dinner:    v.dinner    ?? 0,
        tea:       v.tea       ?? 0, snacks:    v.snacks    ?? 0,
        notes:     (v.notes as string | undefined)?.trim() || undefined,
      };
      saveMut.mutate({ id: editing?.id, payload });
    } catch { /* validation */ }
  };

  const handleExport = async () => {
    setExporting(true);
    try { await downloadFeedingLogExport({ from, to, search }); }
    catch { message.error('Export failed'); }
    finally { setExporting(false); }
  };

  const openRates = () => {
    const vals: Record<string, number> = {};
    (rates ?? []).forEach(r => { vals[r.itemKey] = r.unitPriceNaira; });
    ratesForm.setFieldsValue(vals);
    setRatesOpen(true);
  };

  const columns: ColumnsType<FeedingLogEntry> = [
    { title: 'Date', dataIndex: 'entryDate', width: 105, fixed: 'left' as const,
      render: (v: string) => dayjs(v).format('D MMM YY') },
    { title: 'Name', dataIndex: 'staffName', width: 150, fixed: 'left' as const, ellipsis: true,
      render: (v: string) => v?.toLowerCase() === 'extra'
        ? <Text strong style={{ color: '#cf1322' }}>{v}</Text>
        : <Text>{v}</Text> },
    { title: 'Cost Code', dataIndex: 'projectCostCode', width: 110,
      render: (v?: string) => v ? <Tag>{v}</Tag> : <Text type="secondary">—</Text> },
    ...ITEMS.map(it => ({
      title: it.label, dataIndex: it.field, key: it.field as string, width: 92, align: 'center' as const,
      render: (v: number) => v > 0 ? <Text>{v}</Text> : <Text type="secondary" style={{ opacity: 0.35 }}>—</Text>,
    })),
    { title: 'Total (₦)', dataIndex: 'totalCostNaira', width: 120, align: 'right' as const,
      render: (v: number) => <Text strong style={{ color: '#389e0d' }}>{naira(v)}</Text> },
    { title: 'Logged By', key: 'by', width: 145, ellipsis: true,
      render: (_: unknown, r: FeedingLogEntry) => (
        <span>
          <Text style={{ fontSize: 12 }}>{r.loggedByName}</Text>
          {r.lastEditedByName && (
            <Tooltip title={`Edited by ${r.lastEditedByName}${r.lastEditedAt ? ` on ${dayjs(r.lastEditedAt).format('D MMM YY HH:mm')}` : ''}`}>
              <Tag color="gold" style={{ marginLeft: 4, fontSize: 10 }}>edited</Tag>
            </Tooltip>
          )}
        </span>
      ) },
    { title: '', key: 'act', width: 90, fixed: 'right' as const,
      render: (_: unknown, r: FeedingLogEntry) => canEdit ? (
        <Space size={4}>
          <Tooltip title="Edit entry"><Button size="small" icon={<EditOutlined />} onClick={() => openEdit(r)} /></Tooltip>
          <Popconfirm title="Delete this entry?" okText="Delete" okButtonProps={{ danger: true }}
            onConfirm={() => deleteMut.mutate(r.id)}>
            <Button size="small" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ) : <Text type="secondary" style={{ fontSize: 11 }}>—</Text> },
  ];

  const summaryColumns = (keyTitle: string): ColumnsType<FeedingSummaryRow> => [
    { title: keyTitle, dataIndex: 'key', width: 190, fixed: 'left' as const,
      render: (v: string) => <Text strong>{v}</Text> },
    ...ITEMS.map(it => ({
      title: it.label, dataIndex: it.key as string, key: it.key as string, width: 92, align: 'center' as const,
      render: (v: number) => v > 0 ? v : <Text type="secondary" style={{ opacity: 0.35 }}>—</Text>,
    })),
    { title: 'Total Cost (₦)', dataIndex: 'totalCostNaira', width: 140, align: 'right' as const,
      render: (v: number) => <Text strong style={{ color: '#389e0d' }}>{naira(v)}</Text> },
  ];

  const grand = summary?.grandTotal;

  return (
    <div style={{ paddingTop: 8 }}>
      <Row gutter={12} style={{ marginBottom: 16 }}>
        <Col span={6}><Card size="small"><Statistic title="Entries This Month" value={stats?.entriesThisMonth ?? 0} prefix={<CoffeeOutlined style={{ color: '#fa8c16' }} />} /></Card></Col>
        <Col span={6}><Card size="small"><Statistic title="Staff Fed This Month" value={stats?.staffFedThisMonth ?? 0} prefix={<TeamOutlined style={{ color: '#1677ff' }} />} /></Card></Col>
        <Col span={6}><Card size="small"><Statistic title="Meal Items This Month" value={stats?.mealsThisMonth ?? 0} /></Card></Col>
        <Col span={6}><Card size="small"><Statistic title="Cost This Month" value={stats?.costThisMonth ?? 0} prefix={<WalletOutlined style={{ color: '#722ed1' }} />} formatter={v => `₦${Number(v).toLocaleString()}`} /></Card></Col>
      </Row>

      <Card size="small" style={{ marginBottom: 12 }}>
        <Row gutter={[12, 12]} align="middle">
          <Col>
            <DatePicker.RangePicker allowEmpty={[true, true]}
              onChange={vals => {
                const [a, b] = vals ?? [null, null];
                setFrom(a ? a.format('YYYY-MM-DD') : undefined);
                setTo(b ? b.format('YYYY-MM-DD') : undefined);
                setPage(1);
              }} />
          </Col>
          <Col>
            <Input.Search allowClear placeholder="Search name / cost code" style={{ width: 220 }}
              onSearch={v => { setSearch(v || undefined); setPage(1); }} />
          </Col>
          <Col>
            <Button icon={<ReloadOutlined />}
              onClick={() => { setFrom(undefined); setTo(undefined); setSearch(undefined); setPage(1); }}>
              Reset
            </Button>
          </Col>
          <Col flex="auto" />
          <Col>
            <Space>
              {canEdit && (
                <Tooltip title="Set meal unit prices">
                  <Button icon={<SettingOutlined />} onClick={openRates}>Meal Rates</Button>
                </Tooltip>
              )}
              <Button icon={<DownloadOutlined />} loading={exporting} onClick={handleExport}>Export Excel</Button>
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>New Feeding Entry</Button>
            </Space>
          </Col>
        </Row>
      </Card>

      <Tabs
        size="small"
        items={[
          {
            key: 'detail',
            label: 'Feeding Log',
            children: (
              <>
                <Table<FeedingLogEntry>
                  columns={columns} dataSource={data?.items ?? []} rowKey="id" loading={isFetching}
                  size="small" scroll={{ x: 1500 }}
                  pagination={{ current: page, pageSize: 50, total: data?.totalCount ?? 0,
                    onChange: setPage, showSizeChanger: false, showTotal: t => `${t} entries` }}
                />
                {(data?.totalCostNaira ?? 0) > 0 && (
                  <div style={{ textAlign: 'right', padding: '8px 12px' }}>
                    <Text type="secondary">Total for current filter: </Text>
                    <Text strong style={{ color: '#389e0d', fontSize: 15 }}>{naira(data?.totalCostNaira)}</Text>
                  </div>
                )}
              </>
            ),
          },
          {
            key: 'cost',
            label: 'Summary by Cost Centre',
            children: (
              <Table<FeedingSummaryRow>
                columns={summaryColumns('Project No / Cost Code')}
                dataSource={summary?.byCostCentre ?? []} rowKey="key"
                size="small" scroll={{ x: 1200 }} pagination={false}
                summary={() => grand ? (
                  <Table.Summary.Row style={{ background: '#fafafa', fontWeight: 700 }}>
                    <Table.Summary.Cell index={0}><Text strong>Grand Total</Text></Table.Summary.Cell>
                    {ITEMS.map((it, i) => (
                      <Table.Summary.Cell key={it.key as string} index={i + 1} align="center">
                        <Text strong>{(grand[it.key as keyof FeedingSummaryRow] as number) || '—'}</Text>
                      </Table.Summary.Cell>
                    ))}
                    <Table.Summary.Cell index={9} align="right">
                      <Text strong style={{ color: '#389e0d' }}>{naira(grand.totalCostNaira)}</Text>
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                ) : null}
              />
            ),
          },
          {
            key: 'head',
            label: 'Summary by Head Count',
            children: (
              <Table<FeedingSummaryRow>
                columns={summaryColumns('Name')}
                dataSource={summary?.byHeadCount ?? []} rowKey="key"
                size="small" scroll={{ x: 1200 }} pagination={false}
                summary={() => grand ? (
                  <Table.Summary.Row style={{ background: '#fafafa', fontWeight: 700 }}>
                    <Table.Summary.Cell index={0}><Text strong>Grand Total</Text></Table.Summary.Cell>
                    {ITEMS.map((it, i) => (
                      <Table.Summary.Cell key={it.key as string} index={i + 1} align="center">
                        <Text strong>{(grand[it.key as keyof FeedingSummaryRow] as number) || '—'}</Text>
                      </Table.Summary.Cell>
                    ))}
                    <Table.Summary.Cell index={9} align="right">
                      <Text strong style={{ color: '#389e0d' }}>{naira(grand.totalCostNaira)}</Text>
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                ) : null}
              />
            ),
          },
        ]}
      />

      {/* Entry modal */}
      <Modal
        title={editing ? `Edit Feeding Entry — ${editing.staffName}` : 'New Feeding Entry'}
        open={open} onOk={handleSave}
        onCancel={() => { setOpen(false); setEditing(null); form.resetFields(); }}
        confirmLoading={saveMut.isPending}
        okText={editing ? 'Save Changes' : 'Save Entry'} width={640} destroyOnClose>
        <Form form={form} layout="vertical">
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="entryDate" label="Date" rules={[{ required: true, message: 'Date is required' }]}>
                <DatePicker style={{ width: '100%' }} format="D MMM YYYY" />
              </Form.Item>
            </Col>
            <Col span={9}>
              <Form.Item name="staffName" label="Name" rules={[{ required: true, message: 'Name is required' }]}
                tooltip='Staff name, or "Extra" for meals not attributable to one person.'>
                <Input placeholder="e.g. Ogah Moses, or Extra" />
              </Form.Item>
            </Col>
            <Col span={7}>
              <Form.Item name="projectCostCode" label="Project No / Cost Code">
                <Input placeholder="e.g. 1404" />
              </Form.Item>
            </Col>
          </Row>

          <Text type="secondary" style={{ fontSize: 12 }}>Enter the quantity of each item served:</Text>
          <Row gutter={12} style={{ marginTop: 8 }}>
            {ITEMS.map(it => {
              const rate = rates?.find(r => r.itemKey.toLowerCase() === String(it.key).toLowerCase());
              return (
                <Col span={6} key={it.key as string}>
                  <Form.Item name={it.key as string} label={it.label} initialValue={0}
                    tooltip={rate ? `${naira(rate.unitPriceNaira)} each` : undefined}>
                    <InputNumber style={{ width: '100%' }} min={0} max={99} />
                  </Form.Item>
                </Col>
              );
            })}
          </Row>

          <Card size="small" style={{ background: '#f6ffed', borderColor: '#b7eb8f', marginBottom: 12 }}>
            <Text>Entry total: </Text>
            <Text strong style={{ fontSize: 16, color: '#389e0d' }}>{naira(livePreview)}</Text>
          </Card>

          <Form.Item name="notes" label="Notes">
            <TextArea rows={2} maxLength={2000} placeholder="Any remarks…" />
          </Form.Item>
        </Form>
      </Modal>

      {/* Meal rates modal (manager only) */}
      <Modal title="Meal Rates" open={ratesOpen}
        onOk={() => {
          const v = ratesForm.getFieldsValue();
          ratesMut.mutate((rates ?? []).map((r: MealRate) => ({
            itemKey: r.itemKey, unitPriceNaira: Number(v[r.itemKey] ?? r.unitPriceNaira),
          })));
        }}
        onCancel={() => setRatesOpen(false)} confirmLoading={ratesMut.isPending}
        okText="Save Rates" width={460} destroyOnClose>
        <Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 12 }}>
          Unit price per item. Changing a rate affects new and edited entries only —
          previously saved entries keep the value they were recorded at.
        </Text>
        <Form form={ratesForm} layout="vertical">
          <Row gutter={12}>
            {(rates ?? []).map(r => (
              <Col span={12} key={r.itemKey}>
                <Form.Item name={r.itemKey} label={r.label}>
                  <InputNumber style={{ width: '100%' }} min={0}
                    formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
                    parser={(v: string | undefined) => parseFloat(v?.replace(/₦\s?|(,*)/g, '') ?? '0') as 0} />
                </Form.Item>
              </Col>
            ))}
          </Row>
        </Form>
      </Modal>
    </div>
  );
}
