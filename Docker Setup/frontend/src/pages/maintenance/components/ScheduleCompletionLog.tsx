import { useState } from 'react';
import { DatePicker, Drawer, Space, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import dayjs from 'dayjs';
import { maintenanceApi } from '../../../api/maintenance.api';
import type { ScheduleCompletion } from '../../../api/maintenance.api';
import { MAINTENANCE_CATEGORY_META } from '../../../types';
import FilterableTable from '../../../components/shared/FilterableTable';

const { Text } = Typography;

/**
 * The scheduler's completion log — every time a recurring task (fumigation, AC
 * service, water/diesel tank cleaning, evacuation drill…) was actually carried
 * out, with the real date it happened and who did it.
 *
 * Opened for all schedules from the toolbar, or for a single task from its row.
 */
export default function ScheduleCompletionLog(props: {
  open: boolean;
  onClose: () => void;
  /** Limit to one schedule; omit for the full log. */
  scheduleId?: string;
  title?: string;
}) {
  const [range, setRange] = useState<[dayjs.Dayjs, dayjs.Dayjs] | null>(null);

  const { data = [], isFetching } = useQuery({
    queryKey: ['maintenance', 'completions', props.scheduleId, range?.[0]?.format('YYYY-MM-DD'), range?.[1]?.format('YYYY-MM-DD')],
    queryFn: () => maintenanceApi.completions({
      scheduleId: props.scheduleId,
      from: range?.[0]?.format('YYYY-MM-DD'),
      to:   range?.[1]?.format('YYYY-MM-DD'),
    }),
    enabled: props.open,
  });

  return (
    <Drawer title={props.title ?? 'Completion Log'} width={980} open={props.open} onClose={props.onClose}
      extra={
        <Space>
          <Text type="secondary" style={{ fontSize: 12 }}>Date done</Text>
          <DatePicker.RangePicker format="D MMM YYYY" allowClear
            value={range} onChange={v => setRange(v as [dayjs.Dayjs, dayjs.Dayjs] | null)} />
        </Space>
      }>
      <Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
        Each entry is one occasion the task was actually carried out. Recorded when a
        scheduled task is marked Done.
      </Text>
      <FilterableTable<ScheduleCompletion>
        rowKey="id" size="small" loading={isFetching} dataSource={data}
        pagination={{ pageSize: 20, showSizeChanger: false }}
        columns={[
          { title: 'Date Done', dataIndex: 'completedOn', key: 'completedOn', width: 120,
            defaultSortOrder: 'descend',
            render: (v: string) => dayjs(v).format('D MMM YYYY') },
          { title: 'Task', dataIndex: 'taskName', key: 'taskName', width: 220, ellipsis: true },
          { title: 'Category', dataIndex: 'category', key: 'category', width: 140,
            render: (v: string) => {
              const m = MAINTENANCE_CATEGORY_META[v as keyof typeof MAINTENANCE_CATEGORY_META];
              return <Tag color={m?.color}>{m?.label ?? v}</Tag>;
            } },
          { title: 'Location', dataIndex: 'location', key: 'location', width: 140, ellipsis: true,
            render: (v?: string) => v || '—' },
          { title: 'Done By', dataIndex: 'doneBy', key: 'doneBy', width: 150, ellipsis: true,
            render: (v?: string) => v || '—' },
          { title: 'Notes', dataIndex: 'notes', key: 'notes', ellipsis: true,
            render: (v?: string) => v || '—' },
          { title: 'Logged By', dataIndex: 'loggedByName', key: 'loggedByName', width: 140, ellipsis: true },
        ]}
      />
    </Drawer>
  );
}
