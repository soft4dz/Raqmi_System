import { useEffect, useState } from 'react';
import { api, type AuditLogDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useI18n } from '../../i18n/I18nProvider';

export function AuditLogPage() {
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<AuditLogDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const res = await api.getAuditLogs();
        setItems(res.items);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  if (loading) return <TableSkeleton rows={8} />;

  return (
    <div className="module-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}

      <section className="ux-card">
        <h3 className="ux-card-title">Journal d&apos;audit ({items.length})</h3>
        {items.length === 0 ? (
          <div className="ux-empty-state"><p>Aucun événement enregistré.</p></div>
        ) : (
          <div className="module-table-wrap ux-table-wrap">
            <table className="module-table ux-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Action</th>
                  <th>Module</th>
                  <th>Description</th>
                </tr>
              </thead>
              <tbody>
                {items.map((log) => (
                  <tr key={log.id}>
                    <td>{new Date(log.createdAt).toLocaleString(dateLocale)}</td>
                    <td><span className="ux-badge ux-badge--user">{log.action}</span></td>
                    <td>{log.moduleCode ?? '—'}</td>
                    <td>{log.description}</td>
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
