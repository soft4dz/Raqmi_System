import { FormEvent, useEffect, useState } from 'react';
import { api, type TreasuryMovementDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { useI18n } from '../../i18n/I18nProvider';

export function TreasuryPage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<TreasuryMovementDto[]>([]);
  const [balance, setBalance] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [type, setType] = useState<'in' | 'out'>('in');
  const [amount, setAmount] = useState('0');
  const [label, setLabel] = useState('');

  async function load() {
    try {
      setError(null);
      const res = await api.getTreasury();
      setItems(res.items);
      setBalance(res.balance);
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
      await api.createTreasuryMovement({ type, amount: Number(amount), label: label || undefined });
      setAmount('0');
      setLabel('');
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

      <section className="db-kpi ux-kpi-card" style={{ marginBottom: 16 }}>
        <span className="db-kpi-label">Solde trésorerie</span>
        <strong className="db-kpi-value">{balance.toLocaleString(dateLocale)} DZD</strong>
      </section>

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Nouveau mouvement</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field">
            <span className="ux-field-label">Type</span>
            <select className="ux-field-input" value={type} onChange={(e) => setType(e.target.value as 'in' | 'out')}>
              <option value="in">Entrée</option>
              <option value="out">Sortie</option>
            </select>
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Montant</span>
            <input className="ux-field-input" type="number" min={0} value={amount} onChange={(e) => setAmount(e.target.value)} required />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Libellé</span>
            <input className="ux-field-input" value={label} onChange={(e) => setLabel(e.target.value)} />
          </label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Enregistrer</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Mouvements ({items.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead>
              <tr><th>Date</th><th>Type</th><th>Compte</th><th>Montant</th><th>Libellé</th></tr>
            </thead>
            <tbody>
              {items.map((m) => (
                <tr key={m.id}>
                  <td>{new Date(m.movementDate).toLocaleDateString(dateLocale)}</td>
                  <td>{m.type === 'in' ? 'Entrée' : 'Sortie'}</td>
                  <td>{m.account}</td>
                  <td>{m.amount.toLocaleString(dateLocale)}</td>
                  <td>{m.label}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
