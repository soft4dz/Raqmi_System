import { FormEvent, useEffect, useMemo, useState } from 'react';
import { api, type AuthUser, type SiteDto, type UserDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { IconPlus } from '../../components/icons';
import { canWriteAdmin } from '../../lib/permissions';

function siteNames(siteIds: string[], sites: SiteDto[]) {
  return siteIds
    .map((id) => sites.find((s) => s.id === id)?.name ?? id)
    .join(', ');
}

function userInitials(name: string) {
  return name
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
    .toUpperCase();
}

function roleBadgeClass(role: string) {
  if (role === 'admin') return 'ux-badge ux-badge--admin';
  if (role === 'manager') return 'ux-badge ux-badge--manager';
  return 'ux-badge ux-badge--user';
}

export function UsersPage({ user }: { user: AuthUser }) {
  const canWrite = canWriteAdmin(user.permissions, user.roleCode);
  const [users, setUsers] = useState<UserDto[]>([]);
  const [sites, setSites] = useState<SiteDto[]>([]);
  const [roles, setRoles] = useState<{ code: string; label: string }[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [email, setEmail] = useState('');
  const [fullName, setFullName] = useState('');
  const [roleCode, setRoleCode] = useState('user');
  const [selectedSiteIds, setSelectedSiteIds] = useState<string[]>([]);
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);
  const [editSiteIds, setEditSiteIds] = useState<string[]>([]);
  const [profileUser, setProfileUser] = useState<UserDto | null>(null);
  const [profileFullName, setProfileFullName] = useState('');
  const [profileRole, setProfileRole] = useState('user');
  const [profilePassword, setProfilePassword] = useState('');

  async function load() {
    try {
      setError(null);
      const [usersRes, rolesRes, sitesRes] = await Promise.all([
        api.getUsers(),
        api.getRoles(),
        api.getAdminSites(),
      ]);
      setUsers(usersRes.items);
      setRoles(rolesRes.items);
      setSites(sitesRes.items);
      if (rolesRes.items.length > 0 && !rolesRes.items.some((r) => r.code === roleCode)) {
        setRoleCode(rolesRes.items[0].code);
      }
      if (sitesRes.items.length > 0 && selectedSiteIds.length === 0) {
        setSelectedSiteIds([sitesRes.items[0].id]);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const allSitesSelected = useMemo(
    () => selectedSiteIds.length === sites.length && sites.length > 0,
    [selectedSiteIds, sites],
  );

  function toggleSite(siteId: string, checked: boolean, mode: 'create' | 'edit') {
    const setter = mode === 'create' ? setSelectedSiteIds : setEditSiteIds;
    setter((prev) => (checked ? [...new Set([...prev, siteId])] : prev.filter((id) => id !== siteId)));
  }

  function toggleAllSites(checked: boolean, mode: 'create' | 'edit') {
    const ids = checked ? sites.map((s) => s.id) : [];
    if (mode === 'create') setSelectedSiteIds(ids);
    else setEditSiteIds(ids);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (selectedSiteIds.length === 0) {
      setError('Sélectionnez au moins un site');
      return;
    }
    try {
      setSubmitting(true);
      setError(null);
      await api.createUser({ email, fullName, roleCode, siteIds: selectedSiteIds });
      setEmail('');
      setFullName('');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setSubmitting(false);
    }
  }

  async function toggleActive(user: UserDto) {
    try {
      await api.updateUser(user.id, { active: !user.active });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  function startEdit(user: UserDto) {
    setEditingUser(user);
    setEditSiteIds(user.siteIds);
  }

  function startProfileEdit(user: UserDto) {
    setProfileUser(user);
    setProfileFullName(user.fullName);
    setProfileRole(user.roleCode);
    setProfilePassword('');
  }

  async function saveProfile() {
    if (!profileUser) return;
    try {
      setError(null);
      await api.updateUser(profileUser.id, {
        fullName: profileFullName,
        roleCode: profileRole,
        password: profilePassword || undefined,
      });
      setProfileUser(null);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function saveSites() {
    if (!editingUser || editSiteIds.length === 0) {
      setError('Sélectionnez au moins un site');
      return;
    }
    try {
      setError(null);
      await api.updateUser(editingUser.id, { siteIds: editSiteIds });
      setEditingUser(null);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={6} />;

  return (
    <div className="module-panel">
      {error && (
        <div className="module-error ux-alert" role="alert">
          <p>{error}</p>
        </div>
      )}

      {canWrite && (
      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Nouvel utilisateur</h3>
            <p className="ux-card-desc">Créez un compte et affectez-le à un ou plusieurs sites.</p>
          </div>
        </div>
        <form onSubmit={onSubmit}>
          <div className="ux-form-section">
            <div className="ux-form-grid">
              <label className="ux-field">
                <span className="ux-field-label">Email</span>
                <input className="ux-field-input" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Nom complet</span>
                <input className="ux-field-input" type="text" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Rôle</span>
                <select className="ux-field-input" value={roleCode} onChange={(e) => setRoleCode(e.target.value)}>
                  {roles.map((r) => (
                    <option key={r.code} value={r.code}>{r.label}</option>
                  ))}
                </select>
              </label>
            </div>
            <fieldset className="ux-site-picker">
              <legend style={{ fontSize: 12, fontWeight: 700, color: 'var(--text-3)', letterSpacing: '.06em', textTransform: 'uppercase', marginBottom: 10, display: 'block' }}>Sites affectés</legend>
              <label className="ux-checkbox" style={{ marginBottom: 8, display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
                <input type="checkbox" checked={allSitesSelected} onChange={(e) => toggleAllSites(e.target.checked, 'create')} />
                Tous les sites
              </label>
              <div className="ux-site-grid">
                {sites.map((site) => (
                  <label key={site.id} className="ux-checkbox ux-site-chip">
                    <input type="checkbox" checked={selectedSiteIds.includes(site.id)} onChange={(e) => toggleSite(site.id, e.target.checked, 'create')} />
                    <span>
                      <strong>{site.name}</strong>
                      {site.city && <small>{site.city}</small>}
                    </span>
                  </label>
                ))}
              </div>
            </fieldset>
          </div>
          <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
            <button type="submit" className="ux-btn-primary" disabled={submitting}>
              <IconPlus size={15} />
              {submitting ? 'Ajout…' : 'Ajouter l\'utilisateur'}
            </button>
          </div>
        </form>
      </section>
      )}

      {!canWrite && (
        <div className="ux-alert ux-alert-info">
          <p>Mode lecture seule — vous pouvez consulter les utilisateurs de vos sites.</p>
        </div>
      )}

      {profileUser && (
        <section className="ux-card ux-slide-in">
          <div className="ux-card-head">
            <div>
              <h3 className="ux-card-title">Modifier le profil</h3>
              <p className="ux-card-desc">{profileUser.email}</p>
            </div>
            <button type="button" className="db-ghost-btn" onClick={() => setProfileUser(null)}>Annuler</button>
          </div>
          <div className="ux-form-section">
            <div className="ux-form-grid">
              <label className="ux-field">
                <span className="ux-field-label">Nom complet</span>
                <input className="ux-field-input" value={profileFullName} onChange={(e) => setProfileFullName(e.target.value)} />
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Rôle</span>
                <select className="ux-field-input" value={profileRole} onChange={(e) => setProfileRole(e.target.value)}>
                  {roles.map((r) => <option key={r.code} value={r.code}>{r.label}</option>)}
                </select>
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Nouveau mot de passe</span>
                <input className="ux-field-input" type="password" value={profilePassword} onChange={(e) => setProfilePassword(e.target.value)} autoComplete="new-password" placeholder="Laisser vide pour ne pas changer" />
              </label>
            </div>
          </div>
          <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)', display: 'flex', gap: 10 }}>
            <button type="button" className="ux-btn-primary" onClick={() => void saveProfile()}>Enregistrer</button>
            <button type="button" className="db-ghost-btn" onClick={() => setProfileUser(null)}>Annuler</button>
          </div>
        </section>
      )}

      {editingUser && (
        <section className="ux-card ux-slide-in">
          <div className="ux-card-head">
            <div>
              <h3 className="ux-card-title">Affectation des sites</h3>
              <p className="ux-card-desc">{editingUser.fullName}</p>
            </div>
            <button type="button" className="db-ghost-btn" onClick={() => setEditingUser(null)}>Annuler</button>
          </div>
          <div className="ux-form-section">
            <label className="ux-checkbox" style={{ marginBottom: 8, display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
              <input type="checkbox" checked={editSiteIds.length === sites.length && sites.length > 0} onChange={(e) => toggleAllSites(e.target.checked, 'edit')} />
              Tous les sites
            </label>
            <div className="ux-site-grid">
              {sites.map((site) => (
                <label key={site.id} className="ux-checkbox ux-site-chip">
                  <input type="checkbox" checked={editSiteIds.includes(site.id)} onChange={(e) => toggleSite(site.id, e.target.checked, 'edit')} />
                  <span><strong>{site.name}</strong></span>
                </label>
              ))}
            </div>
          </div>
          <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)', display: 'flex', gap: 10 }}>
            <button type="button" className="ux-btn-primary" onClick={() => void saveSites()}>Enregistrer</button>
            <button type="button" className="db-ghost-btn" onClick={() => setEditingUser(null)}>Annuler</button>
          </div>
        </section>
      )}

      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Utilisateurs</h3>
            <p className="ux-card-desc">{users.length} compte{users.length > 1 ? 's' : ''} · Gestion des accès multi-sites</p>
          </div>
        </div>

        {users.length === 0 ? (
          <div className="ux-empty-state">
            <p>Aucun utilisateur pour le moment.</p>
          </div>
        ) : (
          <div className="ux-table-wrap">
            <table className="ux-table">
              <thead>
                <tr>
                  <th>Utilisateur</th>
                  <th>Rôle</th>
                  <th>Sites</th>
                  <th>Statut</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <div className="ux-user-cell">
                        <span className="ux-user-avatar">{userInitials(u.fullName)}</span>
                        <span>
                          <strong>{u.fullName}</strong>
                          <small>{u.email}</small>
                        </span>
                      </div>
                    </td>
                    <td>
                      <span className={roleBadgeClass(u.roleCode)}>{u.roleCode}</span>
                    </td>
                    <td className="ux-sites-cell">{siteNames(u.siteIds, sites)}</td>
                    <td>
                      <span className={`ux-status ${u.active ? 'ux-status--on' : 'ux-status--off'}`}>
                        <span className="pill-dot" />
                        {u.active ? 'Actif' : 'Inactif'}
                      </span>
                    </td>
                    <td>
                      {canWrite && (
                        <div className="ux-table-actions">
                          <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => startProfileEdit(u)}>Modifier</button>
                          <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => startEdit(u)}>Sites</button>
                          <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => void toggleActive(u)}>
                            {u.active ? 'Désactiver' : 'Activer'}
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
