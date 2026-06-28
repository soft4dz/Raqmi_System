import { useEffect, useState } from 'react';
import { api, type InventoryDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { useI18n } from '../../i18n/I18nProvider';

export function InventoryPage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<InventoryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    try {
      setError(null);
      setItems((await api.getInventories()).items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    setLoading(true);
    void load();
  }, [activeSiteId]);

  async function startSession() {
    try {
      await api.createInventory();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={4} />;

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}
      {activeSite && <p className="ux-card-desc">Site actif : <strong>{activeSite.name}</strong></p>}

      <section className="ux-card">
        <div className="ux-card-head">
          <h3 className="ux-card-title">Sessions d&apos;inventaire ({items.length})</h3>
          <button type="button" className="db-primary-btn ux-btn-primary" onClick={() => void startSession()}>Ouvrir inventaire</button>
        </div>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead><tr><th>Date</th><th>Statut</th><th>Site</th></tr></thead>
            <tbody>
              {items.map((s) => (
                <tr key={s.id}>
                  <td>{new Date(s.sessionDate).toLocaleDateString(dateLocale)}</td>
                  <td>{s.status}</td>
                  <td>{s.siteId}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
