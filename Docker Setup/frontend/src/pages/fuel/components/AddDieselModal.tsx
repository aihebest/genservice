import { useState } from 'react';
import {
  Modal, Form, Select, InputNumber, Input, Alert, DatePicker, Space,
} from 'antd';
import dayjs from 'dayjs';
import { fuelApi } from '../../../api/fuel.api';
import { DIESEL_RECORD_TYPE_META } from '../../../types';

const { TextArea } = Input;

const DESTINATIONS = [
  'HQ Generator Room',
  'Block A Generator',
  'Block B Generator',
  'Block C Generator',
  'Warehouse Generator',
  'Company Vehicle – LAG-342-TE',
  'Company Vehicle – LAG-098-BT',
  'Company Vehicle – LAG-211-GK',
  'HQ Diesel Tank',
];

interface Props {
  open:    boolean;
  onClose: () => void;
  onDone:  () => void;
}

interface FormValues {
  recordDate:    ReturnType<typeof dayjs>;
  recordType:    string;
  quantityLitres: number;
  unitCostNaira:  number;
  supplier?:     string;
  destination?:  string;
  notes?:        string;
}

export default function AddDieselModal({ open, onClose, onDone }: Props) {
  const [form]    = Form.useForm<FormValues>();
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [recType, setRecType] = useState('Purchase');

  const handleClose = () => {
    form.resetFields();
    setError(null);
    setRecType('Purchase');
    onClose();
  };

  const handleOk = async () => {
    try {
      const vals = await form.validateFields();
      setLoading(true); setError(null);

      await fuelApi.createDieselRecord({
        recordDate:    vals.recordDate.toISOString(),
        recordType:    vals.recordType,
        quantityLitres: vals.quantityLitres,
        unitCostNaira:  vals.recordType === 'Purchase' ? vals.unitCostNaira : 0,
        supplier:      vals.supplier,
        destination:   vals.destination,
        notes:         vals.notes,
      });

      onDone();
      handleClose();
    } catch (e: unknown) {
      if (e && typeof e === 'object' && 'response' in e) {
        const err = e as { response?: { data?: { message?: string } } };
        setError(err.response?.data?.message ?? 'Failed to save record.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title="Add Diesel Record"
      open={open}
      onOk={handleOk}
      onCancel={handleClose}
      okText="Save Record"
      confirmLoading={loading}
      width={500}
      destroyOnClose
    >
      {error && (
        <Alert message={error} type="error" showIcon closable
          onClose={() => setError(null)} style={{ marginBottom: 16 }} />
      )}

      <Form form={form} layout="vertical"
        initialValues={{ recordDate: dayjs(), recordType: 'Purchase' }}>

        <Space style={{ width: '100%' }} size={12}>
          <Form.Item name="recordDate" label="Date"
            rules={[{ required: true }]} style={{ flex: 1 }}>
            <DatePicker style={{ width: '100%' }} />
          </Form.Item>

          <Form.Item name="recordType" label="Type"
            rules={[{ required: true }]} style={{ flex: 1 }}>
            <Select onChange={v => setRecType(v)}>
              {Object.entries(DIESEL_RECORD_TYPE_META).map(([k, m]) => (
                <Select.Option key={k} value={k}>{m.label}</Select.Option>
              ))}
            </Select>
          </Form.Item>
        </Space>

        <Space style={{ width: '100%' }} size={12}>
          <Form.Item name="quantityLitres" label="Quantity (Litres)"
            rules={[{ required: true, message: 'Enter quantity' }]} style={{ flex: 1 }}>
            <InputNumber min={1} max={10000} style={{ width: '100%' }} placeholder="e.g. 200" />
          </Form.Item>

          {recType === 'Purchase' && (
            <Form.Item name="unitCostNaira" label="Unit Cost (₦/L)"
              rules={[{ required: true, message: 'Enter unit cost' }]} style={{ flex: 1 }}>
              <InputNumber min={0} style={{ width: '100%' }} placeholder="e.g. 1250" />
            </Form.Item>
          )}
        </Space>

        {recType === 'Purchase' && (
          <Form.Item name="supplier" label="Supplier">
            <Input placeholder="e.g. Total Energies – Ilupeju" maxLength={150} />
          </Form.Item>
        )}

        {(recType === 'Dispensed' || recType === 'Transfer') && (
          <Form.Item name="destination" label="Destination"
            rules={[{ required: true, message: 'Enter destination' }]}>
            <Select placeholder="Select destination" showSearch allowClear>
              {DESTINATIONS.map(d => (
                <Select.Option key={d} value={d}>{d}</Select.Option>
              ))}
            </Select>
          </Form.Item>
        )}

        <Form.Item name="notes" label="Notes">
          <TextArea rows={2} maxLength={2000} showCount placeholder="Optional notes…" />
        </Form.Item>
      </Form>
    </Modal>
  );
}
