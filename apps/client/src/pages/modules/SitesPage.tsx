import { FormEvent, useEffect, useState } from 'react';
import { api, type SiteDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { IconPlus } from '../../components/icons';

const SITE_TYPES = [
  { value: 'hotel', label: 'Hôtel' },
  { value: 'annexe', label: 'Annexe' },
  { value: 'agency', label: 'Agence' },
  { value: 'branch', label: 'Succursale' },
  { value: 'site', label: 'Site' },
];

export function SitesPage() {
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

  useEffect(() => {
    void load();
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      setError(null);
      await api.createSite({ code, name, type, city: city || undefined });
      setCode('');
      setName('');
      setCity('');
      await load();
      await reloadContext();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function saveEdit() {
    if (!editing) return;
    try {
      await api.updateSite(editing.id, {
        name: editing.name,
        type: editing.type,
        city: editing.city,
        active: editing.active,
      });
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
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert" role="alert"><p>{error}</p></div>}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Nouveau site</h3>
        <form className="ux-form" onSubmit={onCreate}>
          <div className="ux-form-grid">
            <label className="ux-field">
              <span className="ux-field-label">Code</span>
              <input className="ux-field-input" value={code} onChange={(e) => setCode(e.target.value)} required />
            </label>
            <label className="ux-field">
              <span className="ux-field-label">Nom</span>
              <input className="ux-field-input" value={name} onChange={(e) => setName(e.target.value)} required />
            </label>
            <label className="ux-field">
              <span className="ux-field-label">Type</span>
              <select className="ux-field-input" value={type} onChange={(e) => setType(e.target.value)}>
                {SITE_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </label>
            <label className="ux-field">
              <span className="ux-field-label">Ville</span>
              <input className="ux-field-input" value={city} onChange={(e) => setCity(e.target.value)} />
            </label>
          </div>
          <button type="submit" className="db-primary-btn ux-btn-primary"><IconPlus size={16} /> Ajouter le site</button>
        </form>
      </section>

      {editing && (
        <section className="ux-card site-edit-panel ux-slide-in">
          <h3 className="ux-card-title">Modifier — {editing.code}</h3>
          <div className="ux-form-grid">
            <label className="ux-field">
              <span className="ux-field-label">Nom</span>
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
          <div className="site-edit-actions">
            <button type="button" className="db-primary-btn" onClick={() => void saveEdit()}>Enregistrer</button>
            <button type="button" className="db-ghost-btn" onClick={() => setEditing(null)}>Annuler</button>
          </div>
        </section>
      )}

      <section className="ux-card">
        <h3 className="ux-card-title">Sites ({sites.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
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
                  <td>{s.name}</td>
                  <td>{s.type ?? 'site'}</td>
                  <td>{s.city ?? '—'}</td>
                  <td>
                    <span className={`ux-status ${s.active ? 'ux-status--on' : 'ux-status--off'}`}>
                      {s.active ? 'Actif' : 'Inactif'}
                    </span>
                  </td>
                  <td className="module-table-actions">
                    <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => setEditing(s)}>Modifier</button>
                    <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => void toggleActive(s)}>
                      {s.active ? 'Désactiver' : 'Activer'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
