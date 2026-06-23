import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Form,
  Input,
  Typography,
  Alert,
  Space,
  Tag,
  Divider,
  Collapse,
} from 'antd';
import { LockOutlined, MailOutlined, WindowsOutlined } from '@ant-design/icons';
import { authApi } from '../../api/auth.api';
import { useAuthStore } from '../../store/authStore';
import { msalInstance, loginRequest } from '../../config/msalConfig';
import type { LoginRequest } from '../../types';

const { Title, Text, Paragraph } = Typography;

// ── Quick-login demo credentials (dev mode only) ──────────────────────────
const DEMO_USERS = [
  { label: 'Manager',    email: 'manager@demo.local',    password: 'DemoManager2026!' },
  { label: 'Supervisor', email: 'supervisor@demo.local', password: 'DemoSuper2026!'   },
  { label: 'Technician', email: 'technician@demo.local', password: 'DemoTech2026!'    },
  { label: 'Driver',     email: 'driver@demo.local',     password: 'DemoDriver2026!'  },
];

// Production = VITE_ENTRA_CLIENT_ID is injected at build time
const isSsoEnabled = !!import.meta.env.VITE_ENTRA_CLIENT_ID;
const ENV_LABEL    = import.meta.env.VITE_ENV_LABEL as string | undefined;

