import { FormEvent, useEffect, useMemo, useState } from 'react';
import { api, type AuditLogDto, type AuthUser, type UserDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useI18n } from '../../i18n/I18nProvider';

const ACTION_OPTIONS = ['login', 'create', 'update', 'delete'];

const ACTION_BADGE: Record<string, string> = {
  login:  'ux-badge--manager',
  create: 'ux-badge--custom',
  update: 'ux-badge--user',
  delete: 'ux-badge--admin',
};

export function AuditLogPage({ user: _user }: { user: AuthUser }) {
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<AuditLogDto[]>([]);
  const [users, setUsers] = useState<UserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [action, setAction] = useState('');
  const [moduleCode, setModuleCode] = useState('');
  const [q, setQ] = useState('');

  const userMap = useMemo(() => new Map(users.map((u) => [u.id, u.fullName])), [users]);

  async function load() {
    try {
      setError(null);
      const [logsRes, usersRes] = await Promise.all([
        api.getAuditLogs({ action: action || undefined, moduleCode: moduleCode || undefined, q: q || undefined }),
        api.getUsers(),
      ]);
      setItems(logsRes.items);
      setUsers(usersRes.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, []);

  function onFilter(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    void load();
  }

  if (loading && items.length === 0) return <TableSkeleton rows={8} />;

  return (
    <div className="module-panel">
      {error && <div className="ux-alert" role="alert"><p>{error}</p></div>}

      {/* Filtres */}
      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Filtres</h3>
            <p className="ux-card-desc">Affinez les événements par action, module ou mot-clé.</p>
          </div>
        </div>
        <form onSubmit={onFilter}>
          <div className="ux-form-section">
            <div className="ux-form-grid">
              <label className="ux-field">
                <span className="ux-field-label">Action</span>
                <select className="ux-field-input" value={action} onChange={(e) => setAction(e.target.value)}>
                  <option value="">Toutes les actions</option>
                  {ACTION_OPTIONS.map((a) => (
                    <option key={a} value={a}>{a.charAt(0).toUpperCase() + a.slice(1)}</option>
                  ))}
                </select>
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Module</span>
                <input className="ux-field-input" value={moduleCode} onChange={(e) => setModuleCode(e.target.value)} placeholder="ex: administration, sites…" />
              </label>
              <label className="ux-field">
                <span className="ux-field-label">Recherche libre</span>
                <input className="ux-field-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Description, entité…" />
              </label>
            </div>
          </div>
          <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
            <button type="submit" className="ux-btn-primary" disabled={loading}>
              {loading ? 'Chargement…' : 'Appliquer les filtres'}
            </button>
          </div>
        </form>
      </section>

      {/* Journal */}
      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Journal d'audit</h3>
            <p className="ux-card-desc">{items.length} événement{items.length > 1 ? 's' : ''} enregistré{items.length > 1 ? 's' : ''}</p>
          </div>
        </div>

        {items.length === 0 ? (
          <div className="ux-empty-state"><p>Aucun événement ne correspond aux filtres.</p></div>
        ) : (
          <div className="ux-table-wrap">
            <table className="ux-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Utilisateur</th>
                  <th>Action</th>
                  <th>Module</th>
                  <th>Description</th>
                </tr>
              </thead>
              <tbody>
                {items.map((log) => (
                  <tr key={log.id}>
                    <td style={{ whiteSpace: 'nowrap', color: 'var(--text-3)', fontSize: 12 }}>
                      {new Date(log.createdAt).toLocaleString(dateLocale)}
                    </td>
                    <td style={{ fontWeight: 500, color: 'var(--text)' }}>
                      {log.userId ? userMap.get(log.userId) ?? log.userId : '—'}
                    </td>
                    <td>
                      <span className={`ux-badge ${ACTION_BADGE[log.action] ?? 'ux-badge--user'}`}>
                        {log.action}
                      </span>
                    </td>
                    <td style={{ color: 'var(--text-3)', fontSize: 12.5 }}>{log.moduleCode ?? '—'}</td>
                    <td style={{ color: 'var(--text-2)' }}>{log.description}</td>
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
