import { FormEvent, useEffect, useState } from 'react';
import { api, type ProductDto, type StockMovementDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { useI18n } from '../../i18n/I18nProvider';

export function StockMovementsPage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<StockMovementDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [stockByProduct, setStockByProduct] = useState<{ productId: string; quantity: number }[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [productId, setProductId] = useState('');
  const [type, setType] = useState<'in' | 'out'>('in');
  const [quantity, setQuantity] = useState('1');

  async function load() {
    try {
      setError(null);
      const [mov, prods] = await Promise.all([api.getStockMovements(), api.getProducts()]);
      setItems(mov.items);
      setStockByProduct(mov.stockByProduct);
      setProducts(prods.items);
      if (!productId && prods.items[0]) setProductId(prods.items[0].id);
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
      await api.createStockMovement({ productId, type, quantity: Number(quantity) });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={5} />;

  const prodName = (id: string) => products.find((p) => p.id === id)?.name ?? id;

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}
      {activeSite && <p className="ux-card-desc">Site actif : <strong>{activeSite.name}</strong></p>}

      {stockByProduct.length > 0 && (
        <section className="ux-card">
          <h3 className="ux-card-title">Niveaux de stock</h3>
          <ul className="ux-card-desc">
            {stockByProduct.map((s) => (
              <li key={s.productId}>{prodName(s.productId)} : <strong>{s.quantity}</strong></li>
            ))}
          </ul>
        </section>
      )}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Nouveau mouvement</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field">
            <span className="ux-field-label">Produit</span>
            <select className="ux-field-input" value={productId} onChange={(e) => setProductId(e.target.value)}>
              {products.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Type</span>
            <select className="ux-field-input" value={type} onChange={(e) => setType(e.target.value as 'in' | 'out')}>
              <option value="in">Entrée</option>
              <option value="out">Sortie</option>
            </select>
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Quantité</span>
            <input className="ux-field-input" type="number" min={1} value={quantity} onChange={(e) => setQuantity(e.target.value)} />
          </label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Enregistrer</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Mouvements ({items.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead><tr><th>Date</th><th>Produit</th><th>Type</th><th>Qté</th></tr></thead>
            <tbody>
              {items.map((m) => (
                <tr key={m.id}>
                  <td>{new Date(m.movementDate).toLocaleDateString(dateLocale)}</td>
                  <td>{prodName(m.productId)}</td>
                  <td>{m.type}</td>
                  <td>{m.quantity}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