// ── Component ──────────────────────────────────────────────────────────────
export default function LoginPage() {
  const navigate  = useNavigate();
  const setAuth   = useAuthStore((s) => s.setAuth);
  const [form]    = Form.useForm<LoginRequest>();
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);

  // ── Microsoft SSO ────────────────────────────────────────────────────────
  const handleMicrosoftLogin = async () => {
    setLoading(true);
    setError(null);
    try {
      await msalInstance.initialize();
      const result = await msalInstance.loginPopup(loginRequest);

      const resp = await authApi.entraLogin(result.idToken);
      setAuth(resp.token, resp.user);
      navigate('/dashboard', { replace: true });
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { message?: string } }; message?: string };
      // Ignore user-cancelled popup (BrowserAuthError: user_cancelled)
      if (axiosErr.message?.includes('user_cancelled') || axiosErr.message?.includes('popup_window_error')) {
        return;
      }
      if (axiosErr.response?.data?.message) {
        setError(axiosErr.response.data.message);
      } else if (!axiosErr.response) {
        setError('Cannot reach the API. The server may be starting up — please wait 30 seconds and try again.');
      } else {
        setError('Microsoft sign-in failed. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  };

  // ── Email / password (local dev) ─────────────────────────────────────────
  const handleSubmit = async (values: LoginRequest) => {
    setLoading(true);
    setError(null);
    try {
      const resp = await authApi.login(values);
      setAuth(resp.token, resp.user);
      navigate('/dashboard', { replace: true });
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { message?: string } }; code?: string };
      let msg: string;
      if (!axiosErr.response) {
        msg = 'Cannot reach the API. The server may be starting up — please wait 30 seconds and try again.';
      } else if (axiosErr.response.data?.message) {
        msg = axiosErr.response.data.message;
      } else {
        msg = `Login failed (HTTP ${(err as { response?: { status?: number } }).response?.status ?? 'unknown'}).`;
      }
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const quickLogin = (email: string, password: string) => {
    form.setFieldsValue({ email, password });
    form.submit();
  };

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'linear-gradient(135deg, #0a1628 0%, #0f2444 50%, #1a3a5c 100%)',
        padding: '24px',
      }}
    >
      <div style={{ width: '100%', maxWidth: 440 }}>

        {/* ── Brand header ─────────────────────────────────────────────── */}
        <div style={{ textAlign: 'center', marginBottom: 28 }}>
          <div style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', marginBottom: 16 }}>
            <img
              src="/logo.png"
              alt="Desicon Group"
              style={{ width: 80, height: 80, borderRadius: 16, objectFit: 'contain', mixBlendMode: 'screen' }}
              onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none'; }}
            />
          </div>

          <Title level={3} style={{ margin: 0, color: '#ffffff', letterSpacing: 0.5 }}>
            GenService Platform
          </Title>
          <Text style={{ color: 'rgba(255,255,255,0.65)', fontSize: 13 }}>
            Desicon Group · General Service Management
          </Text>

          {ENV_LABEL && (
            <div style={{ marginTop: 10 }}>
              <Tag
                color={ENV_LABEL === 'demo' ? 'gold' : 'volcano'}
                style={{ textTransform: 'uppercase', fontWeight: 600 }}
              >
                {ENV_LABEL} environment
              </Tag>
            </div>
          )}
        </div>

        {/* ── Login card ───────────────────────────────────────────────── */}
        <Card
          style={{ borderRadius: 16, boxShadow: '0 20px 60px rgba(0,0,0,0.4)' }}
          styles={{ body: { padding: '32px 36px 28px' } }}
        >
          {error && (
            <Alert
              message={error}
              type="error"
              showIcon
              closable
              onClose={() => setError(null)}
              style={{ marginBottom: 20 }}
            />
          )}

          {/* ── Primary: Microsoft SSO ────────────────────────────────── */}
          {isSsoEnabled && (
            <>
              <Button
                block
                size="large"
                loading={loading}
                onClick={handleMicrosoftLogin}
                style={{
                  height: 48,
                  fontWeight: 600,
                  fontSize: 15,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 10,
                  background: '#0078d4',
                  borderColor: '#0078d4',
                  color: '#fff',
                  borderRadius: 8,
                }}
              >
                <WindowsOutlined style={{ fontSize: 18 }} />
                Sign in with Microsoft
              </Button>

              <div style={{ marginTop: 12, textAlign: 'center' }}>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  Use your Desicon Group Microsoft account
                </Text>
              </div>
            </>
          )}

          {/* ── Dev / fallback: email + password ─────────────────────── */}
          {!isSsoEnabled && (
            <Form
              form={form}
              layout="vertical"
              onFinish={handleSubmit}
              requiredMark={false}
              size="large"
            >
              <Form.Item
                name="email"
                label="Email Address"
                rules={[
                  { required: true, message: 'Email is required' },
                  { type: 'email', message: 'Enter a valid email' },
                ]}
              >
                <Input
                  prefix={<MailOutlined style={{ color: '#bfbfbf' }} />}
                  placeholder="your.email@company.com"
                  autoComplete="email"
                  autoFocus
                />
              </Form.Item>

              <Form.Item
                name="password"
                label="Password"
                rules={[{ required: true, message: 'Password is required' }]}
              >
                <Input.Password
                  prefix={<LockOutlined style={{ color: '#bfbfbf' }} />}
                  placeholder="••••••••"
                  autoComplete="current-password"
                />
              </Form.Item>

              <Form.Item style={{ marginBottom: 0 }}>
                <Button
                  type="primary"
                  htmlType="submit"
                  loading={loading}
                  block
                  style={{ height: 44, fontWeight: 600 }}
                >
                  Sign In
                </Button>
              </Form.Item>
            </Form>
          )}

          {/* ── SSO mode: developer login collapsed ──────────────────── */}
          {isSsoEnabled && (
            <Collapse
              ghost
              style={{ marginTop: 20 }}
              items={[{
                key: 'dev',
                label: <Text type="secondary" style={{ fontSize: 12 }}>Developer login</Text>,
                children: (
                  <Form
                    form={form}
                    layout="vertical"
                    onFinish={handleSubmit}
                    requiredMark={false}
                  >
                    <Form.Item name="email" rules={[{ required: true }, { type: 'email' }]}>
                      <Input prefix={<MailOutlined />} placeholder="Email" size="middle" />
                    </Form.Item>
                    <Form.Item name="password" rules={[{ required: true }]}>
                      <Input.Password prefix={<LockOutlined />} placeholder="Password" size="middle" />
                    </Form.Item>
                    <Button type="default" htmlType="submit" loading={loading} block size="middle">
                      Sign In with Password
                    </Button>
                  </Form>
                ),
              }]}
            />
          )}

          {/* ── Quick login (dev / demo only) ─────────────────────────── */}
          {!isSsoEnabled && (
            <>
              <Divider style={{ marginBlock: 24 }}>
                <Text type="secondary" style={{ fontSize: 12 }}>Quick login</Text>
              </Divider>

              <Paragraph style={{ textAlign: 'center', marginBottom: 12 }}>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  Demo credentials — click to fill &amp; sign in
                </Text>
              </Paragraph>

              <Space wrap size={8} style={{ justifyContent: 'center', width: '100%' }}>
                {DEMO_USERS.map((u) => (
                  <Button
                    key={u.email}
                    size="small"
                    onClick={() => quickLogin(u.email, u.password)}
                    disabled={loading}
                  >
                    {u.label}
                  </Button>
                ))}
              </Space>
            </>
          )}
        </Card>

        {/* ── Footer ───────────────────────────────────────────────────── */}
        <div style={{ textAlign: 'center', marginTop: 20 }}>
          <Text style={{ color: 'rgba(255,255,255,0.35)', fontSize: 12 }}>
            © 2026 Desicon Group · All rights reserved
          </Text>
        </div>

      </div>
    </div>
  );
}
