import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Layout } from 'antd';
import Sidebar from './Sidebar';
import TopBar from './TopBar';

const { Sider, Header, Content } = Layout;

const SIDER_WIDTH        = 240;
const SIDER_COLLAPSED_W  = 72;

export default function AppShell() {
  const [collapsed, setCollapsed] = useState(false);

  return (
    <Layout style={{ minHeight: '100vh' }}>
      {/* ── Sidebar ─────────────────────────────────────────────────── */}
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        width={SIDER_WIDTH}
        collapsedWidth={SIDER_COLLAPSED_W}
        theme="dark"
        style={{
          overflow: 'auto',
          height: '100vh',
          position: 'fixed',
          insetInlineStart: 0,
          top: 0,
          bottom: 0,
          scrollbarWidth: 'thin',
          scrollbarColor: 'unset',
          zIndex: 100,
        }}
      >
        {/* Logo / brand */}
        <div
          style={{
            height: 56,
            display: 'flex',
            alignItems: 'center',
            justifyContent: collapsed ? 'center' : 'flex-start',
            padding: collapsed ? 0 : '0 16px',
            borderBottom: '1px solid rgba(255,255,255,0.08)',
            transition: 'padding 0.2s',
            overflow: 'hidden',
            whiteSpace: 'nowrap',
            gap: 10,
          }}
        >
          {/* Company logo — mixBlendMode:screen removes black background */}
          <img
            src="/logo.png"
            alt="Desicon"
            style={{
              width: 36,
              height: 36,
              borderRadius: 8,
              objectFit: 'contain',
              flexShrink: 0,
              mixBlendMode: 'screen',
            }}
            onError={(e) => {
              (e.currentTarget as HTMLImageElement).style.display = 'none';
            }}
          />
          {!collapsed && (
            <div style={{ lineHeight: 1.2 }}>
              <div style={{ color: '#fff', fontWeight: 700, fontSize: 14, letterSpacing: 0.3 }}>
                Desicon Group
              </div>
              <div style={{ color: 'rgba(255,255,255,0.45)', fontSize: 11 }}>
                GenService Platform
              </div>
            </div>
          )}
        </div>

        <Sidebar collapsed={collapsed} />
      </Sider>

      {/* ── Main layout (offset by sider width) ─────────────────────── */}
      <Layout
        style={{
          marginInlineStart: collapsed ? SIDER_COLLAPSED_W : SIDER_WIDTH,
          transition: 'margin-inline-start 0.2s',
        }}
      >
        {/* ── Top bar ───────────────────────────────────────────────── */}
        <Header
          style={{
            padding: '0 24px',
            background: '#fff',
            height: 56,
            lineHeight: '56px',
            display: 'flex',
            alignItems: 'center',
            borderBottom: '1px solid #f0f0f0',
            position: 'sticky',
            top: 0,
            zIndex: 99,
            boxShadow: '0 1px 4px rgba(0,21,41,0.08)',
          }}
        >
          <TopBar />
        </Header>

        {/* ── Page content ──────────────────────────────────────────── */}
        <Content
          style={{
            margin: '24px',
            minHeight: 'calc(100vh - 56px - 48px)',
          }}
        >
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
