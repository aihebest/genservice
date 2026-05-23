import { useState } from 'react';
import {
  Modal, Form, Input, Select, Switch, Space, Alert, Typography,
} from 'antd';
import { activitiesApi } from '../../../api/activities.api';
import { ACTIVITY_CATEGORY_META } from '../../../types';
import type { ActivityCategory } from '../../../types';
import { useAuthStore } from '../../../store/authStore';

const { TextArea } = Input;
const { Text }     = Typography;

// Demo staff for proxy selection
const STAFF_LIST = [
  { email: 'technician@demo.local', name: 'Chukwudi Nwosu' },
  { email: 'tech2@demo.local',      name: 'Grace Obi'       },
  { email: 'driver@demo.local',     name: 'Bola Adeyemi'    },
  { email: 'driver2@demo.local',    name: 'Kwame Asante'    },
];

interface Props {
  open:      boolean;
  onClose:   () => void;
  onCreated: () => void;
}

interface FormValues {
  staffEmail?:         string;
  activityDescription: string;
  category:            ActivityCategory;
  location:            string;
  notes?:              string;
}

export default function LogActivityModal({ open, onClose, onCreated }: Props) {
  const user = useAuthStore(s => s.user);
  const role = user?.role;

  const canProxy = role === 'DepartmentManager' || role === 'Supervisor' || role === 'SystemAdmin';

  const [form]        = Form.useForm<FormValues>();
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState<string | null>(null);
  const [isProxy,  setIsProxy]  = useState(false);

  const handleClose = () => {
    form.resetFields();
    setError(null);
    setIsProxy(false);
    onClose();
  };

  const handleOk = async () => {
    try {
      const vals = await form.validateFields();
      setLoading(true);
      setError(null);

      const req = {
        staffEmail:          isProxy ? (vals.staffEmail ?? '') : (user?.email ?? ''),
        staffName:           isProxy
          ? (STAFF_LIST.find(s => s.email === vals.staffEmail)?.name ?? '')
          : (user?.fullName ?? ''),
        activityDescription: vals.activityDescription,
        category:            vals.category,
        location:            vals.location,
        notes:               vals.notes,
      };

      if (isProxy) {
        await activitiesApi.proxyLog(req);
      } else {
        await activitiesApi.log(req);
      }

      onCreated();
      handleClose();
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'response' in e) {
        const err = e as { response?: { data?: { message?: string } } };
        setError(err.response?.data?.message ?? 'Failed to log activity.');
      }
      // form validation errors are silently handled by antd
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={isProxy ? 'Log Activity on Behalf of Staff' : 'Log My Activity'}
      open={open}
      onOk={handleOk}
      onCancel={handleClose}
      okText="Log Activity"
      confirmLoading={loading}
      width={520}
      destroyOnClose
    >
      {error && (
        <Alert message={error} type="error" showIcon closable
          onClose={() => setError(null)} style={{ marginBottom: 16 }} />
      )}

      {canProxy && (
        <div style={{ marginBottom: 16, display: 'flex', alignItems: 'center', gap: 8 }}>
          <Switch checked={isProxy} onChange={setIsProxy} size="small" />
          <Text>Log on behalf of another staff member</Text>
        </div>
      )}

      <Form form={form} layout="vertical">
        {isProxy && (
          <Form.Item
            name="staffEmail"
            label="Staff Member"
            rules={[{ required: true, message: 'Select a staff member' }]}
          >
            <Select placeholder="Select staff member">
              {STAFF_LIST.map(s => (
                <Select.Option key={s.email} value={s.email}>
                  {s.name}
                </Select.Option>
              ))}
            </Select>
          </Form.Item>
        )}

        <Form.Item
          name="activityDescription"
          label="Activity Description"
          rules={[
            { required: true, message: 'Enter a description' },
            { min: 10,        message: 'Minimum 10 characters' },
          ]}
        >
          <TextArea
            rows={3}
            maxLength={500}
            showCount
            placeholder="e.g. Repairing faulty AC unit in Conference Room B"
          />
        </Form.Item>

        <Space style={{ width: '100%' }} size={12}>
          <Form.Item
            name="category"
            label="Category"
            rules={[{ required: true, message: 'Select a category' }]}
            style={{ flex: 1, marginBottom: 0 }}
          >
            <Select placeholder="Category" style={{ width: '100%' }}>
              {Object.entries(ACTIVITY_CATEGORY_META).map(([k, m]) => (
                <Select.Option key={k} value={k}>{m.label}</Select.Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="location"
            label="Location"
            rules={[{ required: true, message: 'Enter location' }]}
            style={{ flex: 1, marginBottom: 0 }}
          >
            <Input placeholder="e.g. Conference Room B" maxLength={200} />
          </Form.Item>
        </Space>

        <Form.Item name="notes" label="Notes" style={{ marginTop: 16 }}>
          <TextArea rows={2} maxLength={2000} showCount placeholder="Any additional notes (optional)" />
        </Form.Item>
      </Form>
    </Modal>
  );
}
