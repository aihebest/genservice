import { useState } from 'react';
import { Modal, Form, Input, Select, Alert, Tag, Typography } from 'antd';
import { requestsApi } from '../../../api/requests.api';
import { CATEGORY_META, PRIORITY_META } from '../../../types';
import type { CreateRequestDto, RequestCategory, RequestPriority } from '../../../types';

const { TextArea } = Input;
const { Text }     = Typography;

interface Props {
  open:    boolean;
  onClose: () => void;
  onCreated: () => void;
}

export default function NewRequestModal({ open, onClose, onCreated }: Props) {
  const [form]    = Form.useForm<CreateRequestDto>();
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [selectedCat, setSelectedCat] = useState<RequestCategory | null>(null);

  const handleSubmit = async (values: CreateRequestDto) => {
    setLoading(true);
    setError(null);
    try {
      await requestsApi.create(values);
      form.resetFields();
      setSelectedCat(null);
      onCreated();
      onClose();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })
        ?.response?.data?.message ?? 'Failed to submit request.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    form.resetFields();
    setSelectedCat(null);
    setError(null);
    onClose();
  };

  const needsApproval = selectedCat ? CATEGORY_META[selectedCat].requiresApproval : false;

  return (
    <Modal
      title="Raise a New Request"
      open={open}
      onOk={() => form.submit()}
      onCancel={handleCancel}
      okText="Submit Request"
      confirmLoading={loading}
      width={560}
      destroyOnClose
    >
      {error && (
        <Alert message={error} type="error" showIcon closable
          onClose={() => setError(null)} style={{ marginBottom: 16 }} />
      )}

      {selectedCat && (
        <Alert
          style={{ marginBottom: 16 }}
          type={needsApproval ? 'warning' : 'info'}
          showIcon
          message={
            needsApproval
              ? 'This request type requires management approval before it is processed.'
              : 'This request will be routed directly to the operations team for processing.'
          }
        />
      )}

      <Form form={form} layout="vertical" onFinish={handleSubmit} requiredMark>

        <Form.Item name="category" label="Request Type" rules={[{ required: true, message: 'Select a request type' }]}>
          <Select
            placeholder="Select category…"
            onChange={(v: RequestCategory) => setSelectedCat(v)}
            options={Object.entries(CATEGORY_META).map(([key, meta]) => ({
              value: key,
              label: (
                <span>
                  <Tag color={meta.color} style={{ fontSize: 11, marginRight: 6 }}>
                    {meta.requiresApproval ? 'Approval' : 'Direct'}
                  </Tag>
                  {meta.label}
                </span>
              ),
            }))}
          />
        </Form.Item>

        <Form.Item name="title" label="Request Title" rules={[{ required: true, message: 'Enter a title' }, { max: 250 }]}>
          <Input placeholder="Brief description of what you need…" />
        </Form.Item>

        <Form.Item name="description" label="Details" rules={[{ required: true, message: 'Provide more details' }]}>
          <TextArea
            rows={4}
            placeholder="Provide full details — include any relevant equipment, location info, urgency reason, or supporting context…"
            maxLength={4000}
            showCount
          />
        </Form.Item>

        <Form.Item name="location" label="Location / Area" rules={[{ required: true, message: 'Enter a location' }]}>
          <Input placeholder="e.g. Conference Room B, Block C Rooftop, Server Room…" />
        </Form.Item>

        <Form.Item name="priority" label="Priority" initialValue="Normal" rules={[{ required: true }]}>
          <Select options={
            Object.entries(PRIORITY_META).map(([key, meta]) => ({
              value: key,
              label: <Tag color={meta.color}>{meta.label}</Tag>,
            }))
          } />
        </Form.Item>

      </Form>
    </Modal>
  );
}
