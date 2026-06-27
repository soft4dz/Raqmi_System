import { useEffect, useState } from 'react';
import { api, type AuthUser, type LicenseStatusResponse, type ModuleItem } from './api';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';

export function App() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [modules, setModules] = useState<ModuleItem[]>([]);
  const [license, setLicense] = useState<LicenseStatusResponse | null>(null);
  const [loading, setLoading] = useState(Boolean(api.getToken()));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!api.getToken()) return;

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
        setError(err instanceof Error ? err.message : 'Session expirée');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function handleLogin(email: string, password: string) {
    setError(null);
    const response = await api.login(email, password);
    api.setToken(response.token);
    setUser(response.user);

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
  }

  if (loading) {
    return (
      <div className="shell center">
        <p className="muted">Chargement de Raqmi System…</p>
      </div>
    );
  }

  if (!user || !license) {
    return <LoginPage onLogin={handleLogin} error={error} />;
  }

  return (
    <DashboardPage
      user={user}
      modules={modules}
      license={license}
      onLogout={handleLogout}
    />
  );
}
