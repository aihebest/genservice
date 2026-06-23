import { useNavigate } from 'react-router-dom';
import {
  Alert, Avatar, Badge, Button, Card, Col, List, Progress,
  Row, Skeleton, Space, Statistic, Tag, Tooltip, Typography,
} from 'antd';
import {
  FormOutlined, TeamOutlined, ToolOutlined,
  WarningOutlined, ClockCircleOutlined, CheckCircleOutlined,
  ReloadOutlined, ArrowRightOutlined, ThunderboltOutlined,
  EnvironmentOutlined, UserOutlined, ShopOutlined,
  FireOutlined, FileProtectOutlined, CalendarOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { dashboardApi } from '../../api/dashboard.api';
import type {
  DashboardRequestItem, DashboardActivityItem,
  DashboardMaintenanceItem, DashboardStoreItem,
  DashboardDieselReqItem,
} from '../../api/dashboard.api';
import { useAuthStore } from '../../store/authStore';
import {
  CATEGORY_META, STATUS_META, PRIORITY_META,
  ACTIVITY_CATEGORY_META, MAINTENANCE_CATEGORY_META,
} from '../../types';
import type {
  RequestCategory, RequestStatus, RequestPriority,
  ActivityCategory, MaintenanceCategory,
} from '../../types';

dayjs.extend(relativeTime);

const { Title, Text } = Typography;

const DIESEL_TANK_CAPACITY = 5000; // litres — adjust to site's actual capacity

// ── Helpers ────────────────────────────────────────────────────────────────────
function getInitials(name: string) {
  return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
}
function getAvatarColor(name: string) {
  const colors = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#eb2f96', '#13c2c2'];
  let hash = 0;
  for (const ch of name) hash = ch.charCodeAt(0) + ((hash << 5) - hash);
  return colors[Math.abs(hash) % colors.length];
}

// ── KPI Card ───────────────────────────────────────────────────────────────────
interface KpiProps {
  title:      string;
  value:      number | string | undefined;
  suffix?:    string;
  icon:       React.ReactNode;
  color:      string;
  loading:    boolean;
  onClick?:   () => void;
  alert?:     boolean;
  precision?: number;
}
function KpiCard({ title, value, suffix, icon, color, loading, onClick, alert, precision }: KpiProps) {
  const numVal = typeof value === 'number' ? value : Number(value ?? 0);
  return (
    <Card
      styles={{ body: { padding: '14px 16px' } }}
      style={{
        cursor: onClick ? 'pointer' : 'default',
        borderColor: alert && numVal > 0 ? '#ff4d4f' : undefined,
        borderLeftWidth: alert && numVal > 0 ? 3 : undefined,
      }}
      onClick={onClick}
      hoverable={!!onClick}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          width: 40, height: 40, borderRadius: 10,
          background: color + '18',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 18, color, flexShrink: 0,
        }}>
          {icon}
        </div>
        {loading
          ? <Skeleton.Button active style={{ width: 80, height: 40 }} />
          : <Statistic
              title={<span style={{ fontSize: 11, color: '#8c8c8c' }}>{title}</span>}
              value={precision !== undefined ? numVal.toFixed(precision) : numVal}
              suffix={suffix && <span style={{ fontSize: 14 }}>{suffix}</span>}
              valueStyle={{
                fontSize: 22, fontWeight: 700,
                color: alert && numVal > 0 ? '#ff4d4f' : color,
              }}
            />
        }
      </div>
    </Card>
  );
}

