import { FormEvent, useEffect, useState } from 'react';
import { api, type AuthUser, type SiteDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { IconPlus } from '../../components/icons';
import { canWriteSites } from '../../lib/permissions';

const SITE_TYPES = [
  { value: 'hotel',    label: 'Hôtel' },
  { value: 'annexe',   label: 'Annexe' },
  { value: 'agency',   label: 'Agence' },
  { value: 'branch',   label: 'Succursale' },
  { value: 'site',     label: 'Site' },
];

export function SitesPage({ user }: { user: AuthUser }) {
  const canWrite = canWriteSites(user.permissions, user.roleCode);
  const { reload: reloadContext } = useSiteContext();
  const [sites, setSites] = useState<SiteDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [type, setType] = useState('hotel');
  const [city, setCity] = useState('');
  const [editing, setEditing] = useState<SiteDto | null>(null);

  async function load() {
    try {
      setError(null);
      const res = await api.getSites();
      setSites(res.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      setError(null);
      await api.createSite({ code, name, type, city: city || undefined });
      setCode(''); setName(''); setCity('');
      await load();
      await reloadContext();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function saveEdit() {
    if (!editing) return;
    try {
      await api.updateSite(editing.id, { name: editing.name, type: editing.type, city: editing.city, active: editing.active });
      setEditing(null);
      await load();
      await reloadContext();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function toggleActive(site: SiteDto) {
    try {
      await api.updateSite(site.id, { active: !site.active });
      await load();
      await reloadContext();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={4} />;

  return (
    <div className="module-panel">
      {error && <div className="ux-alert" role="alert"><p>{error}</p></div>}

      {/* Formulaire création */}
      {canWrite && (
        <section className="ux-card">
          <div className="ux-card-head">
            <div>
              <h3 className="ux-card-title">Nouveau site</h3>
              <p className="ux-card-desc">Ajoutez un hôtel, une annexe, une agence ou toute unité organisationnelle.</p>
            </div>
          </div>
          <form onSubmit={onCreate}>
            <div className="ux-form-section">
              <div className="ux-form-grid">
                <label className="ux-field">
                  <span className="ux-field-label">Code</span>
                  <input className="ux-field-input" value={code} onChange={(e) => setCode(e.target.value)} placeholder="ex: HTL-ALGER" required />
                </label>
                <label className="ux-field">
                  <span className="ux-field-label">Nom du site</span>
                  <input className="ux-field-input" value={name} onChange={(e) => setName(e.target.value)} placeholder="ex: Hôtel Demo Alger" required />
                </label>
                <label className="ux-field">
                  <span className="ux-field-label">Type</span>
                  <select className="ux-field-input" value={type} onChange={(e) => setType(e.target.value)}>
                    {SITE_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                  </select>
                </label>
                <label className="ux-field">
                  <span className="ux-field-label">Ville</span>
                  <input className="ux-field-input" value={city} onChange={(e) => setCity(e.target.value)} placeholder="ex: Alger" />
                </label>
              </div>
            </div>
            <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
              <button type="submit" className="ux-btn-primary">
                <IconPlus size={15} /> Ajouter le site
              </button>
            </div>
          </form>
        </section>
      )}

      {/* Panneau d'édition */}
      {editing && (
        <section className="ux-card ux-slide-in">
          <div className="ux-card-head">
            <div>
              <h3 className="ux-card-title">Modifier le site</h3>
              <p className="ux-card-desc">{editing.code}</p>
            </div>
            <button type="button" className="db-ghost-btn" onClick={() => setEditing(null)}>Annuler</button>
          </div>
          <div className="ux-form-section">
            <div className="ux-form-grid">
              <label className="ux-field">
                <span className="ux-field-label">Nom du site</span>
                <input className="ux-field-input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} />
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Type</span>
                <select className="ux-field-input" value={editing.type ?? 'site'} onChange={(e) => setEditing({ ...editing, type: e.target.value })}>
                  {SITE_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                </select>
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Ville</span>
                <input className="ux-field-input" value={editing.city ?? ''} onChange={(e) => setEditing({ ...editing, city: e.target.value })} />
              </label>
            </div>
          </div>
          <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)', display: 'flex', gap: 10 }}>
            <button type="button" className="ux-btn-primary" onClick={() => void saveEdit()}>Enregistrer</button>
            <button type="button" className="db-ghost-btn" onClick={() => setEditing(null)}>Annuler</button>
          </div>
        </section>
      )}

      {/* Tableau */}
      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Sites</h3>
            <p className="ux-card-desc">{sites.length} site{sites.length > 1 ? 's' : ''} · Unités organisationnelles</p>
          </div>
        </div>

        {sites.length === 0 ? (
          <div className="ux-empty-state"><p>Aucun site configuré.</p></div>
        ) : (
          <div className="ux-table-wrap">
            <table className="ux-table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Nom</th>
                  <th>Type</th>
                  <th>Ville</th>
                  <th>Statut</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {sites.map((s) => (
                  <tr key={s.id}>
                    <td><code>{s.code}</code></td>
                    <td style={{ fontWeight: 500, color: 'var(--text)' }}>{s.name}</td>
                    <td>{s.type ?? 'site'}</td>
                    <td>{s.city ?? '—'}</td>
                    <td>
                      <span className={`ux-status ${s.active ? 'ux-status--on' : 'ux-status--off'}`}>
                        <span className="pill-dot" />
                        {s.active ? 'Actif' : 'Inactif'}
                      </span>
                    </td>
                    <td>
                      {canWrite && (
                        <div className="ux-table-actions">
                          <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => setEditing(s)}>Modifier</button>
                          <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => void toggleActive(s)}>
                            {s.active ? 'Désactiver' : 'Activer'}
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
