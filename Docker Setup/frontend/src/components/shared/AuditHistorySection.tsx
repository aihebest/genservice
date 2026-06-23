import { Divider, Empty, Space, Tag, Timeline, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { auditApi } from '../../api/audit.api';
import { AUDIT_ACTION_META } from '../../types';
import type { AuditEntry } from '../../types';

dayjs.extend(relativeTime);

const { Text } = Typography;

interface Props {
  entityType: string;
  entityId:   string;
}

export default function AuditHistorySection({ entityType, entityId }: Props) {
  const { data, isFetching } = useQuery({
    queryKey: ['audit', entityType, entityId],
    queryFn:  () => auditApi.list({ entityType, entityId, days: 365 }),
    enabled:  !!entityId,
  });

  const entries = data?.items ?? [];

  return (
    <div style={{ marginTop: 8 }}>
      <Divider titlePlacement="left" orientationMargin={0} style={{ fontSize: 12 }}>
        Audit History
      </Divider>

      {isFetching ? (
        <Text type="secondary" style={{ fontSize: 12 }}>Loading history…</Text>
      ) : entries.length === 0 ? (
        <Empty description="No audit trail yet" image={Empty.PRESENTED_IMAGE_SIMPLE}
          style={{ padding: '8px 0' }} />
      ) : (
        <Timeline
          items={entries.map((e: AuditEntry) => {
            const meta = AUDIT_ACTION_META[e.action] ?? { label: e.action, color: 'default' };
            return {
              color: meta.color === 'green' ? 'green' : meta.color === 'red' ? 'red' : 'blue',
              children: (
                <div style={{ marginBottom: 4 }}>
                  <Space wrap style={{ marginBottom: 2 }}>
                    <Tag color={meta.color} style={{ fontSize: 11 }}>{meta.label}</Tag>
                    <Text strong style={{ fontSize: 12 }}>{e.performedByName}</Text>
                    <Text type="secondary" style={{ fontSize: 11 }}>
                      {dayjs(e.timestamp).fromNow()}
                    </Text>
                  </Space>
                  {(e.oldValue || e.newValue) && (
                    <div style={{ fontSize: 12 }}>
                      {e.oldValue && (
                        <Text type="secondary" style={{ marginRight: 4 }}>
                          {e.oldValue}
                        </Text>
                      )}
                      {e.oldValue && e.newValue && <Text type="secondary"> → </Text>}
                      {e.newValue && (
                        <Text strong style={{ color: '#1677ff' }}>{e.newValue}</Text>
                      )}
                    </div>
                  )}
                  {e.details && (
                    <Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
                      {e.details}
                    </Text>
                  )}
                </div>
              ),
            };
          })}
        />
      )}
    </div>
  );
}
