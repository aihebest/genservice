import { useState } from 'react';
import {
  Alert, Badge, Button, Divider, Form, Input,
  Modal, Select, Space, Tag, Timeline, Tooltip, Typography,
} from 'antd';
import { PlusOutlined, UserOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { taskProgressLogApi } from '../../api/taskProgressLog.api';
import { PROGRESS_STATUS_META } from '../../types';
import type { ProgressStatus } from '../../types';
import { useAuthStore } from '../../store/authStore';

dayjs.extend(relativeTime);

const { Text } = Typography;
const { TextArea } = Input;

const STAFF_OPTIONS = [
  'Chukwudi Nwosu', 'Grace Obi', 'Bola Adeyemi', 'Kwame Asante',
];

interface Props {
  module:    string;   // "Requests" | "Equipment" | "Facility" | "Vehicle"
  entityId:  string;   // GUID of the related entity
  refNumber: string;   // display ref (REQ-2026-0001, E/26/001, etc.)
  taskTitle: string;   // request title or asset name
}

export default function ProgressLogSection({ module, entityId, refNumber, taskTitle }: Props) {
  const role = useAuthStore(s => s.user?.role);
  const qc   = useQueryClient();

  const [open,       setOpen]       = useState(false);
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState<string | null>(null);
  const [isProxy,    setIsProxy]    = useState(false);
  const [form]       = Form.useForm();

  const isApprover = ['DepartmentManager', 'Supervisor', 'SystemAdmin'].includes(role ?? '');
  const queryKey   = ['task-logs', module, entityId];

  const { data: logs = [], isFetching } = useQuery({
    queryKey,
    queryFn: () => taskProgressLogApi.list(module, entityId),
    enabled: !!entityId,
  });

  const handleSubmit = async (values: Record<string, string>) => {
    setLoading(true); setError(null);
    try {
      await taskProgressLogApi.create({
        module, entityId, refNumber, taskTitle,
        activityPerformed: values.activityPerformed,
        progressStatus:    values.progressStatus,
        materialsRequired: values.materialsRequired || undefined,
        nextAction:        values.nextAction        || undefined,
        isProxy,
        proxyForName:      isProxy ? values.proxyForName : undefined,
      });
      form.resetFields(); setIsProxy(false); setOpen(false);
      qc.invalidateQueries({ queryKey });
    } catch (e: unknown) {
      setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? 'Failed to save.');
    } finally { setLoading(false); }
  };

  return (
    <div style={{ marginTop: 8 }}>
      <Divider orientation="left" orientationMargin={0} style={{ fontSize: 12 }}>
        <Space>
          Progress Log
          {logs.length > 0 && <Badge count={logs.length} size="small" style={{ background: '#1677ff' }} />}
        </Space>
      </Divider>

      {/* Add update button */}
      <Button
        size="small"
        icon={<PlusOutlined />}
        onClick={() => setOpen(true)}
        style={{ marginBottom: logs.length > 0 ? 12 : 0 }}
      >
        Log Daily Update
      </Button>

      {/* Timeline of existing logs */}
      {isFetching ? null : logs.length > 0 ? (
        <Timeline
          style={{ marginTop: 12 }}
          items={logs.map(log => {
            const meta = PROGRESS_STATUS_META[log.progressStatus as ProgressStatus];
            return {
              color: meta?.color === 'green' ? 'green'
                   : meta?.color === 'red'   ? 'red'
                   : 'blue',
              children: (
                <div style={{ marginBottom: 4 }}>
                  <Space wrap style={{ marginBottom: 4 }}>
                    <Text strong style={{ fontSize: 12 }}>
                      {dayjs(log.logDate).format('D MMM YYYY')}
                    </Text>
                    <Tag color={meta?.color} style={{ fontSize: 11 }}>{meta?.label ?? log.progressStatus}</Tag>
                    {log.isProxy && (
                      <Tooltip title={`Logged by ${log.loggedByName} on behalf of ${log.proxyForName}`}>
                        <Tag icon={<UserOutlined />} color="purple" style={{ fontSize: 10 }}>
                          Proxy
                        </Tag>
                      </Tooltip>
                    )}
                  </Space>
                  <Text style={{ fontSize: 13, display: 'block', whiteSpace: 'pre-wrap' }}>
                    {log.activityPerformed}
                  </Text>
                  {log.materialsRequired && (
                    <Text type="warning" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
                      🔧 Materials needed: {log.materialsRequired}
                    </Text>
                  )}
                  {log.nextAction && (
                    <Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
                      ➡ Next: {log.nextAction}
                    </Text>
                  )}
                  <Text type="secondary" style={{ fontSize: 11 }}>
                    — {log.isProxy ? `${log.loggedByName} (for ${log.proxyForName})` : log.loggedByName}
                    {' · '}{dayjs(log.createdAt).fromNow()}
                  </Text>
                </div>
              ),
            };
          })}
        />
      ) : (
        <Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 8 }}>
          No progress updates yet.
        </Text>
      )}

      {/* Log update modal */}
      <Modal
        title="Log Daily Progress Update"
        open={open}
        onOk={() => form.submit()}
        onCancel={() => { setOpen(false); form.resetFields(); setIsProxy(false); setError(null); }}
        okText="Save Update"
        confirmLoading={loading}
        width={520}
        destroyOnClose
      >
        {error && <Alert message={error} type="error" showIcon style={{ marginBottom: 12 }} />}

        {/* Proxy toggle — supervisors/managers only */}
        {isApprover && (
          <div style={{
            background: '#f0f5ff', borderRadius: 6, padding: '10px 14px',
            marginBottom: 16, display: 'flex', alignItems: 'center', gap: 12,
          }}>
            <Text style={{ fontSize: 13 }}>Logging on behalf of a technician?</Text>
            <Button
              size="small"
              type={isProxy ? 'primary' : 'default'}
              onClick={() => setIsProxy(p => !p)}
            >
              {isProxy ? '✓ Proxy Mode On' : 'Enable Proxy'}
            </Button>
          </div>
        )}

        <Form form={form} layout="vertical" onFinish={handleSubmit}>
          {isProxy && (
            <Form.Item name="proxyForName" label="Logging on behalf of" rules={[{ required: true }]}>
              <Select placeholder="Select technician…"
                options={STAFF_OPTIONS.map(n => ({ value: n, label: n }))} />
            </Form.Item>
          )}

          <Form.Item name="progressStatus" label="Current Status" rules={[{ required: true }]} initialValue="WorkInProgress">
            <Select
              options={Object.entries(PROGRESS_STATUS_META).map(([k, m]) => ({
                value: k,
                label: <Tag color={m.color}>{m.label}</Tag>,
              }))}
            />
          </Form.Item>

          <Form.Item name="activityPerformed" label="Activity Performed Today" rules={[{ required: true, message: 'Describe what was done' }]}>
            <TextArea
              rows={3}
              placeholder="Describe the work done today, findings, actions taken…"
              maxLength={2000}
              showCount
            />
          </Form.Item>

          <Form.Item name="materialsRequired" label="Materials / Parts Required (if any)">
            <Input placeholder="e.g. 2 x Compressor gasket, 500g R22 gas, 10L engine oil…" maxLength={1000} />
          </Form.Item>

          <Form.Item name="nextAction" label="Next Action / Follow-up">
            <Input placeholder="e.g. Return tomorrow to complete, awaiting vendor quotation…" maxLength={1000} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
