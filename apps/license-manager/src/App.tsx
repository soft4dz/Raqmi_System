import { useEffect, useMemo, useState } from 'react';

type LicenseRecord = {
  id: string;
  tenantName: string;
  kind: string;
  mode: string;
  status: string;
  startsAt: string;
  expiresAt: string;
  allowedModules: string[];
  limits: {
    maxUsers: number;
    maxSites: number;
    maxStorageGb: number;
    offlineGraceDays: number;
  };
};

type ModuleRecord = {
  code: string;
  label: string;
  family: string;
  commercial: boolean;
};

const API_URL = import.meta.env.VITE_RAQMI_SERVER_URL ?? 'http://localhost:4000';

export function App() {
  const [licenses, setLicenses] = useState<LicenseRecord[]>([]);
  const [modules, setModules] = useState<ModuleRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadData() {
      try {
        setLoading(true);
        const [licensesResponse, modulesResponse] = await Promise.all([
          fetch(`${API_URL}/api/licenses`),
          fetch(`${API_URL}/api/modules`),
        ]);

        if (!licensesResponse.ok || !modulesResponse.ok) {
          throw new Error('Serveur Raqmi indisponible.');
        }

        const licensesJson = await licensesResponse.json();
        const modulesJson = await modulesResponse.json();
        setLicenses(licensesJson.data ?? []);
        setModules(modulesJson.data ?? []);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur inconnue.');
      } finally {
        setLoading(false);
      }
    }

    void loadData();
  }, []);

  const stats = useMemo(() => {
    const active = licenses.filter((license) => license.status === 'active').length;
    const totalModules = modules.filter((module) => module.commercial).length;
    return { active, total: licenses.length, totalModules };
  }, [licenses, modules]);

  return (
    <main className="shell">
      <header className="hero">
        <div>
          <p className="eyebrow">Raqmi System</p>
          <h1>License Manager</h1>
          <p className="subtitle">
            Portail éditeur pour suivre les clients, licences, modules autorisés et limites commerciales.
          </p>
        </div>
        <div className="badge">API: {API_URL}</div>
      </header>

      <section className="cards">
        <article className="card">
          <span>Licences</span>
          <strong>{stats.total}</strong>
        </article>
        <article className="card">
          <span>Actives</span>
          <strong>{stats.active}</strong>
        </article>
        <article className="card">
          <span>Modules commerciaux</span>
          <strong>{stats.totalModules}</strong>
        </article>
      </section>

      {loading && <div className="panel">Chargement des données...</div>}
      {error && <div className="panel error">{error}</div>}

      {!loading && !error && (
        <section className="panel">
          <div className="panel-title">
            <h2>Licences clients</h2>
            <span>{licenses.length} licence(s)</span>
          </div>

          <div className="table">
            <div className="row head">
              <span>Client</span>
              <span>Pack</span>
              <span>Mode</span>
              <span>Statut</span>
              <span>Expiration</span>
              <span>Modules</span>
              <span>Limites</span>
            </div>
            {licenses.map((license) => (
              <div className="row" key={license.id}>
                <span>{license.tenantName}</span>
                <span>{license.kind}</span>
                <span>{license.mode}</span>
                <span className={`status ${license.status}`}>{license.status}</span>
                <span>{new Date(license.expiresAt).toLocaleDateString('fr-DZ')}</span>
                <span>{license.allowedModules.length}</span>
                <span>
                  {license.limits.maxUsers} users / {license.limits.maxSites} sites / {license.limits.maxStorageGb} GB
                </span>
              </div>
            ))}
          </div>
        </section>
      )}

      <section className="panel">
        <div className="panel-title">
          <h2>Catalogue modules</h2>
          <span>{modules.length} module(s)</span>
        </div>
        <div className="modules-grid">
          {modules.map((module) => (
            <article className="module" key={module.code}>
              <strong>{module.label}</strong>
              <span>{module.family}</span>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}
