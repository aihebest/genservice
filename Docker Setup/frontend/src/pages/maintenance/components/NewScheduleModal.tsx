import { useState } from 'react';
import {
  Modal, Form, Input, Select, InputNumber, DatePicker, Alert, Space,
} from 'antd';
import dayjs from 'dayjs';
import { maintenanceApi } from '../../../api/maintenance.api';
import { MAINTENANCE_CATEGORY_META } from '../../../types';
import type { MaintenanceCategory } from '../../../types';

const { TextArea } = Input;

const FREQUENCY_PRESETS = [
  { label: 'Weekly',    days: 7   },
  { label: 'Monthly',   days: 30  },
  { label: 'Quarterly', days: 90  },
  { label: 'Annually',  days: 365 },
  { label: 'Custom',    days: 0   },
];

const STAFF_LIST = [
  { email: 'technician@demo.local', name: 'Chukwudi Nwosu' },
  { email: 'tech2@demo.local',      name: 'Grace Obi'       },
  { email: 'supervisor@demo.local', name: 'Emeka Okonkwo'   },
];

interface Props {
  open:      boolean;
  onClose:   () => void;
  onCreated: () => void;
}

interface FormValues {
  taskName:        string;
  description?:    string;
  category:        MaintenanceCategory;
  location?:       string;
  frequencyPreset: string;
  frequencyDays?:  number;
  nextDueAt:       ReturnType<typeof dayjs>;
  assignedTo?:     string;
}

export default function NewScheduleModal({ open, onClose, onCreated }: Props) {
  const [form]              = Form.useForm<FormValues>();
  const [loading, setLoading]         = useState(false);
  const [error,   setError]           = useState<string | null>(null);
  const [isCustom, setIsCustom]       = useState(false);

  const handleClose = () => {
    form.resetFields();
    setError(null);
    setIsCustom(false);
    onClose();
  };

  const handleOk = async () => {
    try {
      const vals = await form.validateFields();
      setLoading(true); setError(null);

      const preset     = FREQUENCY_PRESETS.find(p => p.label === vals.frequencyPreset);
      const freqDays   = isCustom ? (vals.frequencyDays ?? 30) : (preset?.days ?? 30);
      const assignedTo = STAFF_LIST.find(s => s.email === vals.assignedTo);

      await maintenanceApi.create({
        taskName:        vals.taskName,
        description:     vals.description,
        category:        vals.category,
        location:        vals.location,
        frequencyLabel:  vals.frequencyPreset,
        frequencyDays:   freqDays,
        nextDueAt:       vals.nextDueAt.toISOString(),
        assignedToEmail: assignedTo?.email,
        assignedToName:  assignedTo?.name,
      });

      onCreated();
      handleClose();
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'response' in e) {
        const err = e as { response?: { data?: { message?: string } } };
        setError(err.response?.data?.message ?? 'Failed to create schedule.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title="New Maintenance Schedule"
      open={open}
      onOk={handleOk}
      onCancel={handleClose}
      okText="Create Schedule"
      confirmLoading={loading}
      width={560}
      destroyOnClose
    >
      {error && (
        <Alert message={error} type="error" showIcon closable
          onClose={() => setError(null)} style={{ marginBottom: 16 }} />
      )}

      <Form form={form} layout="vertical">
        <Form.Item
          name="taskName"
          label="Task Name"
          rules={[{ required: true, message: 'Enter a task name' }]}
        >
          <Input placeholder="e.g. Quarterly Fumigation – All Buildings" maxLength={200} />
        </Form.Item>

        <Space style={{ width: '100%' }} size={12}>
          <Form.Item
            name="category"
            label="Category"
            rules={[{ required: true, message: 'Select a category' }]}
            style={{ flex: 1 }}
          >
            <Select placeholder="Category" style={{ width: '100%' }}>
              {Object.entries(MAINTENANCE_CATEGORY_META).map(([k, m]) => (
                <Select.Option key={k} value={k}>{m.label}</Select.Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="location"
            label="Location"
            style={{ flex: 1 }}
          >
            <Input placeholder="e.g. All Buildings" maxLength={200} />
          </Form.Item>
        </Space>

        <Form.Item name="description" label="Description">
          <TextArea rows={2} maxLength={2000} showCount
            placeholder="Describe the maintenance task…" />
        </Form.Item>

        <Space style={{ width: '100%' }} size={12}>
          <Form.Item
            name="frequencyPreset"
            label="Frequency"
            rules={[{ required: true, message: 'Select frequency' }]}
            style={{ flex: 1 }}
          >
            <Select
              placeholder="Select frequency"
              style={{ width: '100%' }}
              onChange={v => setIsCustom(v === 'Custom')}
            >
              {FREQUENCY_PRESETS.map(p => (
                <Select.Option key={p.label} value={p.label}>{p.label}</Select.Option>
              ))}
            </Select>
          </Form.Item>

          {isCustom && (
            <Form.Item
              name="frequencyDays"
              label="Days interval"
              rules={[{ required: true, message: 'Enter number of days' }]}
              style={{ flex: 1 }}
            >
              <InputNumber min={1} max={365} style={{ width: '100%' }} placeholder="e.g. 14" />
            </Form.Item>
          )}

          <Form.Item
            name="nextDueAt"
            label="Next Due Date"
            rules={[{ required: true, message: 'Select due date' }]}
            style={{ flex: 1 }}
          >
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>
        </Space>

        <Form.Item name="assignedTo" label="Assign To">
          <Select placeholder="Assign to staff member (optional)" allowClear>
            {STAFF_LIST.map(s => (
              <Select.Option key={s.email} value={s.email}>{s.name}</Select.Option>
            ))}
          </Select>
        </Form.Item>
      </Form>
    </Modal>
  );
}
