import { useEffect, useMemo, useState } from 'react';
import { RAQMI_LICENSE_PACKS } from '@raqmi/licensing/packs';
import { RAQMI_MODULES } from '@raqmi/shared';
import type { EditorData, EditorLicense, EditorTenant } from './editor-api';
import { exportSignedLicense, loadEditorData, saveEditorData } from './editor-api';

function uid(prefix: string) {
  return `${prefix}-${crypto.randomUUID()}`;
}

const emptyLicense = (tenantId: string): EditorLicense => ({
  id: uid('lic'),
  tenantId,
  kind: 'professional',
  mode: 'offline',
  status: 'active',
  startsAt: new Date().toISOString().slice(0, 10),
  expiresAt: new Date(Date.now() + 365 * 86400000).toISOString().slice(0, 10),
  allowedModules: RAQMI_LICENSE_PACKS.find((p) => p.kind === 'professional')!.modules,
  limits: RAQMI_LICENSE_PACKS.find((p) => p.kind === 'professional')!.defaultLimits,
});

export function App() {
  const [data, setData] = useState<EditorData>({ tenants: [], licenses: [] });
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null);
  const [selectedLicenseId, setSelectedLicenseId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      const loaded = await loadEditorData();
      if (loaded.tenants.length === 0) {
        const demoTenant: EditorTenant = {
          id: uid('tenant'),
          code: 'demo-hotel',
          name: 'Hotel Demo Raqmi',
        };
        const demoLicense = emptyLicense(demoTenant.id);
        loaded.tenants = [demoTenant];
        loaded.licenses = [demoLicense];
        await saveEditorData(loaded);
      }
      setData(loaded);
      setSelectedTenantId(loaded.tenants[0]?.id ?? null);
      setSelectedLicenseId(loaded.licenses[0]?.id ?? null);
      setLoading(false);
    })();
  }, []);

  const selectedTenant = data.tenants.find((t) => t.id === selectedTenantId) ?? null;
  const selectedLicense = data.licenses.find((l) => l.id === selectedLicenseId) ?? null;
  const tenantLicenses = useMemo(
    () => data.licenses.filter((l) => l.tenantId === selectedTenantId),
    [data.licenses, selectedTenantId],
  );

  async function persist(next: EditorData) {
    setData(next);
    await saveEditorData(next);
  }

  function addTenant() {
    const tenant: EditorTenant = { id: uid('tenant'), code: 'client', name: 'Nouveau client' };
    const license = emptyLicense(tenant.id);
    const next = {
      tenants: [...data.tenants, tenant],
      licenses: [...data.licenses, license],
    };
    void persist(next);
    setSelectedTenantId(tenant.id);
    setSelectedLicenseId(license.id);
  }

  function updateLicense(patch: Partial<EditorLicense>) {
    if (!selectedLicense) return;
    const next = {
      ...data,
      licenses: data.licenses.map((l) => (l.id === selectedLicense.id ? { ...l, ...patch } : l)),
    };
    void persist(next);
  }

  function applyPack(kind: EditorLicense['kind']) {
    const pack = RAQMI_LICENSE_PACKS.find((p) => p.kind === kind);
    if (!pack || !selectedLicense) return;
    updateLicense({
      kind,
      allowedModules: pack.modules,
      limits: pack.defaultLimits,
    });
  }

  async function handleExport() {
    if (!selectedLicense || !selectedTenant) return;
    try {
      const path = await exportSignedLicense(selectedLicense, selectedTenant.name);
      setMessage(path ? `Licence exportée : ${path}` : 'Export annulé');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Export impossible');
    }
  }

  if (loading) return <main className="shell"><div className="panel">Chargement…</div></main>;

  return (
    <main className="shell">
      <header className="hero">
        <div>
          <p className="eyebrow">Raqmi System</p>
          <h1>License Manager</h1>
          <p className="subtitle">Créer, prolonger, suspendre et exporter les licences clients.</p>
        </div>
        <button type="button" onClick={addTenant}>Nouveau client</button>
      </header>

      {message && <div className="panel">{message}</div>}

      <section className="layout">
        <aside className="panel">
          <h2>Clients</h2>
          {data.tenants.map((tenant) => (
            <button
              key={tenant.id}
              type="button"
              className={tenant.id === selectedTenantId ? 'active' : ''}
              onClick={() => {
                setSelectedTenantId(tenant.id);
                const first = data.licenses.find((l) => l.tenantId === tenant.id);
                setSelectedLicenseId(first?.id ?? null);
              }}
            >
              {tenant.name}
            </button>
          ))}
        </aside>

        <section className="panel">
          {selectedTenant && selectedLicense ? (
            <>
              <h2>Licence — {selectedTenant.name}</h2>
              <div className="form-grid">
                <label>
                  Pack
                  <select
                    value={selectedLicense.kind}
                    onChange={(e) => applyPack(e.target.value as EditorLicense['kind'])}
                  >
                    {RAQMI_LICENSE_PACKS.map((pack) => (
                      <option key={pack.kind} value={pack.kind}>{pack.label}</option>
                    ))}
                    <option value="custom">Custom</option>
                  </select>
                </label>
                <label>
                  Statut
                  <select
                    value={selectedLicense.status}
                    onChange={(e) => updateLicense({ status: e.target.value as EditorLicense['status'] })}
                  >
                    <option value="active">active</option>
                    <option value="suspended">suspended</option>
                    <option value="expired">expired</option>
                    <option value="revoked">revoked</option>
                  </select>
                </label>
                <label>
                  Début
                  <input
                    type="date"
                    value={selectedLicense.startsAt.slice(0, 10)}
                    onChange={(e) => updateLicense({ startsAt: `${e.target.value}T00:00:00Z` })}
                  />
                </label>
                <label>
                  Expiration
                  <input
                    type="date"
                    value={selectedLicense.expiresAt.slice(0, 10)}
                    onChange={(e) => updateLicense({ expiresAt: `${e.target.value}T23:59:59Z` })}
                  />
                </label>
                <label>
                  Empreinte serveur (optionnel)
                  <input
                    type="text"
                    value={selectedLicense.serverFingerprint ?? ''}
                    onChange={(e) => updateLicense({ serverFingerprint: e.target.value || undefined })}
                  />
                </label>
              </div>

              <div className="actions">
                <button type="button" onClick={() => updateLicense({ status: 'active' })}>Activer</button>
                <button type="button" onClick={() => updateLicense({ status: 'suspended' })}>Suspendre</button>
                <button
                  type="button"
                  onClick={() =>
                    updateLicense({
                      expiresAt: new Date(Date.now() + 365 * 86400000).toISOString(),
                    })
                  }
                >
                  Prolonger 1 an
                </button>
                <button type="button" onClick={handleExport}>Exporter .license</button>
              </div>

              <h3>Modules ({selectedLicense.allowedModules.length})</h3>
              <div className="modules-grid">
                {RAQMI_MODULES.filter((m) => m.commercial).map((module) => {
                  const enabled = selectedLicense.allowedModules.includes(module.code);
                  return (
                    <label key={module.code} className="module">
                      <input
                        type="checkbox"
                        checked={enabled}
                        onChange={(e) => {
                          const allowed = new Set(selectedLicense.allowedModules);
                          if (e.target.checked) allowed.add(module.code);
                          else allowed.delete(module.code);
                          updateLicense({ allowedModules: [...allowed] });
                        }}
                      />
                      {module.label}
                    </label>
                  );
                })}
              </div>
            </>
          ) : (
            <p>Sélectionnez un client.</p>
          )}
        </section>
      </section>
    </main>
  );
}
