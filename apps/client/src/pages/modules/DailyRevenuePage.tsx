import { FormEvent, useEffect, useState } from 'react';
import { api, type DailyRevenueDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { useI18n } from '../../i18n/I18nProvider';

export function DailyRevenuePage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<DailyRevenueDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [amount, setAmount] = useState('0');
  const [category, setCategory] = useState('restaurant');
  const [notes, setNotes] = useState('');

  async function load() {
    try {
      setError(null);
      const res = await api.getDailyRevenue();
      setItems(res.items);
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
      await api.createDailyRevenue({ amount: Number(amount), category, notes: notes || undefined });
      setAmount('0');
      setNotes('');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function onValidate(id: string) {
    try {
      await api.validateDailyRevenue(id);
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
        <h3 className="ux-card-title">Nouvelle recette</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field">
            <span className="ux-field-label">Montant (DZD)</span>
            <input className="ux-field-input" type="number" min={0} value={amount} onChange={(e) => setAmount(e.target.value)} required />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Catégorie</span>
            <select className="ux-field-input" value={category} onChange={(e) => setCategory(e.target.value)}>
              <option value="restaurant">Restaurant</option>
              <option value="bar">Bar</option>
              <option value="room">Chambres</option>
              <option value="general">Général</option>
            </select>
          </label>
          <label className="ux-field ux-field--wide">
            <span className="ux-field-label">Notes</span>
            <input className="ux-field-input" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Enregistrer</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Recettes ({items.length})</h3>
        {items.length === 0 ? (
          <div className="ux-empty-state"><p>Aucune recette pour ce site.</p></div>
        ) : (
          <div className="module-table-wrap ux-table-wrap">
            <table className="module-table ux-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Catégorie</th>
                  <th>Montant</th>
                  <th>Statut</th>
                  <th>Notes</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((r) => (
                  <tr key={r.id}>
                    <td>{new Date(r.businessDate).toLocaleDateString(dateLocale)}</td>
                    <td>{r.category}</td>
                    <td>{r.amount.toLocaleString(dateLocale)}</td>
                    <td><span className={`ux-badge ${r.status === 'validated' ? 'ux-badge--admin' : 'ux-badge--user'}`}>{r.status}</span></td>
                    <td>{r.notes ?? '—'}</td>
                    <td>
                      {r.status !== 'validated' && (
                        <button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => void onValidate(r.id)}>Valider</button>
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
