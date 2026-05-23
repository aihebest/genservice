import { Card, Col, Row, Statistic, Spin } from 'antd';
import {
  FileTextOutlined, ClockCircleOutlined, CheckCircleOutlined,
  CloseCircleOutlined, SyncOutlined, FlagOutlined,
} from '@ant-design/icons';
import type { RequestStats as Stats } from '../../../types';

interface Props { stats?: Stats; loading?: boolean; }

const CARDS = [
  { key: 'open',            label: 'Open',             icon: <FileTextOutlined />,    colour: '#1677ff' },
  { key: 'pendingApproval', label: 'Pending Approval', icon: <ClockCircleOutlined />, colour: '#fa8c16' },
  { key: 'inProgress',      label: 'In Progress',      icon: <SyncOutlined spin />,   colour: '#722ed1' },
  { key: 'completed',       label: 'Completed',        icon: <CheckCircleOutlined />, colour: '#52c41a' },
  { key: 'rejected',        label: 'Rejected',         icon: <CloseCircleOutlined />, colour: '#ff4d4f' },
  { key: 'total',           label: 'Total (all time)', icon: <FlagOutlined />,        colour: '#8c8c8c' },
] as const;

export default function RequestStats({ stats, loading }: Props) {
  return (
    <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
      {CARDS.map(c => (
        <Col key={c.key} xs={12} sm={8} md={8} lg={4}>
          <Card styles={{ body: { padding: '14px 18px' } }}>
            <Spin spinning={loading ?? false} size="small">
              <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
                <div style={{
                  width: 38, height: 38, borderRadius: 8, flexShrink: 0,
                  background: c.colour + '18', color: c.colour,
                  display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 17,
                }}>
                  {c.icon}
                </div>
                <Statistic
                  title={<span style={{ fontSize: 11 }}>{c.label}</span>}
                  value={stats?.[c.key] ?? 0}
                  valueStyle={{ fontSize: 22, fontWeight: 700, color: c.colour }}
                />
              </div>
            </Spin>
          </Card>
        </Col>
      ))}
    </Row>
  );
}
