import { FormEvent, useEffect, useState } from 'react';
import { api, type RoleDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';

const PERMISSION_OPTIONS = [
  'finance:read', 'finance:write', 'hr:read', 'hr:write',
  'stocks:read', 'stocks:write', 'ged:read', 'administration:read', 'sites:read',
];

export function RolesPage() {
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [label, setLabel] = useState('');
  const [permissions, setPermissions] = useState<string[]>(['finance:read']);

  async function load() {
    try {
      setError(null);
      const res = await api.getRoles();
      setRoles(res.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  function togglePermission(p: string) {
    setPermissions((prev) => (prev.includes(p) ? prev.filter((x) => x !== p) : [...prev, p]));
  }

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    try {
      await api.createRole({ code, label, permissions });
      setCode('');
      setLabel('');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function onDelete(role: RoleDto) {
    if (role.isSystem) return;
    try {
      await api.deleteRole(role.code);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={4} />;

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Nouveau rôle personnalisé</h3>
        <form className="ux-form" onSubmit={onCreate}>
          <div className="ux-form-grid">
            <label className="ux-field">
              <span className="ux-field-label">Code</span>
              <input className="ux-field-input" value={code} onChange={(e) => setCode(e.target.value)} required />
            </label>
            <label className="ux-field">
              <span className="ux-field-label">Libellé</span>
              <input className="ux-field-input" value={label} onChange={(e) => setLabel(e.target.value)} required />
            </label>
          </div>
          <fieldset className="site-picker ux-site-picker">
            <legend>Permissions</legend>
            <div className="ux-site-grid">
              {PERMISSION_OPTIONS.map((p) => (
                <label key={p} className="site-picker-item ux-checkbox ux-site-chip">
                  <input type="checkbox" checked={permissions.includes(p)} onChange={() => togglePermission(p)} />
                  <span>{p}</span>
                </label>
              ))}
            </div>
          </fieldset>
          <button type="submit" className="db-primary-btn ux-btn-primary">Créer le rôle</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Rôles ({roles.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Libellé</th>
                <th>Type</th>
                <th>Permissions</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {roles.map((r) => (
                <tr key={r.code}>
                  <td><code>{r.code}</code></td>
                  <td>{r.label}</td>
                  <td>{r.isSystem ? 'Système' : 'Personnalisé'}</td>
                  <td className="ux-sites-cell">{r.permissions.join(', ')}</td>
                  <td>
                    {!r.isSystem && (
                      <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => void onDelete(r)}>Supprimer</button>
                    )}
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
