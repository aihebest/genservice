import { useState, useCallback } from 'react';
import {
  Alert, AutoComplete, Badge, Button, Card, Checkbox, Col, DatePicker, Descriptions, Divider, Drawer,
  Dropdown, Form, Input, InputNumber, Modal, Row, Select, Space, Statistic, Table, Tag, Tooltip, Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined, ReloadOutlined, CheckOutlined, CloseOutlined,
  CarOutlined, ToolOutlined, WarningOutlined, SyncOutlined, SettingOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { vehicleMaintenanceApi } from '../../api/vehicleMaintenance.api';
import { integrationApi } from '../../api/integration.api';
import ProgressLogSection from '../../components/shared/ProgressLogSection';
import { VM_STATUS_META, VM_TYPE_META, PRIORITY_META, OFFICE_LOCATIONS, VEHICLE_LIST, VEHICLE_ASSET_NO } from '../../types';
import type { VehicleMaintenance, VehicleMaintenanceStatus } from '../../types';
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

/**
 * Shows whether this request is feeding back to the Logistics platform.
 *
 * Logistics own the vehicles, so an unlinked request means their team is blind
 * to a vehicle that is sitting with us — worth surfacing on the list, not just
 * buried in a detail pane.
 */
function LogisticsSyncTag({ record }: { record: VehicleMaintenance }) {
  const fromLogistics = record.sourceSystem === 'Logistics';

  if (!record.logisticsVehicleId && !record.logisticsRecordId) {
    return (
      <Tooltip title={record.logisticsSyncError
        ?? 'This registration does not match a vehicle in the Logistics fleet register, so their team is not being updated. A manager can link it from the request.'}>
        <Tag color="warning" style={{ fontSize: 11 }}>Not linked</Tag>
      </Tooltip>
    );
  }

  if (record.logisticsSyncStatus === 'Failed') {
    return (
      <Tooltip title={record.logisticsSyncError ?? 'The last update did not reach Logistics.'}>
        <Tag color="error" style={{ fontSize: 11 }}>Sync failed</Tag>
      </Tooltip>
    );
  }

  return (
    <Tooltip title={
      (fromLogistics ? 'Sent in by the Logistics team. ' : '') +
      (record.logisticsSyncedAt
        ? `Logistics last updated ${dayjs(record.logisticsSyncedAt).fromNow()}.`
        : 'Linked to the Logistics fleet register.')
    }>
      <Tag color={fromLogistics ? 'purple' : 'green'} style={{ fontSize: 11 }}>
        {fromLogistics ? 'From Logistics' : 'Synced'}
      </Tag>
    </Tooltip>
  );
}

// ── Table columns ─────────────────────────────────────────────────────────────

/**
 * Every column on the department's MRSF vehicle register, in register order,
 * plus the platform-only columns the team relies on.
 *
 * Two things the coordinators asked for are handled here:
 *  • all 21 register columns are available, not just the dozen that fitted on
 *    screen — the rest are hidden by default and switched on from the Columns
 *    menu, so the default view stays readable;
 *  • headers filter and sort, mirroring the slicers they use in Excel.
 *
 * `defaultHidden` marks the register columns that are off until asked for.
 */
type VmColumn = ColumnsType<VehicleMaintenance>[number] & { key: string };

/** Distinct values in the loaded rows, as antd column filters. */
function valueFilters(rows: VehicleMaintenance[], pick: (r: VehicleMaintenance) => string | undefined) {
  const seen = Array.from(new Set(rows.map(pick).filter((v): v is string => !!v && v.trim() !== '')));
  return seen.sort((a, b) => a.localeCompare(b)).map(v => ({ text: v, value: v }));
}

const txt = (v?: string | null) =>
  v && String(v).trim() !== ''
    ? <Text style={{ fontSize: 12 }}>{v}</Text>
    : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>;

const money = (v?: number | null, strong = false) =>
  v != null
    ? <Text strong={strong} style={{ fontSize: 12, color: strong ? '#389e0d' : undefined }}>
        ₦{Number(v).toLocaleString()}
      </Text>
    : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>;

const num = (v?: number | null) =>
  v != null
    ? <Text style={{ fontSize: 12 }}>{Number(v).toLocaleString()}</Text>
    : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>;

const day = (v?: string | null) =>
  v ? <Text style={{ fontSize: 12 }}>{dayjs(v).format('D MMM YYYY')}</Text>
    : <Text type="secondary" style={{ fontSize: 12 }}>—</Text>;

/** Register columns that stay hidden until switched on. */
const DEFAULT_HIDDEN = new Set([
  'month', 'notificationStatus', 'odometerReading', 'nextServiceMileage',
  'description', 'workDone', 'partsSuppliedBy', 'dateDeliveredToWorkshop',
  'completedAt', 'daysTaken', 'sparesCostNaira', 'justificationEvaluation',
]);

function buildColumns(
  onView: (r: VehicleMaintenance) => void,
  rows: VehicleMaintenance[],
): VmColumn[] {
  return [
    {
      title: 'Ref #', dataIndex: 'requestNumber', key: 'requestNumber', width: 120, fixed: 'left',
      sorter: (a, b) => a.requestNumber.localeCompare(b.requestNumber),
      render: (v: string) => <Text code style={{ fontSize: 12 }}>{v}</Text>,
    },
    {
      title: 'Month', key: 'month', width: 80,
      render: (_: unknown, r) => txt(dayjs(r.dateOfRequest ?? r.createdAt).format('MMM')),
      filters: valueFilters(rows, r => dayjs(r.dateOfRequest ?? r.createdAt).format('MMM')),
      onFilter: (v, r) => dayjs(r.dateOfRequest ?? r.createdAt).format('MMM') === v,
    },
    {
      title: 'Date of Request', dataIndex: 'dateOfRequest', key: 'dateOfRequest', width: 130,
      defaultSortOrder: 'descend',
      sorter: (a, b) =>
        dayjs(a.dateOfRequest ?? a.createdAt).valueOf() - dayjs(b.dateOfRequest ?? b.createdAt).valueOf(),
      render: (_: unknown, r) => day(r.dateOfRequest ?? r.createdAt),
    },
    {
      title: 'Raised By', dataIndex: 'requestedByName', key: 'requestedByName', width: 140, ellipsis: true,
      filters: valueFilters(rows, r => r.requestedByName),
      onFilter: (v, r) => r.requestedByName === v,
      sorter: (a, b) => (a.requestedByName ?? '').localeCompare(b.requestedByName ?? ''),
      render: (v: string) => txt(v),
    },
    {
      title: 'Notification', dataIndex: 'notificationStatus', key: 'notificationStatus', width: 110,
      filters: valueFilters(rows, r => r.notificationStatus),
      onFilter: (v, r) => r.notificationStatus === v,
      render: (v?: string) =>
        v ? <Tag color={v === 'Closed' ? 'default' : 'gold'}>{v}</Tag> : txt(null),
    },
    {
      title: 'Location', dataIndex: 'currentLocation', key: 'currentLocation', width: 150, ellipsis: true,
      filters: valueFilters(rows, r => r.currentLocation),
      onFilter: (v, r) => r.currentLocation === v,
      sorter: (a, b) => (a.currentLocation ?? '').localeCompare(b.currentLocation ?? ''),
      render: (v?: string) => txt(v),
    },
    {
      title: 'Asset No.', dataIndex: 'assetNo', key: 'assetNo', width: 110, ellipsis: true,
      sorter: (a, b) => (a.assetNo ?? '').localeCompare(b.assetNo ?? ''),
      render: (v?: string) => txt(v),
    },
    {
      title: 'Vehicle Reg', dataIndex: 'vehicleRegNo', key: 'vehicleRegNo', width: 130,
      filters: valueFilters(rows, r => r.vehicleRegNo),
      onFilter: (v, r) => r.vehicleRegNo === v,
      filterSearch: true,
      sorter: (a, b) => a.vehicleRegNo.localeCompare(b.vehicleRegNo),
      render: (v: string) => <Tag icon={<CarOutlined />} color="blue">{v}</Tag>,
    },
    {
      title: 'Vehicle Type', dataIndex: 'vehicleType', key: 'vehicleType', width: 160, ellipsis: true,
      filters: valueFilters(rows, r => r.vehicleType),
      onFilter: (v, r) => r.vehicleType === v,
      filterSearch: true,
      render: (v?: string) => txt(v),
    },
    {
      title: 'Odometer', dataIndex: 'odometerReading', key: 'odometerReading', width: 110,
      render: (v?: string) => txt(v),
    },
    {
      title: 'Next Service Mileage', dataIndex: 'nextServiceMileage', key: 'nextServiceMileage', width: 150,
      sorter: (a, b) => (a.nextServiceMileage ?? 0) - (b.nextServiceMileage ?? 0),
      render: (v?: number) => num(v),
    },
    {
      // Widened and ellipsised: at 130px the type tag spilled over the status
      // column, which is what the coordinators saw in their screenshot.
      title: 'Maintenance', dataIndex: 'maintenanceType', key: 'maintenanceType', width: 190, ellipsis: true,
      filters: valueFilters(rows, r => r.maintenanceType),
      onFilter: (v, r) => r.maintenanceType === v,
      render: (v: string) => {
        const m = VM_TYPE_META[v as keyof typeof VM_TYPE_META];
        return <Tag color={m?.color} style={{ whiteSpace: 'normal' }}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Description of Request', dataIndex: 'description', key: 'description', width: 220, ellipsis: true,
      render: (v?: string) => <Tooltip title={v}>{txt(v)}</Tooltip>,
    },
    {
      title: 'Work Done', dataIndex: 'workDone', key: 'workDone', width: 220, ellipsis: true,
      render: (v?: string) => <Tooltip title={v}>{txt(v)}</Tooltip>,
    },
    {
      title: 'Parts Supplied By', dataIndex: 'partsSuppliedBy', key: 'partsSuppliedBy', width: 150, ellipsis: true,
      filters: valueFilters(rows, r => r.partsSuppliedBy),
      onFilter: (v, r) => r.partsSuppliedBy === v,
      render: (v?: string) => txt(v),
    },
    {
      title: 'Work Done At', dataIndex: 'workshopName', key: 'workshopName', width: 150, ellipsis: true,
      filters: valueFilters(rows, r => r.workshopName),
      onFilter: (v, r) => r.workshopName === v,
      render: (v?: string) => v ? txt(v) : <Text type="secondary" style={{ fontSize: 12 }}>Not dispatched</Text>,
    },
    {
      title: 'Date to Workshop', dataIndex: 'dateDeliveredToWorkshop', key: 'dateDeliveredToWorkshop', width: 145,
      sorter: (a, b) =>
        dayjs(a.dateDeliveredToWorkshop ?? 0).valueOf() - dayjs(b.dateDeliveredToWorkshop ?? 0).valueOf(),
      render: (v?: string) => day(v),
    },
    {
      title: 'Status', dataIndex: 'status', key: 'status', width: 140,
      filters: valueFilters(rows, r => r.status),
      onFilter: (v, r) => r.status === v,
      render: (v: VehicleMaintenanceStatus) => {
        const m = VM_STATUS_META[v];
        return <Badge status={m?.badge as any} text={m?.label ?? v} />;
      },
    },
    {
      title: 'Date Completed', dataIndex: 'completedAt', key: 'completedAt', width: 145,
      sorter: (a, b) => dayjs(a.completedAt ?? 0).valueOf() - dayjs(b.completedAt ?? 0).valueOf(),
      render: (v?: string) => day(v),
    },
    {
      title: 'Days Taken', key: 'daysTaken', width: 105,
      sorter: (a, b) => (a.daysInWorkshop ?? 0) - (b.daysInWorkshop ?? 0),
      render: (_: unknown, r) => num(r.daysInWorkshop),
    },
    {
      title: 'Spares Cost (₦)', dataIndex: 'sparesCostNaira', key: 'sparesCostNaira', width: 140,
      sorter: (a, b) => (a.sparesCostNaira ?? 0) - (b.sparesCostNaira ?? 0),
      render: (v?: number) => money(v),
    },
    {
      title: 'Justification / Evaluation', dataIndex: 'justificationEvaluation', key: 'justificationEvaluation',
      width: 220, ellipsis: true,
      render: (v?: string) => <Tooltip title={v}>{txt(v)}</Tooltip>,
    },
    {
      title: 'Priority', dataIndex: 'priority', key: 'priority', width: 100,
      filters: valueFilters(rows, r => r.priority),
      onFilter: (v, r) => r.priority === v,
      render: (v: string) => {
        const m = PRIORITY_META[v as keyof typeof PRIORITY_META];
        return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
      },
    },
    {
      title: 'Amount (₦)', dataIndex: 'amountNaira', key: 'amountNaira', width: 120,
      sorter: (a, b) => (a.amountNaira ?? 0) - (b.amountNaira ?? 0),
      render: (v?: number) => money(v),
    },
    {
      title: 'Final Amount (₦)', dataIndex: 'finalAmountNaira', key: 'finalAmountNaira', width: 140,
      sorter: (a, b) => (a.finalAmountNaira ?? 0) - (b.finalAmountNaira ?? 0),
      render: (v?: number) => money(v, true),
    },
    {
      title: 'Days Open', dataIndex: 'daysOpen', key: 'daysOpen', width: 100,
      sorter: (a, b) => (a.daysOpen ?? 0) - (b.daysOpen ?? 0),
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
      title: 'Logistics', key: 'logistics', width: 130,
      filters: [
        { text: 'From Logistics', value: 'from' },
        { text: 'Synced',         value: 'synced' },
        { text: 'Not linked',     value: 'unlinked' },
        { text: 'Sync failed',    value: 'failed' },
      ],
      onFilter: (v, r) => {
        if (v === 'from')     return r.sourceSystem === 'Logistics';
        if (v === 'failed')   return r.logisticsSyncStatus === 'Failed';
        if (v === 'unlinked') return !r.logisticsVehicleId && !r.logisticsRecordId;
        return !!(r.logisticsVehicleId || r.logisticsRecordId) && r.logisticsSyncStatus !== 'Failed';
      },
      render: (_: unknown, r: VehicleMaintenance) => <LogisticsSyncTag record={r} />,
    },
    {
      title: '', key: 'action', width: 70, fixed: 'right',
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
  const [completeFinal, setCompleteFinal] = useState<number | null>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [actionError,   setActionError]   = useState<string | null>(null);

  const [createForm]    = Form.useForm();
  const [createLoading, setCreateLoading] = useState(false);
  const [createError,   setCreateError]   = useState<string | null>(null);
  const [locationSel,   setLocationSel]   = useState<string | null>(null);
  // Set when the user picks a vehicle out of the live Logistics fleet, so the
  // request links to their record without depending on the plate matching.
  const [logisticsVehicleId, setLogisticsVehicleId] = useState<string | null>(null);

  const isApprover = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';

  // One-shot back-population of everything raised before the Logistics link existed.
  const [resyncing, setResyncing] = useState(false);

  const handleResyncAll = () => {
    Modal.confirm({
      title: 'Send all requests to Logistics?',
      content:
        'Every request that matches a vehicle in the Logistics fleet register will be pushed to their maintenance register. '
        + 'Existing records there are updated, not duplicated, so this is safe to run more than once.',
      okText: 'Send',
      onOk: async () => {
        setResyncing(true);
        try {
          const res = await integrationApi.resyncAll();
          refresh();
          Modal.info({ title: 'Sync complete', content: res.message });
        } catch (e: unknown) {
          Modal.error({
            title: 'Sync failed',
            content: (e as { response?: { data?: { message?: string } } })?.response?.data?.message
              ?? 'Could not reach the Logistics platform.',
          });
        } finally { setResyncing(false); }
      },
    });
  };

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

  // Live fleet from the Logistics platform. The API returns [] rather than an
  // error when Logistics is unreachable, so the form degrades to the built-in
  // list and free text instead of blocking the user.
  const { data: logisticsFleet } = useQuery({
    queryKey: ['vm', 'logistics-fleet'],
    queryFn:  integrationApi.fleet,
    staleTime: 10 * 60_000,
    retry: false,
  });

  // Live Logistics vehicles first, then any from the built-in list they don't
  // already cover — matching on the plate with spacing and case ignored.
  const vehicleOptions = (() => {
    const norm = (s: string) => s.replace(/[^a-z0-9]/gi, '').toUpperCase();
    const live = (logisticsFleet ?? []).map(v => ({
      value: v.registrationNo,
      label: `${v.registrationNo} — ${[v.make, v.model].filter(Boolean).join(' ') || 'Vehicle'}`,
    }));
    const seen = new Set(live.map(o => norm(o.value)));
    const fallback = VEHICLE_LIST
      .filter(v => !seen.has(norm(v.regNo)))
      .map(v => ({ value: v.regNo, label: `${v.regNo} - ${v.description}` }));
    return [...live, ...fallback];
  })();

  const openDetail = (r: VehicleMaintenance) => { setSelected(r); setDrawerOpen(true); setActionError(null); };

  // All 21 register columns are available; the detail-heavy ones start hidden so
  // the default view stays readable, and the Columns menu switches them on.
  const rows = data?.items ?? [];
  const allColumns = buildColumns(openDetail, rows);
  const [hiddenCols, setHiddenCols] = useState<Set<string>>(new Set(DEFAULT_HIDDEN));
  const visibleColumns = allColumns.filter(c => !hiddenCols.has(c.key));

  const toggleCol = (key: string) => setHiddenCols(prev => {
    const next = new Set(prev);
    if (next.has(key)) next.delete(key); else next.add(key);
    return next;
  });


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

      // Only send the Logistics id if the registration still matches what was
      // picked. If the user typed over it afterwards, let the server re-match by
      // plate rather than linking to the wrong vehicle.
      const [pickedId, pickedReg] = (logisticsVehicleId ?? '').split('|');
      const stillMatches = pickedReg &&
        pickedReg.replace(/[^a-z0-9]/gi, '').toUpperCase() ===
        values.vehicleRegNo.replace(/[^a-z0-9]/gi, '').toUpperCase();

      await vehicleMaintenanceApi.create({
        logisticsVehicleId: stillMatches ? pickedId : undefined,
        vehicleRegNo: values.vehicleRegNo.toUpperCase(), vehicleType: values.vehicleType,
        maintenanceType: values.maintenanceType, description: values.description,
        priority: values.priority, currentLocation: location,
        assetNo: values.assetNo?.trim() || undefined,
        amountNaira: values.amountNaira ? Number(values.amountNaira) : undefined,
        odometerReading: values.odometerReading?.trim() || undefined,
        runningHours: values.runningHours ? Number(values.runningHours) : undefined,
        nextServiceHour: values.nextServiceHour ? Number(values.nextServiceHour) : undefined,
        nextServiceMileage: values.nextServiceMileage ? Number(values.nextServiceMileage) : undefined,
        justificationEvaluation: values.justificationEvaluation?.trim() || undefined,
        notificationStatus: values.notificationStatus || undefined,
        dateOfRequest: values.dateOfRequest ? (values.dateOfRequest as unknown as dayjs.Dayjs).format('YYYY-MM-DD') : undefined,
      });
      createForm.resetFields(); setLocationSel(null); setLogisticsVehicleId(null);
      setCreateOpen(false); refresh();
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
            {isApprover && (
              <Tooltip title="Send every request to the Logistics platform. Use once after go-live — from then on each status change syncs by itself.">
                <Button icon={<SyncOutlined />} loading={resyncing} onClick={handleResyncAll}>
                  Sync to Logistics
                </Button>
              </Tooltip>
            )}
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
        <div style={{ padding: '12px 24px', borderBottom: '1px solid #f0f0f0',
                      display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <Input.Search placeholder="Search by reg number, vehicle type, description…" style={{ width: 340 }}
            allowClear onSearch={v => { setSearch(v); setPage(1); }} onChange={e => !e.target.value && setSearch('')} />

          {/* All 21 register columns, switchable — the register is wider than any
              screen, so the alternative to this is horizontal scrolling forever. */}
          <Dropdown
            trigger={['click']}
            menu={{
              items: allColumns
                .filter(c => c.key !== 'action')
                .map(c => ({
                  key: c.key,
                  label: (
                    <Checkbox checked={!hiddenCols.has(c.key)} onClick={e => e.preventDefault()}>
                      {typeof c.title === 'string' && c.title ? c.title : c.key}
                    </Checkbox>
                  ),
                })),
              onClick: ({ key }) => toggleCol(key),
            }}
          >
            <Button icon={<SettingOutlined />}>
              Columns ({visibleColumns.length}/{allColumns.length})
            </Button>
          </Dropdown>

          <Button size="small" type="link" onClick={() => setHiddenCols(new Set())}>
            Show all register columns
          </Button>
          <Button size="small" type="link" onClick={() => setHiddenCols(new Set(DEFAULT_HIDDEN))}>
            Reset
          </Button>
        </div>
        <Table<VehicleMaintenance>
          columns={visibleColumns} dataSource={rows} rowKey="id" loading={isFetching}
          pagination={{ current: page, pageSize: 15, total: data?.totalCount ?? 0, onChange: p => setPage(p),
            showTotal: (t, [f, to]) => `${f}–${to} of ${t} requests`, showSizeChanger: false }}
          onRow={r => ({ onClick: () => openDetail(r), style: { cursor: 'pointer' } })}
          size="middle" style={{ padding: '0 8px' }}
          // Sum of the visible widths, so the fixed Ref # and View columns pin
          // correctly however many register columns are switched on.
          scroll={{ x: visibleColumns.reduce((w, c) => w + (Number(c.width) || 120), 0) }}
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
                rules={[{ required: true, message: 'Enter registration number' }]}
                tooltip="Pick a vehicle to auto-fill the type, or type the registration manually.">
                <AutoComplete
                  placeholder="Select or type e.g. PHC 185 AM"
                  onSelect={(val: string) => {
                    const fleetHit = (logisticsFleet ?? []).find(x => x.registrationNo === val);
                    const v = VEHICLE_LIST.find(x => x.regNo === val);
                    // Remember the Logistics id so the request links to their
                    // record even if the plate is typed differently later.
                    setLogisticsVehicleId(fleetHit ? `${fleetHit.id}|${fleetHit.registrationNo}` : null);
                    const typeFromFleet = [fleetHit?.make, fleetHit?.model].filter(Boolean).join(' ');
                    createForm.setFieldsValue({
                      ...(typeFromFleet ? { vehicleType: typeFromFleet }
                                        : v ? { vehicleType: v.description } : {}),
                      ...(fleetHit?.assetTagNo ? { assetNo: fleetHit.assetTagNo }
                          : VEHICLE_ASSET_NO[val] ? { assetNo: VEHICLE_ASSET_NO[val] } : {}),
                      ...(fleetHit?.odometerKm ? { odometerReading: String(fleetHit.odometerKm) } : {}),
                    });
                  }}
                  filterOption={(input, option) =>
                    String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())}
                  options={vehicleOptions} />
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
              <Form.Item name="dateOfRequest" label="Date of Request" initialValue={dayjs()}>
                <DatePicker style={{ width: '100%' }} format="D MMM YYYY" />
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
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="assetNo" label="Asset No."
                tooltip="Auto-fills from the vehicle registry when you pick a vehicle. You can still edit it.">
                <Input placeholder="Auto-fills from vehicle" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="amountNaira" label="Amount (₦)">
                <InputNumber style={{ width: '100%' }} min={0} placeholder="e.g. 50000"
                  formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
                  parser={(v: string | undefined) => parseFloat(v?.replace(/₦\s?|(,*)/g, '') ?? '0') as 0} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="odometerReading" label="Odometer / Hour Reading">
                <Input placeholder="Reading at time of request (optional)" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="notificationStatus" label="Notification Status" initialValue="Open">
                <Select options={['Open','Notified','Closed'].map(s => ({ value: s, label: s }))} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              <Form.Item name="runningHours" label="Current Running Hours">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 13602" min={0} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="nextServiceHour" label="Next Service Due At">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 13852" min={0} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={12}>
              {/* NEXT SERVICE MILEAGE on the register — the odometer the next
                  service falls due at, as opposed to the hour reading above. */}
              <Form.Item name="nextServiceMileage" label="Next Service Mileage"
                tooltip="Odometer reading at which the next service becomes due.">
                <InputNumber style={{ width: '100%' }} placeholder="e.g. 170000" min={0} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="justificationEvaluation" label="Justification / Evaluation"
                tooltip="Why the work is needed — routine servicing, reported fault, breakdown.">
                <Input placeholder="e.g. Routine servicing required" />
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
            <Descriptions.Item label="Logistics">
              <Space size={6}>
                <LogisticsSyncTag record={selected} />
                {isApprover && (selected.logisticsSyncStatus === 'Failed' || !selected.logisticsVehicleId) && (
                  <Button size="small" loading={actionLoading}
                    onClick={() => act(() => integrationApi.resync(selected.id))}>
                    Retry sync
                  </Button>
                )}
              </Space>
            </Descriptions.Item>
            {selected.assetNo && <Descriptions.Item label="Asset No.">{selected.assetNo}</Descriptions.Item>}
            {selected.amountNaira != null && (
              <Descriptions.Item label="Amount (estimated)">₦{Number(selected.amountNaira).toLocaleString()}</Descriptions.Item>
            )}
            {selected.finalAmountNaira != null && (
              <Descriptions.Item label="Final Amount">₦{Number(selected.finalAmountNaira).toLocaleString()}</Descriptions.Item>
            )}
            {selected.odometerReading && (
              <Descriptions.Item label="Odometer / Hour Reading">{selected.odometerReading}</Descriptions.Item>
            )}
            {selected.runningHours != null && (
              <Descriptions.Item label="Current Running Hours">{selected.runningHours.toLocaleString()} h</Descriptions.Item>
            )}
            {selected.nextServiceHour != null && (
              <Descriptions.Item label="Next Service Due At">{selected.nextServiceHour.toLocaleString()} h</Descriptions.Item>
            )}
            {selected.notificationStatus && (
              <Descriptions.Item label="Notification Status">{selected.notificationStatus}</Descriptions.Item>
            )}
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
              <Divider titlePlacement="left" orientationMargin={0} style={{ fontSize: 12 }}>Workshop</Divider>
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
              <Divider titlePlacement="left" orientationMargin={0} style={{ fontSize: 12 }}>
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
              <Divider titlePlacement="left" orientationMargin={0} style={{ fontSize: 12 }}>Completion</Divider>
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
        onOk={async () => { if (!workshopName.trim() || !selected) return; await act(() => vehicleMaintenanceApi.dispatch(selected.id, { workshopName, workshopLocation: workshopLoc || undefined })); setDispatchOpen(false); setWorkshopName(''); setWorkshopLoc(''); }}
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
        onOk={async () => { if (!selected) return; await act(() => vehicleMaintenanceApi.complete(selected.id, { notes: completeNotes || undefined, finalAmountNaira: completeFinal ?? undefined })); setCompleteOpen(false); setCompleteNotes(''); setCompleteFinal(null); }}
        onCancel={() => { setCompleteOpen(false); setCompleteNotes(''); setCompleteFinal(null); }}
        okText="Confirm Completion" confirmLoading={actionLoading}>
        <p>Mark <strong>{selected?.requestNumber}</strong> ({selected?.vehicleRegNo}) as completed:</p>
        <div style={{ marginBottom: 12 }}>
          <Text strong style={{ display: 'block', marginBottom: 6 }}>Final Amount (₦)</Text>
          <InputNumber style={{ width: '100%' }} min={0} value={completeFinal ?? undefined}
            onChange={v => setCompleteFinal(v as number | null)} placeholder="Actual total cost of this repair"
            formatter={v => `₦ ${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
            parser={(v: string | undefined) => parseFloat(v?.replace(/₦\s?|(,*)/g, '') ?? '0') as 0} />
        </div>
        <TextArea rows={3} value={completeNotes} onChange={e => setCompleteNotes(e.target.value)}
          placeholder="Completion notes — work done, parts replaced, etc. (optional)" maxLength={2000} />
      </Modal>
    </div>
  );
}