// ── Diesel tank gauge ─────────────────────────────────────────────────────────
function FuelGauge({ litres, loading }: { litres: number | null; loading: boolean }) {
  if (loading) return <Skeleton.Button active style={{ width: '100%', height: 48 }} />;
  if (litres === null) return (
    <div style={{ textAlign: 'center', padding: '12px 0' }}>
      <Text type="secondary" style={{ fontSize: 12 }}>No tank reading logged yet</Text>
    </div>
  );

  const pct   = Math.min(100, Math.round((litres / DIESEL_TANK_CAPACITY) * 100));
  const color = pct <= 15 ? '#ff4d4f' : pct <= 30 ? '#fa8c16' : '#52c41a';

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
        <Text strong style={{ fontSize: 13 }}>Diesel Tank Level</Text>
        <Text strong style={{ fontSize: 13, color }}>
          {litres.toLocaleString()} L ({pct}%)
        </Text>
      </div>
      <Progress
        percent={pct}
        strokeColor={color}
        trailColor="#f5f5f5"
        showInfo={false}
        strokeWidth={14}
        style={{ marginBottom: 4 }}
      />
      <Text type="secondary" style={{ fontSize: 11 }}>
        Capacity: {DIESEL_TANK_CAPACITY.toLocaleString()} L
        {pct <= 15 && ' · ⚠️ Critical — schedule refill'}
        {pct > 15 && pct <= 30 && ' · Low — consider refilling soon'}
      </Text>
    </div>
  );
}

// ── Escalation badge ──────────────────────────────────────────────────────────
function EscBadge({ level }: { level: number }) {
  if (level === 0) return null;
  if (level >= 2)  return <Tag color="red"   style={{ fontSize: 10 }}>🆘 Manager</Tag>;
  return               <Tag color="orange" style={{ fontSize: 10 }}>⚠️ Escalated</Tag>;
}

// ── Diesel req status tag ─────────────────────────────────────────────────────
function DieselStatusTag({ status }: { status: string }) {
  const map: Record<string, { color: string; label: string }> = {
    Pending:   { color: 'orange', label: 'Pending'   },
    Approved:  { color: 'blue',   label: 'Approved'  },
    Dispensed: { color: 'green',  label: 'Dispensed' },
    Rejected:  { color: 'red',    label: 'Rejected'  },
  };
  const m = map[status] ?? { color: 'default', label: status };
  return <Tag color={m.color} style={{ fontSize: 11 }}>{m.label}</Tag>;
}

