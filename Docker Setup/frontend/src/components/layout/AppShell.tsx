import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Drawer, Grid, Layout } from 'antd';
import Sidebar from './Sidebar';
import TopBar from './TopBar';

const { Sider, Header, Content } = Layout;
const { useBreakpoint } = Grid;

const SIDER_WIDTH        = 240;
const SIDER_COLLAPSED_W  = 72;

function SidebarLogo({ collapsed }: { collapsed: boolean }) {
  return (
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
  );
}

export default function AppShell() {
  const [collapsed,      setCollapsed]      = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const screens  = useBreakpoint();
  const isMobile = !screens.md;

  return (
    <Layout style={{ minHeight: '100vh' }}>

      {!isMobile && (
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
          <SidebarLogo collapsed={collapsed} />
          <Sidebar collapsed={collapsed} />
        </Sider>
      )}

      {isMobile && (
        <Drawer
          open={mobileMenuOpen}
          onClose={() => setMobileMenuOpen(false)}
          placement="left"
          width={220}
          styles={{
            header: { display: 'none' },
            body: { padding: 0, background: '#001529' },
          }}
          style={{ padding: 0 }}
        >
          <SidebarLogo collapsed={false} />
          <Sidebar
            collapsed={false}
            onItemClick={() => setMobileMenuOpen(false)}
          />
        </Drawer>
      )}

      <Layout
        style={{
          marginInlineStart: isMobile ? 0 : (collapsed ? SIDER_COLLAPSED_W : SIDER_WIDTH),
          transition: 'margin-inline-start 0.2s',
        }}
      >
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
          <TopBar onMenuClick={isMobile ? () => setMobileMenuOpen(true) : undefined} />
        </Header>

        <Content
          style={{
            margin: isMobile ? '16px 12px' : '24px',
            minHeight: 'calc(100vh - 56px - 48px)',
          }}
        >
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
