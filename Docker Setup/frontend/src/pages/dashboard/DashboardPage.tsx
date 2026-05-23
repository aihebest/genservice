import { useNavigate } from 'react-router-dom';
import {
  Alert, Avatar, Badge, Button, Card, Col, List, Row,
  Skeleton, Space, Statistic, Tag, Tooltip, Typography,
} from 'antd';
import {
  FormOutlined, TeamOutlined, ToolOutlined,
  WarningOutlined, ClockCircleOutlined, CheckCircleOutlined,
  ReloadOutlined, ArrowRightOutlined, ThunderboltOutlined,
  EnvironmentOutlined, UserOutlined,
} from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { dashboardApi } from '../../api/dashboard.api';
import type { DashboardRequestItem, DashboardActivityItem, DashboardMaintenanceItem } from '../../api/dashboard.api';
import { useAuthStore } from '../../store/authStore';
import { CATEGORY_META, STATUS_META, PRIORITY_META, ACTIVITY_CATEGORY_META, MAINTENANCE_CATEGORY_META } from '../../types';
import type { RequestCategory, RequestStatus, RequestPriority, ActivityCategory, MaintenanceCategory } from '../../types';

dayjs.extend(relativeTime);

const { Title, Text } = Typography;

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
  title:   string;
  value:   number | undefined;
  icon:    React.ReactNode;
  color:   string;
  loading: boolean;
  onClick?: () => void;
  alert?:  boolean;
}
function KpiCard({ title, value, icon, color, loading, onClick, alert }: KpiProps) {
  return (
    <Card
      styles={{ body: { padding: '16px 20px' } }}
      style={{ cursor: onClick ? 'pointer' : 'default', borderColor: alert ? '#ff4d4f' : undefined }}
      onClick={onClick}
      hoverable={!!onClick}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
        <div style={{
          width: 44, height: 44, borderRadius: 10,
          background: color + '18',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 20, color, flexShrink: 0,
        }}>
          {icon}
        </div>
        {loading
          ? <Skeleton.Button active style={{ width: 80, height: 40 }} />
          : <Statistic
              title={<span style={{ fontSize: 12, color: '#8c8c8c' }}>{title}</span>}
              value={value ?? 0}
              valueStyle={{ fontSize: 26, fontWeight: 700, color: alert && (value ?? 0) > 0 ? '#ff4d4f' : color }}
            />
        }
      </div>
    </Card>
  );
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
    refetchInterval: 30_000,   // auto-refresh every 30s
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
            {dayjs().format('dddd, D MMMM YYYY')} — General Service Operations Overview
          </Text>
        </Col>
        <Col>
          <Tooltip title="Refresh dashboard">
            <Button icon={<ReloadOutlined />} onClick={refresh} loading={isFetching} />
          </Tooltip>
        </Col>
      </Row>

      {/* ── Pending approvals alert (managers only) ──────────────────── */}
      {isApprover && (data?.requestsPendingApproval ?? 0) > 0 && (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined />}
          message={
            <span>
              <strong>{data!.requestsPendingApproval} request{data!.requestsPendingApproval > 1 ? 's' : ''}</strong>
              {' '}waiting for your approval.
            </span>
          }
          action={
            <Button size="small" onClick={() => navigate('/requests')} icon={<ArrowRightOutlined />}>
              Review
            </Button>
          }
          style={{ marginBottom: 20 }}
        />
      )}

      {/* ── Overdue maintenance alert ────────────────────────────────── */}
      {(data?.maintenanceOverdue ?? 0) > 0 && (
        <Alert
          type="error"
          showIcon
          icon={<WarningOutlined />}
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
          style={{ marginBottom: 20 }}
        />
      )}

      {/* ── KPI row ─────────────────────────────────────────────────── */}
      <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
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

      {/* ── Main panels ─────────────────────────────────────────────── */}
      <Row gutter={[16, 16]}>

        {/* ── Left column: Recent Requests + Pending Approvals ──────── */}
        <Col xs={24} lg={14}>
          <Space direction="vertical" style={{ width: '100%' }} size={16}>

            {/* Recent requests */}
            <Card
              title={<Space><FormOutlined /><span>Recent Requests</span></Space>}
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

            {/* Pending approvals — only show for approvers */}
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
                              <Button size="small" type="primary" onClick={e => { e.stopPropagation(); navigate('/requests'); }}>
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
          </Space>
        </Col>

        {/* ── Right column: Active Staff + Maintenance ───────────────── */}
        <Col xs={24} lg={10}>
          <Space direction="vertical" style={{ width: '100%' }} size={16}>

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
                                      <Tag color={catMeta.color} style={{ fontSize: 10, marginRight: 0 }}>
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

            {/* Upcoming maintenance */}
            <Card
              title={
                <Space>
                  <ToolOutlined style={{ color: (data?.maintenanceOverdue ?? 0) > 0 ? '#ff4d4f' : '#fa8c16' }} />
                  <span>Maintenance Schedule</span>
                  {(data?.maintenanceOverdue ?? 0) > 0 && (
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
                            style={{ padding: '10px 16px', cursor: 'pointer' }}
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
                                <Text
                                  style={{ fontSize: 13, color: overdue ? '#ff4d4f' : undefined }}
                                  ellipsis
                                >
                                  {item.taskName}
                                </Text>
                              }
                              description={
                                <Space size={6}>
                                  {catMeta && (
                                    <Tag color={catMeta.color} style={{ fontSize: 10, marginRight: 0 }}>
                                      {catMeta.label}
                                    </Tag>
                                  )}
                                  <Tag style={{ fontSize: 10, marginRight: 0 }}>{item.frequencyLabel}</Tag>
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

          </Space>
        </Col>
      </Row>
    </div>
  );
}