// ── Main page ──────────────────────────────────────────────────────────────────
export default function DashboardPage() {
  const user     = useAuthStore(s => s.user);
  const navigate = useNavigate();
  const qc       = useQueryClient();

  const isApprover = user?.role === 'DepartmentManager'
    || user?.role === 'Supervisor'
    || user?.role === 'SystemAdmin';

  const { data, isFetching, isLoading } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn:  dashboardApi.summary,
    refetchInterval: 30_000,
  });

  const refresh = () => qc.invalidateQueries({ queryKey: ['dashboard-summary'] });

  const greeting = (() => {
    const h = new Date().getHours();
    if (h < 12) return 'Good morning';
    if (h < 17) return 'Good afternoon';
    return 'Good evening';
  })();

  return (
    <div>
      {/* ── Header ──────────────────────────────────────────────────── */}
      <Row justify="space-between" align="middle" style={{ marginBottom: 20 }}>
        <Col>
          <Title level={4} style={{ margin: 0 }}>
            {greeting}, {user?.fullName?.split(' ')[0] ?? 'there'} 👋
          </Title>
          <Text type="secondary" style={{ fontSize: 13 }}>
            {dayjs().format('dddd, D MMMM YYYY')} · General Service Operations Overview
          </Text>
        </Col>
        <Col>
          <Tooltip title="Refresh dashboard">
            <Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} />
          </Tooltip>
        </Col>
      </Row>

      {/* ── Alert banners (ordered by urgency) ──────────────────────── */}
      {isApprover && (data?.maintenanceEscalations ?? 0) > 0 && (
        <Alert
          type="error" showIcon icon={<WarningOutlined />}
          message={
            <span>
              <strong>{data!.maintenanceEscalations} maintenance task{data!.maintenanceEscalations > 1 ? 's have' : ' has'}</strong>
              {' '}been escalated and require urgent management attention.
            </span>
          }
          action={
            <Button size="small" danger onClick={() => navigate('/maintenance')} icon={<ArrowRightOutlined />}>
              View escalations
            </Button>
          }
          style={{ marginBottom: 12 }}
        />
      )}

      {isApprover && (data?.requestsPendingApproval ?? 0) > 0 && (
        <Alert
          type="warning" showIcon icon={<WarningOutlined />}
          message={
            <span>
              <strong>{data!.requestsPendingApproval} service request{data!.requestsPendingApproval > 1 ? 's are' : ' is'}</strong>
              {' '}waiting for your approval.
            </span>
          }
          action={
            <Button size="small" onClick={() => navigate('/requests')} icon={<ArrowRightOutlined />}>
              Review
            </Button>
          }
          style={{ marginBottom: 12 }}
        />
      )}

      {(data?.maintenanceOverdue ?? 0) > 0 && (
        <Alert
          type="error" showIcon icon={<WarningOutlined />}
          message={
            <span>
              <strong>{data!.maintenanceOverdue} maintenance task{data!.maintenanceOverdue > 1 ? 's are' : ' is'}</strong>
              {' '}overdue and require immediate attention.
            </span>
          }
          action={
            <Button size="small" danger onClick={() => navigate('/maintenance')} icon={<ArrowRightOutlined />}>
              View
            </Button>
          }
          style={{ marginBottom: 12 }}
        />
      )}

      {isApprover && (data?.storeLowStockCount ?? 0) > 0 && (
        <Alert
          type="warning" showIcon icon={<ShopOutlined />}
          message={
            <span>
              <strong>{data!.storeLowStockCount} store item{data!.storeLowStockCount > 1 ? 's are' : ' is'}</strong>
              {' '}at or below reorder level.
            </span>
          }
          action={
            <Button size="small" onClick={() => navigate('/store')} icon={<ArrowRightOutlined />}>
              View stock
            </Button>
          }
          style={{ marginBottom: 12 }}
        />
      )}

      {isApprover && (data?.dieselReqsPending ?? 0) > 0 && (
        <Alert
          type="info" showIcon icon={<FileProtectOutlined />}
          message={
            <span>
              <strong>{data!.dieselReqsPending} diesel requisition{data!.dieselReqsPending > 1 ? 's are' : ' is'}</strong>
              {' '}awaiting approval or dispensing.
            </span>
          }
          action={
            <Button size="small" onClick={() => navigate('/fuel')} icon={<ArrowRightOutlined />}>
              View
            </Button>
          }
          style={{ marginBottom: 12 }}
        />
      )}

      {/* ── KPI Row 1 — Requests & Operations ───────────────────────── */}
      <div style={{ marginBottom: 8 }}>
        <Text type="secondary" style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.5 }}>
          Requests &amp; Operations
        </Text>
      </div>
      <Row gutter={[12, 12]} style={{ marginBottom: 20 }}>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Open Requests" value={data?.requestsOpen} loading={isLoading}
            icon={<FormOutlined />} color="#1677ff"
            onClick={() => navigate('/requests')}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Pending Approval" value={data?.requestsPendingApproval} loading={isLoading}
            icon={<ClockCircleOutlined />} color="#fa8c16"
            onClick={() => navigate('/requests')}
            alert={isApprover && (data?.requestsPendingApproval ?? 0) > 0}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="In Progress" value={data?.requestsInProgress} loading={isLoading}
            icon={<ThunderboltOutlined />} color="#722ed1"
            onClick={() => navigate('/requests')}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Staff Active Now" value={data?.staffActiveNow} loading={isLoading}
            icon={<TeamOutlined />} color="#52c41a"
            onClick={() => navigate('/activities')}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Overdue Maint." value={data?.maintenanceOverdue} loading={isLoading}
            icon={<ToolOutlined />} color="#ff4d4f"
            onClick={() => navigate('/maintenance')}
            alert={(data?.maintenanceOverdue ?? 0) > 0}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Done Today" value={data?.requestsCompletedToday} loading={isLoading}
            icon={<CheckCircleOutlined />} color="#13c2c2"
          />
        </Col>
      </Row>

      {/* ── KPI Row 2 — Modules ─────────────────────────────────────── */}
      <div style={{ marginBottom: 8 }}>
        <Text type="secondary" style={{ fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.5 }}>
          Module Overview
        </Text>
      </div>
      <Row gutter={[12, 12]} style={{ marginBottom: 24 }}>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Store Reqs Pending" value={data?.storeReqsPending} loading={isLoading}
            icon={<ShopOutlined />} color="#eb2f96"
            onClick={() => navigate('/store')}
            alert={(data?.storeReqsPending ?? 0) > 0}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Low Stock Items" value={data?.storeLowStockCount} loading={isLoading}
            icon={<WarningOutlined />} color="#fa541c"
            onClick={() => navigate('/store')}
            alert={(data?.storeLowStockCount ?? 0) > 0}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Diesel Reqs Pending" value={data?.dieselReqsPending} loading={isLoading}
            icon={<FileProtectOutlined />} color="#d4b106"
            onClick={() => navigate('/fuel')}
            alert={(data?.dieselReqsPending ?? 0) > 0}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Diesel MTD" value={data?.dieselDispensedThisMonthLitres} loading={isLoading}
            icon={<FireOutlined />} color="#ff7a45"
            suffix="L"
            precision={0}
            onClick={() => navigate('/fuel')}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Maint. Escalations" value={data?.maintenanceEscalations} loading={isLoading}
            icon={<CalendarOutlined />} color="#ff4d4f"
            onClick={() => navigate('/maintenance')}
            alert={(data?.maintenanceEscalations ?? 0) > 0}
          />
        </Col>
        <Col xs={12} sm={8} md={8} lg={4}>
          <KpiCard
            title="Due in 7 Days" value={data?.maintenanceDueSoon} loading={isLoading}
            icon={<ClockCircleOutlined />} color="#1677ff"
            onClick={() => navigate('/maintenance')}
          />
        </Col>
      </Row>

      {/* ── Main panels ─────────────────────────────────────────────── */}
      <Row gutter={[16, 16]}>

        {/* ── Left column ──────────────────────────────────────────── */}
        <Col xs={24} lg={14}>
          <Space direction="vertical" style={{ width: '100%' }} size={16}>

            {/* Recent requests */}
            <Card
              title={<Space><FormOutlined /><span>Recent Service Requests</span></Space>}
              extra={
                <Button type="link" size="small" onClick={() => navigate('/requests')}
                  icon={<ArrowRightOutlined />}>
                  View all
                </Button>
              }
              styles={{ body: { padding: 0 } }}
            >
              {isLoading
                ? <div style={{ padding: 20 }}><Skeleton active paragraph={{ rows: 4 }} /></div>
                : (
                  <List<DashboardRequestItem>
                    dataSource={data?.recentRequests ?? []}
                    locale={{ emptyText: 'No requests yet' }}
                    renderItem={item => {
                      const catMeta  = CATEGORY_META[item.category as RequestCategory];
                      const statMeta = STATUS_META[item.status as RequestStatus];
                      const priMeta  = PRIORITY_META[item.priority as RequestPriority];
                      return (
                        <List.Item
                          style={{ padding: '10px 20px', cursor: 'pointer' }}
                          onClick={() => navigate('/requests')}
                        >
                          <List.Item.Meta
                            title={
                              <Space size={6}>
                                <Text style={{ fontSize: 13 }}>{item.title}</Text>
                                {item.priority === 'Urgent' && (
                                  <Tag color={priMeta?.color} style={{ fontSize: 11 }}>Urgent</Tag>
                                )}
                              </Space>
                            }
                            description={
                              <Space size={6} wrap>
                                <Text code style={{ fontSize: 11 }}>{item.ticketNumber}</Text>
                                <Tag color={catMeta?.color} style={{ fontSize: 11 }}>{catMeta?.label ?? item.category}</Tag>
                                <Text type="secondary" style={{ fontSize: 11 }}>{dayjs(item.createdAt).fromNow()}</Text>
                              </Space>
                            }
                          />
                          <Badge status={statMeta?.color as any} text={
                            <Text style={{ fontSize: 12 }}>{statMeta?.label ?? item.status}</Text>
                          } />
                        </List.Item>
                      );
                    }}
                  />
                )
              }
            </Card>

            {/* Pending approvals — approvers only */}
            {isApprover && (
              <Card
                title={
                  <Space>
                    <ClockCircleOutlined style={{ color: '#fa8c16' }} />
                    <span>Awaiting Your Approval</span>
                    {(data?.requestsPendingApproval ?? 0) > 0 && (
                      <Tag color="orange">{data!.requestsPendingApproval}</Tag>
                    )}
                  </Space>
                }
                extra={
                  <Button type="link" size="small" onClick={() => navigate('/requests')}
                    icon={<ArrowRightOutlined />}>
                    Review all
                  </Button>
                }
                styles={{ body: { padding: 0 } }}
              >
                {isLoading
                  ? <div style={{ padding: 20 }}><Skeleton active paragraph={{ rows: 3 }} /></div>
                  : (data?.pendingApprovals?.length ?? 0) === 0
                    ? (
                      <div style={{ padding: '24px 20px', textAlign: 'center' }}>
                        <CheckCircleOutlined style={{ fontSize: 24, color: '#52c41a', marginBottom: 8 }} />
                        <Text type="secondary" style={{ display: 'block' }}>All caught up — no pending approvals</Text>
                      </div>
                    )
                    : (
                      <List<DashboardRequestItem>
                        dataSource={data?.pendingApprovals ?? []}
                        renderItem={item => {
                          const catMeta = CATEGORY_META[item.category as RequestCategory];
                          return (
                            <List.Item
                              style={{ padding: '10px 20px', cursor: 'pointer' }}
                              onClick={() => navigate('/requests')}
                            >
                              <List.Item.Meta
                                title={<Text style={{ fontSize: 13 }}>{item.title}</Text>}
                                description={
                                  <Space size={6}>
                                    <Text code style={{ fontSize: 11 }}>{item.ticketNumber}</Text>
                                    <Tag color={catMeta?.color} style={{ fontSize: 11 }}>{catMeta?.label ?? item.category}</Tag>
                                    <Text type="secondary" style={{ fontSize: 11 }}>
                                      by {item.requestedByName} · {dayjs(item.createdAt).fromNow()}
                                    </Text>
                                  </Space>
                                }
                              />
                              <Button size="small" type="primary"
                                onClick={e => { e.stopPropagation(); navigate('/requests'); }}>
                                Review
                              </Button>
                            </List.Item>
                          );
                        }}
                      />
                    )
                }
              </Card>
            )}

            {/* Recent diesel requisitions — approvers / dispensers only */}
            {isApprover && (
              <Card
                title={
                  <Space>
                    <FileProtectOutlined style={{ color: '#d4b106' }} />
                    <span>Recent Diesel Requisitions</span>
                    {(data?.dieselReqsPending ?? 0) > 0 && (
                      <Tag color="gold">{data!.dieselReqsPending} pending</Tag>
                    )}
                  </Space>
                }
                extra={
                  <Button type="link" size="small" onClick={() => navigate('/fuel')}
                    icon={<ArrowRightOutlined />}>
                    View all
                  </Button>
                }
                styles={{ body: { padding: 0 } }}
              >
                {isLoading
                  ? <div style={{ padding: 20 }}><Skeleton active paragraph={{ rows: 3 }} /></div>
                  : (data?.recentDieselRequisitions?.length ?? 0) === 0
                    ? (
                      <div style={{ padding: '24px 20px', textAlign: 'center' }}>
                        <Text type="secondary">No diesel requisitions yet</Text>
                      </div>
                    )
                    : (
                      <List<DashboardDieselReqItem>
                        dataSource={data?.recentDieselRequisitions ?? []}
                        renderItem={item => (
                          <List.Item
                            style={{ padding: '10px 20px', cursor: 'pointer' }}
                            onClick={() => navigate('/fuel')}
                          >
                            <List.Item.Meta
                              title={
                                <Space size={6}>
                                  <Text style={{ fontSize: 13 }}>
                                    {item.equipmentType} — {item.purpose}
                                  </Text>
                                </Space>
                              }
                              description={
                                <Space size={6} wrap>
                                  <Text code style={{ fontSize: 11 }}>{item.requisitionNumber}</Text>
                                  <Text type="secondary" style={{ fontSize: 11 }}>
                                    {item.quantityRequestedLitres}L requested
                                    {item.quantityDispensedLitres != null && ` · ${item.quantityDispensedLitres}L dispensed`}
                                  </Text>
                                  <Text type="secondary" style={{ fontSize: 11 }}>
                                    {dayjs(item.createdAt).fromNow()}
                                  </Text>
                                </Space>
                              }
                            />
                            <DieselStatusTag status={item.status} />
                          </List.Item>
                        )}
                      />
                    )
                }
              </Card>
            )}
          </Space>
        </Col>

        {/* ── Right column ─────────────────────────────────────────── */}
        <Col xs={24} lg={10}>
          <Space direction="vertical" style={{ width: '100%' }} size={16}>

            {/* Fuel tank gauge */}
            <Card
              title={
                <Space>
                  <FireOutlined style={{ color: '#ff7a45' }} />
                  <span>Fuel &amp; Power</span>
                </Space>
              }
              extra={
                <Button type="link" size="small" onClick={() => navigate('/fuel')}
                  icon={<ArrowRightOutlined />}>
                  View fuel page
                </Button>
              }
            >
              <FuelGauge
                litres={data?.dieselTankLevelLitres ?? null}
                loading={isLoading}
              />
            </Card>

            {/* Active staff feed */}
            <Card
              title={
                <Space>
                  <TeamOutlined style={{ color: '#52c41a' }} />
                  <span>Active Staff</span>
                  {(data?.staffActiveNow ?? 0) > 0 && (
                    <Badge count={data!.staffActiveNow} color="#52c41a" />
                  )}
                </Space>
              }
              extra={
                <Button type="link" size="small" onClick={() => navigate('/activities')}
                  icon={<ArrowRightOutlined />}>
                  Full tracker
                </Button>
              }
              styles={{ body: { padding: 0 } }}
            >
              {isLoading
                ? <div style={{ padding: 20 }}><Skeleton active avatar paragraph={{ rows: 2 }} /></div>
                : (data?.activeStaff?.length ?? 0) === 0
                  ? (
                    <div style={{ padding: '24px 20px', textAlign: 'center' }}>
                      <UserOutlined style={{ fontSize: 24, color: '#d9d9d9', marginBottom: 8 }} />
                      <Text type="secondary" style={{ display: 'block' }}>No staff currently active</Text>
                    </div>
                  )
                  : (
                    <List<DashboardActivityItem>
                      dataSource={data?.activeStaff ?? []}
                      renderItem={item => {
                        const catMeta = ACTIVITY_CATEGORY_META[item.category as ActivityCategory];
                        const color   = getAvatarColor(item.staffName);
                        return (
                          <List.Item style={{ padding: '10px 16px' }}>
                            <List.Item.Meta
                              avatar={
                                <Avatar size={36} style={{ backgroundColor: color, flexShrink: 0 }}>
                                  {getInitials(item.staffName)}
                                </Avatar>
                              }
                              title={
                                <Space size={6}>
                                  <Text strong style={{ fontSize: 13 }}>{item.staffName}</Text>
                                  <Badge status="processing" />
                                  {item.isProxy && (
                                    <Tooltip title={`Logged by ${item.loggedByName}`}>
                                      <Tag style={{ fontSize: 10 }}>Proxy</Tag>
                                    </Tooltip>
                                  )}
                                </Space>
                              }
                              description={
                                <div>
                                  <Text style={{ fontSize: 12, display: 'block' }} ellipsis>
                                    {item.activityDescription}
                                  </Text>
                                  <Space size={6} style={{ marginTop: 2 }}>
                                    {item.location && (
                                      <Text type="secondary" style={{ fontSize: 11 }}>
                                        <EnvironmentOutlined style={{ marginRight: 2 }} />
                                        {item.location}
                                      </Text>
                                    )}
                                    {catMeta && (
                                      <Tag color={catMeta.color} style={{ fontSize: 10 }}>
                                        {catMeta.label}
                                      </Tag>
                                    )}
                                  </Space>
                                </div>
                              }
                            />
                            <Text type="secondary" style={{ fontSize: 11, whiteSpace: 'nowrap' }}>
                              {dayjs(item.startedAt).fromNow()}
                            </Text>
                          </List.Item>
                        );
                      }}
                    />
                  )
              }
            </Card>

            {/* Upcoming / overdue maintenance */}
            <Card
              title={
                <Space>
                  <ToolOutlined style={{ color: (data?.maintenanceOverdue ?? 0) > 0 ? '#ff4d4f' : '#fa8c16' }} />
                  <span>Maintenance Schedule</span>
                  {(data?.maintenanceEscalations ?? 0) > 0 && (
                    <Tag color="red">{data!.maintenanceEscalations} escalated</Tag>
                  )}
                  {(data?.maintenanceEscalations ?? 0) === 0 && (data?.maintenanceOverdue ?? 0) > 0 && (
                    <Tag color="red">{data!.maintenanceOverdue} overdue</Tag>
                  )}
                </Space>
              }
              extra={
                <Button type="link" size="small" onClick={() => navigate('/maintenance')}
                  icon={<ArrowRightOutlined />}>
                  View all
                </Button>
              }
              styles={{ body: { padding: 0 } }}
            >
              {isLoading
                ? <div style={{ padding: 20 }}><Skeleton active paragraph={{ rows: 4 }} /></div>
                : (data?.upcomingMaintenance?.length ?? 0) === 0
                  ? (
                    <div style={{ padding: '24px 20px', textAlign: 'center' }}>
                      <CheckCircleOutlined style={{ fontSize: 24, color: '#52c41a', marginBottom: 8 }} />
                      <Text type="secondary" style={{ display: 'block' }}>No active maintenance schedules</Text>
                    </div>
                  )
                  : (
                    <List<DashboardMaintenanceItem>
                      dataSource={data?.upcomingMaintenance ?? []}
                      renderItem={item => {
                        const catMeta  = MAINTENANCE_CATEGORY_META[item.category as MaintenanceCategory];
                        const overdue  = item.isOverdue;
                        const dayLabel = overdue
                          ? `${Math.abs(item.daysUntilDue)}d overdue`
                          : item.daysUntilDue === 0
                            ? 'Due today'
                            : `Due in ${item.daysUntilDue}d`;

                        return (
                          <List.Item
                            style={{
                              padding: '10px 16px', cursor: 'pointer',
                              background: item.escalationLevel >= 2 ? '#fff1f0'
                                        : item.escalationLevel === 1 ? '#fffbe6'
                                        : undefined,
                            }}
                            onClick={() => navigate('/maintenance')}
                          >
                            <List.Item.Meta
                              avatar={
                                <div style={{
                                  width: 32, height: 32, borderRadius: 6, flexShrink: 0,
                                  background: overdue ? '#fff1f0' : item.daysUntilDue <= 7 ? '#fff7e6' : '#f0f5ff',
                                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                                  fontSize: 15,
                                  color: overdue ? '#ff4d4f' : item.daysUntilDue <= 7 ? '#fa8c16' : '#1677ff',
                                }}>
                                  {overdue ? <WarningOutlined /> : <ClockCircleOutlined />}
                                </div>
                              }
                              title={
                                <Space size={4}>
                                  <Text
                                    style={{ fontSize: 13, color: overdue ? '#ff4d4f' : undefined }}
                                    ellipsis
                                  >
                                    {item.taskName}
                                  </Text>
                                  <EscBadge level={item.escalationLevel} />
                                </Space>
                              }
                              description={
                                <Space size={6}>
                                  {catMeta && (
                                    <Tag color={catMeta.color} style={{ fontSize: 10 }}>{catMeta.label}</Tag>
                                  )}
                                  <Tag style={{ fontSize: 10 }}>{item.frequencyLabel}</Tag>
                                </Space>
                              }
                            />
                            <Text
                              style={{
                                fontSize: 11, whiteSpace: 'nowrap', fontWeight: 600,
                                color: overdue ? '#ff4d4f' : item.daysUntilDue <= 7 ? '#fa8c16' : '#8c8c8c',
                              }}
                            >
                              {dayLabel}
                            </Text>
                          </List.Item>
                        );
                      }}
                    />
                  )
              }
            </Card>

            {/* Low stock items */}
            {(data?.storeLowStockCount ?? 0) > 0 && (
              <Card
                title={
                  <Space>
                    <ShopOutlined style={{ color: '#fa541c' }} />
                    <span>Low Stock Items</span>
                    <Tag color="red">{data!.storeLowStockCount}</Tag>
                  </Space>
                }
                extra={
                  <Button type="link" size="small" onClick={() => navigate('/store')}
                    icon={<ArrowRightOutlined />}>
                    View store
                  </Button>
                }
                styles={{ body: { padding: 0 } }}
              >
                {isLoading
                  ? <div style={{ padding: 20 }}><Skeleton active paragraph={{ rows: 3 }} /></div>
                  : (
                    <List<DashboardStoreItem>
                      dataSource={data?.lowStockItems ?? []}
                      renderItem={item => {
                        const stockPct = item.reorderLevel > 0
                          ? Math.min(100, Math.round((item.quantityInStock / item.reorderLevel) * 100))
                          : 100;
                        const isCritical = item.quantityInStock === 0;
                        return (
                          <List.Item
                            style={{
                              padding: '8px 16px', cursor: 'pointer',
                              background: isCritical ? '#fff1f0' : undefined,
                            }}
                            onClick={() => navigate('/store')}
                          >
                            <List.Item.Meta
                              title={
                                <Space size={6}>
                                  <Text style={{ fontSize: 13 }}>{item.name}</Text>
                                  {isCritical && <Tag color="red" style={{ fontSize: 10 }}>Out of stock</Tag>}
                                </Space>
                              }
                              description={
                                <Space size={6}>
                                  <Text code style={{ fontSize: 11 }}>{item.itemCode}</Text>
                                  <Text type="secondary" style={{ fontSize: 11 }}>{item.category}</Text>
                                </Space>
                              }
                            />
                            <div style={{ textAlign: 'right', minWidth: 90 }}>
                              <Text
                                strong
                                style={{ fontSize: 13, color: isCritical ? '#ff4d4f' : '#fa8c16', display: 'block' }}
                              >
                                {item.quantityInStock} {item.unit}
                              </Text>
                              <Text type="secondary" style={{ fontSize: 11 }}>
                                min: {item.reorderLevel}
                              </Text>
                              <Progress
                                percent={stockPct}
                                showInfo={false}
                                strokeColor={isCritical ? '#ff4d4f' : '#fa8c16'}
                                trailColor="#f5f5f5"
                                style={{ width: 80, margin: '2px 0 0' }}
                                size="small"
                              />
                            </div>
                          </List.Item>
                        );
                      }}
                    />
                  )
                }
              </Card>
            )}

          </Space>
        </Col>
      </Row>
    </div>
  );
}
