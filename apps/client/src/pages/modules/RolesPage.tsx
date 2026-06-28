import { FormEvent, useEffect, useState } from 'react';
import { api, type AuthUser, type RoleDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { isAdminRole } from '../../lib/permissions';

interface RolesPageProps {
  user: AuthUser;
}

export function RolesPage({ user }: RolesPageProps) {
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [permissionOptions, setPermissionOptions] = useState<{ key: string; label: string }[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [label, setLabel] = useState('');
  const [permissions, setPermissions] = useState<string[]>([]);
  const [editingRole, setEditingRole] = useState<RoleDto | null>(null);
  const [editLabel, setEditLabel] = useState('');
  const [editPermissions, setEditPermissions] = useState<string[]>([]);

  const canManage = isAdminRole(user.roleCode);

  async function load() {
    try {
      setError(null);
      const [rolesRes, permsRes] = await Promise.all([api.getRoles(), api.getPermissions()]);
      setRoles(rolesRes.items);
      setPermissionOptions(permsRes.items.map((p) => ({ key: p.key, label: p.label })));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  function togglePermission(p: string, list: string[], setter: (v: string[]) => void) {
    setter(list.includes(p) ? list.filter((x) => x !== p) : [...list, p]);
  }

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!canManage) return;
    try {
      await api.createRole({ code, label, permissions });
      setCode('');
      setLabel('');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  function startEdit(role: RoleDto) {
    setEditingRole(role);
    setEditLabel(role.label);
    setEditPermissions(role.permissions.includes('*') ? permissionOptions.map((p) => p.key) : [...role.permissions]);
  }

  async function saveEdit() {
    if (!editingRole || !canManage) return;
    try {
      await api.updateRole(editingRole.code, {
        label: editLabel,
        permissions: editingRole.isSystem ? undefined : editPermissions,
      });
      setEditingRole(null);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function onDelete(role: RoleDto) {
    if (role.isSystem || !canManage) return;
    try {
      await api.deleteRole(role.code);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={4} />;

  if (!canManage) {
    return (
      <div className="module-panel">
        <div className="ux-empty-state"><p>Accès réservé aux administrateurs.</p></div>
      </div>
    );
  }

  return (
    <div className="module-panel">
      {error && <div className="ux-alert" role="alert"><p>{error}</p></div>}

      {/* Formulaire création */}
      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Nouveau rôle personnalisé</h3>
            <p className="ux-card-desc">Définissez un rôle avec un jeu de permissions spécifiques.</p>
          </div>
        </div>
        <form onSubmit={onCreate}>
          <div className="ux-form-section">
            <div className="ux-form-grid">
              <label className="ux-field">
                <span className="ux-field-label">Code</span>
                <input className="ux-field-input" value={code} onChange={(e) => setCode(e.target.value)} placeholder="ex: comptable" required />
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Libellé affiché</span>
                <input className="ux-field-input" value={label} onChange={(e) => setLabel(e.target.value)} placeholder="ex: Comptable" required />
              </label>
            </div>
            <fieldset className="ux-site-picker" style={{ marginTop: 4 }}>
              <legend style={{ fontSize: 12, fontWeight: 700, color: 'var(--text-3)', letterSpacing: '.06em', textTransform: 'uppercase', marginBottom: 10, display: 'block' }}>Permissions</legend>
              <div className="ux-site-grid">
                {permissionOptions.map((p) => (
                  <label key={p.key} className="ux-checkbox ux-site-chip">
                    <input type="checkbox" checked={permissions.includes(p.key)} onChange={() => togglePermission(p.key, permissions, setPermissions)} />
                    <span>{p.label}</span>
                  </label>
                ))}
              </div>
            </fieldset>
          </div>
          <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
            <button type="submit" className="ux-btn-primary">Créer le rôle</button>
          </div>
        </form>
      </section>

      {/* Panneau édition */}
      {editingRole && (
        <section className="ux-card ux-slide-in">
          <div className="ux-card-head">
            <div>
              <h3 className="ux-card-title">Modifier — {editingRole.code}</h3>
              <p className="ux-card-desc">{editingRole.isSystem ? 'Rôle système — permissions non modifiables.' : 'Rôle personnalisé'}</p>
            </div>
            <button type="button" className="db-ghost-btn" onClick={() => setEditingRole(null)}>Annuler</button>
          </div>
          <div className="ux-form-section">
            <label className="ux-field" style={{ maxWidth: 340 }}>
              <span className="ux-field-label">Libellé affiché</span>
              <input className="ux-field-input" value={editLabel} onChange={(e) => setEditLabel(e.target.value)} />
            </label>
            {!editingRole.isSystem && (
              <fieldset className="ux-site-picker">
                <legend style={{ fontSize: 12, fontWeight: 700, color: 'var(--text-3)', letterSpacing: '.06em', textTransform: 'uppercase', marginBottom: 10, display: 'block' }}>Permissions</legend>
                <div className="ux-site-grid">
                  {permissionOptions.map((p) => (
                    <label key={p.key} className="ux-checkbox ux-site-chip">
                      <input type="checkbox" checked={editPermissions.includes(p.key)} onChange={() => togglePermission(p.key, editPermissions, setEditPermissions)} />
                      <span>{p.label}</span>
                    </label>
                  ))}
                </div>
              </fieldset>
            )}
          </div>
          <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)', display: 'flex', gap: 10 }}>
            <button type="button" className="ux-btn-primary" onClick={() => void saveEdit()}>Enregistrer</button>
            <button type="button" className="db-ghost-btn" onClick={() => setEditingRole(null)}>Annuler</button>
          </div>
        </section>
      )}

      {/* Tableau */}
      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Rôles ({roles.length})</h3>
            <p className="ux-card-desc">Rôles système et rôles personnalisés</p>
          </div>
        </div>
        <div className="ux-table-wrap">
          <table className="ux-table">
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
                  <td style={{ fontWeight: 500, color: 'var(--text)' }}>{r.label}</td>
                  <td>
                    <span className={`ux-badge ${r.isSystem ? 'ux-badge--system' : 'ux-badge--custom'}`}>
                      {r.isSystem ? 'Système' : 'Personnalisé'}
                    </span>
                  </td>
                  <td>
                    {r.permissions.includes('*') ? (
                      <span className="ux-badge ux-badge--system">Toutes</span>
                    ) : r.permissions.length === 0 ? (
                      <span style={{ color: 'var(--text-3)', fontSize: 12 }}>Aucune</span>
                    ) : (
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                        {r.permissions.map((p) => {
                          const [mod, action] = p.split(':');
                          return (
                            <span key={p} className={`ux-badge ${action === 'write' ? 'ux-badge--admin' : 'ux-badge--user'}`} title={p}>
                              {mod}:{action}
                            </span>
                          );
                        })}
                      </div>
                    )}
                  </td>
                  <td>
                    <div className="ux-table-actions">
                      <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => startEdit(r)}>Modifier</button>
                      {!r.isSystem && (
                        <button type="button" className="db-ghost-btn ux-btn-sm" style={{ color: 'var(--red-text)' }} onClick={() => void onDelete(r)}>Supprimer</button>
                      )}
                    </div>
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
