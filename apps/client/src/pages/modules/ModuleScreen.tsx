import { useEffect, useState } from 'react';
import { api } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import type { AppScreen } from '../../navigation/moduleRoutes';

const ENDPOINTS: Partial<Record<AppScreen, string>> = {
  admin_users: '/api/v1/admin/users',
  daily_revenue: '/api/v1/finance/daily-revenue',
  billing: '/api/v1/finance/invoices',
  treasury: '/api/v1/finance/treasury/movements',
  hr_employees: '/api/v1/hr/employees',
  hr_contracts: '/api/v1/hr/employees',
  hr_attendance: '/api/v1/hr/attendance',
  stocks_products: '/api/v1/stocks/products',
  stocks_movements: '/api/v1/stocks/movements',
  stocks_inventory: '/api/v1/stocks/inventories',
  ged: '/api/v1/ged/documents',
};

interface ModuleScreenProps {
  screen: AppScreen;
}

export function ModuleScreen({ screen }: ModuleScreenProps) {
  const [items, setItems] = useState<Record<string, unknown>[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const path = ENDPOINTS[screen];

  useEffect(() => {
    if (!path) {
      setLoading(false);
      return;
    }

    void (async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await api.fetchJson<{ items?: Record<string, unknown>[] }>(path);
        setItems(data.items ?? []);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur de chargement');
        setItems([]);
      } finally {
        setLoading(false);
      }
    })();
  }, [path, screen]);

  if (loading) return <TableSkeleton rows={5} />;

  if (error) {
    return (
      <div className="module-error ux-alert" role="alert">
        <p>{error}</p>
        <p className="muted module-error-hint">
          Utilisez le serveur C# (.NET) avec <code>pnpm dotnet:server</code> pour les modules métier.
        </p>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="ux-empty-state ux-empty-state--large">
        <p className="ux-empty-title">Aucune donnée</p>
        <p className="muted">Ce module est prêt — les enregistrements apparaîtront ici dès qu&apos;ils seront créés.</p>
      </div>
    );
  }

  const columns = Object.keys(items[0] ?? {});

  return (
    <div className="ux-card">
      <div className="module-table-wrap ux-table-wrap">
        <table className="module-table ux-table">
          <thead>
            <tr>
              {columns.map((col) => (
                <th key={col}>{col}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((row, i) => (
              <tr key={i}>
                {columns.map((col) => (
                  <td key={col}>{String(row[col] ?? '')}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
