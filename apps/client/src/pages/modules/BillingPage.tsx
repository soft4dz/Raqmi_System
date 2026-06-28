import { FormEvent, useEffect, useState } from 'react';
import { api, type InvoiceDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { useI18n } from '../../i18n/I18nProvider';

export function BillingPage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<InvoiceDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [clientName, setClientName] = useState('');

  async function load() {
    try {
      setError(null);
      setItems((await api.getInvoices()).items);
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

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    try {
      await api.createInvoice({
        clientName,
        lines: [{ description: 'Prestation', quantity: 1, unitPrice: 10000, taxRate: 19 }],
      });
      setClientName('');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function markSent(id: string) {
    try {
      await api.updateInvoice(id, { status: 'sent' });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={5} />;

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}
      {activeSite && <p className="ux-card-desc">Site actif : <strong>{activeSite.name}</strong></p>}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Nouvelle facture</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field">
            <span className="ux-field-label">Client</span>
            <input className="ux-field-input" value={clientName} onChange={(e) => setClientName(e.target.value)} required />
          </label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Créer brouillon</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Factures ({items.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead>
              <tr><th>N°</th><th>Client</th><th>Date</th><th>Total</th><th>Statut</th><th /></tr>
            </thead>
            <tbody>
              {items.map((inv) => (
                <tr key={inv.id}>
                  <td><code>{inv.number}</code></td>
                  <td>{inv.clientName}</td>
                  <td>{new Date(inv.issueDate).toLocaleDateString(dateLocale)}</td>
                  <td>{inv.totalAmount.toLocaleString(dateLocale)}</td>
                  <td>{inv.status}</td>
                  <td>
                    {inv.status === 'draft' && (
                      <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => void markSent(inv.id)}>Envoyer</button>
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
