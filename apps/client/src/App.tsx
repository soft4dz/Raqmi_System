import { useEffect, useState } from 'react';
import { api, getServerUrl, testServerUrl, type AuthUser, type LicenseStatusResponse, type ModuleItem } from './api';
import { ShellLayout } from './components/ShellLayout';
import { LoadingShell } from './components/LoadingShell';
import { useI18n } from './i18n/I18nProvider';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { ModuleScreen } from './pages/modules/ModuleScreen';
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

  function renderScreen() {
    if (screen === 'dashboard') {
      return (
        <DashboardPage
          user={user!}
          modules={modules}
          license={license!}
          onModuleOpen={handleModuleOpen}
        />
      );
    }
    if (screen === 'admin_users') return <UsersPage />;
    if (screen === 'admin_roles') return <RolesPage />;
    if (screen === 'admin_audit') return <AuditLogPage />;
    if (screen === 'core_sites') return <SitesPage />;
    if (screen === 'core_settings') return <TenantSettingsPage />;
    return <ModuleScreen screen={screen} />;
  }

  if (configured === null) {
    return (
      <div className="shell center">
        <p className="muted">{t('init')}</p>
      </div>
    );
  }

  if (!configured) {
    return <SettingsPage onConfigured={() => setConfigured(true)} />;
  }

  if (loading) {
    return <LoadingShell />;
  }

  if (!user || !license) {
    return <LoginPage onLogin={handleLogin} error={error} />;
  }

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
