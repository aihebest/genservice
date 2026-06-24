import { useState } from 'react';
import { Modal, Form, Input, Select, Alert, Tag } from 'antd';
import { requestsApi } from '../../../api/requests.api';
import { CATEGORY_META, PRIORITY_META, OFFICE_LOCATIONS } from '../../../types';
import type { CreateRequestDto, RequestCategory } from '../../../types';

const { TextArea } = Input;

interface Props {
  open:      boolean;
  onClose:   () => void;
  onCreated: () => void;
}

export default function NewRequestModal({ open, onClose, onCreated }: Props) {
  const [form]           = Form.useForm<CreateRequestDto & { locationSelect: string; locationOther: string }>();
  const [loading,        setLoading]        = useState(false);
  const [error,          setError]          = useState<string | null>(null);
  const [selectedCat,    setSelectedCat]    = useState<RequestCategory | null>(null);
  const [locationSelect, setLocationSelect] = useState<string | null>(null);

  const handleSubmit = async (values: CreateRequestDto & { locationSelect: string; locationOther?: string }) => {
    setLoading(true);
    setError(null);
    try {
      // Resolve location: if "Other" was chosen, use the free-text field
      const location = values.locationSelect === 'Other'
        ? (values.locationOther?.trim() ?? 'Other')
        : values.locationSelect;

      await requestsApi.create({
        title:             values.title,
        description:       values.description,
        category:          values.category,
        priority:          values.priority,
        location,
        lineManagerEmail:  values.lineManagerEmail,
        lineManagerName:   values.lineManagerName,
      });
      form.resetFields();
      setSelectedCat(null);
      setLocationSelect(null);
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
    setLocationSelect(null);
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

        <Form.Item name="category" label="Request Type"
          rules={[{ required: true, message: 'Select a request type' }]}>
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

        <Form.Item name="title" label="Request Title"
          rules={[{ required: true, message: 'Enter a title' }, { max: 250 }]}>
          <Input placeholder="Brief description of what you need…" />
        </Form.Item>

        <Form.Item name="description" label="Details"
          rules={[{ required: true, message: 'Provide more details' }]}>
          <TextArea
            rows={4}
            placeholder="Provide full details — equipment, location info, urgency reason, supporting context…"
            maxLength={4000}
            showCount
          />
        </Form.Item>

        {/* ── Location dropdown ─────────────────────────────────── */}
        <Form.Item name="locationSelect" label="Office / Location"
          rules={[{ required: true, message: 'Select a location' }]}>
          <Select
            placeholder="Select office or site…"
            onChange={(v: string) => setLocationSelect(v)}
            options={OFFICE_LOCATIONS.map(loc => ({ value: loc, label: loc }))}
          />
        </Form.Item>

        {locationSelect === 'Other' && (
          <Form.Item name="locationOther" label="Specify Location"
            rules={[{ required: true, message: 'Please specify the location' }]}>
            <Input placeholder="Enter the specific location or area…" />
          </Form.Item>
        )}

        <Form.Item name="priority" label="Priority" initialValue="Normal"
          rules={[{ required: true }]}>
          <Select options={
            Object.entries(PRIORITY_META).map(([key, meta]) => ({
              value: key,
              label: <Tag color={meta.color}>{meta.label}</Tag>,
            }))
          } />
        </Form.Item>

        {/* ── Line manager fields — only for approval-required categories ── */}
        {needsApproval && (
          <>
            <Form.Item
              name="lineManagerName"
              label="Your Line Manager's Name"
              rules={[{ required: true, message: "Enter your line manager's name" }]}
            >
              <Input placeholder="e.g. John Adebayo" />
            </Form.Item>

            <Form.Item
              name="lineManagerEmail"
              label="Your Line Manager's Email"
              rules={[
                { required: true, message: "Enter your line manager's work email" },
                { type: 'email', message: 'Enter a valid email address' },
              ]}
            >
              <Input placeholder="e.g. j.adebayo@desicongroup.com" />
            </Form.Item>

            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 8 }}
              message="Your line manager will receive an email with Approve / Reject buttons. They do not need to log into the platform."
            />
          </>
        )}

      </Form>
    </Modal>
  );
}
