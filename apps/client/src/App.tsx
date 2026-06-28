import { useEffect, useState } from 'react';
import { api, getServerUrl, testServerUrl, type AuthUser, type LicenseStatusResponse, type ModuleItem } from './api';
import { ShellLayout } from './components/ShellLayout';
import { LoadingShell } from './components/LoadingShell';
import { useI18n } from './i18n/I18nProvider';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { SitesPage } from './pages/modules/SitesPage';
import { TenantSettingsPage } from './pages/modules/TenantSettingsPage';
import { RolesPage } from './pages/modules/RolesPage';
import { AuditLogPage } from './pages/modules/AuditLogPage';
import { UsersPage } from './pages/modules/UsersPage';
import { SettingsPage } from './pages/SettingsPage';
import { screenForModule, type AppScreen } from './navigation/moduleRoutes';
import { SiteProvider } from './context/SiteContext';

export function App() {
  const { t } = useI18n();
  const [configured, setConfigured] = useState<boolean | null>(null);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [modules, setModules] = useState<ModuleItem[]>([]);
  const [license, setLicense] = useState<LicenseStatusResponse | null>(null);
  const [screen, setScreen] = useState<AppScreen>('dashboard');
  const [loading, setLoading] = useState(Boolean(api.getToken()));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const url = await getServerUrl();
        setConfigured(await testServerUrl(url));
      } catch {
        setConfigured(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!configured || !api.getToken()) return;
    void (async () => {
      try {
        setLoading(true);
        const meResponse = await api.getMe();
        const [modulesResponse, licenseResponse] = await Promise.all([
          api.getModules(),
          api.getLicenseStatus(),
        ]);
        setModules(modulesResponse.modules);
        setLicense(licenseResponse);
        setUser({
          id: meResponse.user.id,
          email: meResponse.user.email,
          fullName: meResponse.user.fullName,
          roleCode: meResponse.user.roleCode,
          permissions: meResponse.user.permissions,
          siteIds: meResponse.user.siteIds,
          tenant: licenseResponse.tenant,
        });
      } catch (err) {
        api.setToken(null);
        setError(err instanceof Error ? err.message : t('sessionExpired'));
      } finally {
        setLoading(false);
      }
    })();
  }, [configured, t]);

  async function handleLogin(email: string, password: string) {
    setError(null);
    const response = await api.login(email, password);
    api.setToken(response.token);
    setUser(response.user);
    setScreen('dashboard');
    const [modulesResponse, licenseResponse] = await Promise.all([
      api.getModules(),
      api.getLicenseStatus(),
    ]);
    setModules(modulesResponse.modules);
    setLicense(licenseResponse);
  }

  function handleLogout() {
    api.setToken(null);
    setUser(null);
    setModules([]);
    setLicense(null);
    setScreen('dashboard');
  }

  function handleModuleOpen(code: string) {
    const target = screenForModule(code);
    if (target) setScreen(target);
  }

  void handleModuleOpen; // used by future module cards

  function renderScreen() {
    switch (screen) {
      case 'dashboard':     return <DashboardPage user={user!} modules={modules} license={license!} onNavigate={(s) => setScreen(s as AppScreen)} />;
      case 'admin_users':   return <UsersPage user={user!} />;
      case 'admin_roles':   return <RolesPage user={user!} />;
      case 'admin_audit':   return <AuditLogPage user={user!} />;
      case 'core_sites':    return <SitesPage user={user!} />;
      case 'core_settings': return <TenantSettingsPage user={user!} />;
      default:              return null;
    }
  }

  if (configured === null) {
    return (
      <div className="ux-loading-screen">
        <div className="ux-loading-spinner" />
        <p className="ux-loading-text">{t('init')}</p>
      </div>
    );
  }

  if (!configured) return <SettingsPage onConfigured={() => setConfigured(true)} />;
  if (loading) return <LoadingShell />;
  if (!user || !license) return <LoginPage onLogin={handleLogin} error={error} />;

  return (
    <SiteProvider userRole={user.roleCode}>
      <ShellLayout
        user={user}
        modules={modules}
        license={license}
        screen={screen}
        onNavigate={setScreen}
        onLogout={handleLogout}
      >
        {renderScreen()}
      </ShellLayout>
    </SiteProvider>
  );
}
