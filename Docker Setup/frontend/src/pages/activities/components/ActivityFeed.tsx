import { Avatar, Badge, Card, Space, Tag, Typography, Tooltip } from 'antd';
import { UserOutlined, EnvironmentOutlined, TagOutlined } from '@ant-design/icons';
import type { StaffActivity } from '../../../types';
import { ACTIVITY_STATUS_META, ACTIVITY_CATEGORY_META } from '../../../types';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';

dayjs.extend(relativeTime);

const { Text } = Typography;

function getInitials(name: string) {
  return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
}

function getAvatarColor(email: string) {
  const colors = ['#1677ff', '#52c41a', '#fa8c16', '#722ed1', '#eb2f96', '#13c2c2'];
  let hash = 0;
  for (const ch of email) hash = ch.charCodeAt(0) + ((hash << 5) - hash);
  return colors[Math.abs(hash) % colors.length];
}

interface Props {
  activities: StaffActivity[];
  onStatusUpdate?: (id: string, status: string) => void;
  canManage?: boolean;
}

export default function ActivityFeed({ activities, onStatusUpdate, canManage }: Props) {
  if (activities.length === 0) {
    return (
      <div style={{ textAlign: 'center', padding: '40px 0', color: '#999' }}>
        No active staff activities right now.
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {activities.map(a => {
        const sm       = ACTIVITY_STATUS_META[a.status] ?? ACTIVITY_STATUS_META.Active;
        const catMeta  = ACTIVITY_CATEGORY_META[a.category];
        const initials = getInitials(a.staffName);
        const color    = getAvatarColor(a.staffEmail);

        return (
          <Card
            key={a.id}
            size="small"
            styles={{ body: { padding: '12px 16px' } }}
            style={{ borderLeft: `3px solid ${a.status === 'Active' ? '#52c41a' : a.status === 'Paused' ? '#fa8c16' : '#d9d9d9'}` }}
          >
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
              {/* Avatar */}
              <Avatar size={40} style={{ backgroundColor: color, flexShrink: 0 }}>
                {initials}
              </Avatar>

              {/* Content */}
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                  <Text strong style={{ fontSize: 14 }}>{a.staffName}</Text>
                  <Badge status={sm.badge as any} text={
                    <Text style={{ fontSize: 12, color: '#595959' }}>{sm.label}</Text>
                  } />
                  {a.isProxy && (
                    <Tooltip title={`Logged by ${a.loggedByName}`}>
                      <Tag color="geekblue" style={{ fontSize: 11 }}>Proxy</Tag>
                    </Tooltip>
                  )}
                </div>

                <Text style={{ fontSize: 13, display: 'block', marginTop: 2 }}>
                  {a.activityDescription}
                </Text>

                <Space size={12} style={{ marginTop: 6 }} wrap>
                  {a.location && (
                    <Text type="secondary" style={{ fontSize: 12 }}>
                      <EnvironmentOutlined style={{ marginRight: 3 }} />{a.location}
                    </Text>
                  )}
                  {catMeta && (
                    <Tag icon={<TagOutlined />} color={catMeta.color} style={{ fontSize: 11 }}>
                      {catMeta.label}
                    </Tag>
                  )}
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    Started {dayjs(a.startedAt).fromNow()}
                  </Text>
                </Space>
              </div>

              {/* Action */}
              {canManage && onStatusUpdate && a.status === 'Active' && (
                <div style={{ flexShrink: 0 }}>
                  <Space size={4}>
                    <Tag
                      color="orange"
                      style={{ cursor: 'pointer', margin: 0 }}
                      onClick={() => onStatusUpdate(a.id, 'Paused')}
                    >
                      Pause
                    </Tag>
                    <Tag
                      color="default"
                      style={{ cursor: 'pointer', margin: 0 }}
                      onClick={() => onStatusUpdate(a.id, 'Completed')}
                    >
                      Complete
                    </Tag>
                  </Space>
                </div>
              )}
            </div>
          </Card>
        );
      })}
    </div>
  );
}
