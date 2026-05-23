import { useState } from 'react';
import {
  Modal, Form, Select, InputNumber, Input, Alert, Space, Typography,
} from 'antd';
import { fuelApi } from '../../../api/fuel.api';
import { GENERATOR_RUN_REASON_META } from '../../../types';

const { TextArea } = Input;
const { Text }     = Typography;

const GENERATOR_LOCATIONS = [
  'HQ Generator Room',
  'Block A Generator',
  'Block B Generator',
  'Block C Generator',
  'Warehouse Generator',
];

interface Props {
  open:      boolean;
  mode:      'start' | 'stop';
  runningId?: string;   // used for stop
  onClose:   () => void;
  onDone:    () => void;
}

interface StartForm {
  location:       string;
  runReason:      string;
  outageCause?:   string;
  fuelLevelBefore?: number;
  notes?:         string;
}

interface StopForm {
  fuelLevelAfter?: number;
  notes?:          string;
}

export default function LogGeneratorModal({ open, mode, runningId, onClose, onDone }: Props) {
  const [startForm] = Form.useForm<StartForm>();
  const [stopForm]  = Form.useForm<StopForm>();
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [reason,  setReason]  = useState('');

  const handleClose = () => {
    startForm.resetFields();
    stopForm.resetFields();
    setError(null);
    setReason('');
    onClose();
  };

  const handleOk = async () => {
    try {
      setLoading(true); setError(null);
      if (mode === 'start') {
        const vals = await startForm.validateFields();
        await fuelApi.startGenerator({
          location:       vals.location,
          runReason:      vals.runReason,
          outageCause:    vals.outageCause,
          fuelLevelBefore: vals.fuelLevelBefore,
          notes:          vals.notes,
        });
      } else {
        const vals = await stopForm.validateFields();
        await fuelApi.stopGenerator(runningId!, {
          fuelLevelAfter: vals.fuelLevelAfter,
          notes:          vals.notes,
        });
      }
      onDone();
      handleClose();
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'response' in e) {
        const err = e as { response?: { data?: { message?: string } } };
        setError(err.response?.data?.message ?? 'Action failed.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={mode === 'start' ? 'Start Generator Session' : 'Stop Generator Session'}
      open={open}
      onOk={handleOk}
      onCancel={handleClose}
      okText={mode === 'start' ? 'Start Generator' : 'Stop Generator'}
      okButtonProps={{ danger: mode === 'stop' }}
      confirmLoading={loading}
      width={480}
      destroyOnClose
    >
      {error && (
        <Alert message={error} type="error" showIcon closable
          onClose={() => setError(null)} style={{ marginBottom: 16 }} />
      )}

      {mode === 'start' ? (
        <Form form={startForm} layout="vertical">
          <Form.Item name="location" label="Generator Location"
            rules={[{ required: true, message: 'Select a location' }]}>
            <Select placeholder="Select generator">
              {GENERATOR_LOCATIONS.map(l => (
                <Select.Option key={l} value={l}>{l}</Select.Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item name="runReason" label="Reason for Running"
            rules={[{ required: true, message: 'Select a reason' }]}>
            <Select placeholder="Select reason" onChange={v => setReason(v)}>
              {Object.entries(GENERATOR_RUN_REASON_META).map(([k, m]) => (
                <Select.Option key={k} value={k}>{m.label}</Select.Option>
              ))}
            </Select>
          </Form.Item>

          {(reason === 'PowerOutage' || reason === 'LoadShedding') && (
            <Form.Item name="outageCause" label="Outage Cause">
              <Input placeholder="e.g. EKEDC grid failure, transformer fault…" maxLength={200} />
            </Form.Item>
          )}

          <Space style={{ width: '100%' }} size={12}>
            <Form.Item name="fuelLevelBefore" label="Fuel Level Before (L)" style={{ flex: 1 }}>
              <InputNumber min={0} max={5000} style={{ width: '100%' }} placeholder="e.g. 200" />
            </Form.Item>
          </Space>

          <Form.Item name="notes" label="Notes">
            <TextArea rows={2} maxLength={2000} placeholder="Optional notes…" />
          </Form.Item>
        </Form>
      ) : (
        <Form form={stopForm} layout="vertical">
          <Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
            Recording the end of this generator session. Runtime will be calculated automatically.
          </Text>
          <Form.Item name="fuelLevelAfter" label="Fuel Level After (L)">
            <InputNumber min={0} max={5000} style={{ width: '100%' }} placeholder="e.g. 140" />
          </Form.Item>
          <Form.Item name="notes" label="Notes">
            <TextArea rows={2} maxLength={2000} placeholder="Optional notes…" />
          </Form.Item>
        </Form>
      )}
    </Modal>
  );
}
