import { Card, Empty, Typography, Tag, Space } from 'antd';
import { CarOutlined } from '@ant-design/icons';

const { Title, Paragraph } = Typography;

export default function FleetPage() {
  return (
    <div>
      <div style={{ marginBottom: 24 }}>
        <Space align="center">
          <CarOutlined style={{ fontSize: 22, color: '#52c41a' }} />
          <Title level={4} style={{ margin: 0 }}>Fleet &amp; Vehicles</Title>
        </Space>
        <div style={{ marginTop: 4 }}>
          <Tag color="green">Vehicle Tracking</Tag>
          <Tag color="cyan">Driver Assignment</Tag>
          <Tag color="orange">Usage Logs</Tag>
        </div>
      </div>

      <Card>
        <Empty
          description={
            <Paragraph type="secondary" style={{ margin: 0 }}>
              Fleet Management module — coming in Phase 3.<br />
              Track vehicles, assign drivers, log trips, and monitor fuel consumption.
            </Paragraph>
          }
        />
      </Card>
    </div>
  );
}
