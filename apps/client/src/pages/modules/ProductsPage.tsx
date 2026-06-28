import { FormEvent, useEffect, useState } from 'react';
import { api, type ProductDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';

export function ProductsPage() {
  const [items, setItems] = useState<ProductDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [unit, setUnit] = useState('u');

  async function load() {
    try {
      setError(null);
      setItems((await api.getProducts()).items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    try {
      await api.createProduct({ code: code || undefined, name, unit });
      setCode('');
      setName('');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={5} />;

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Nouveau produit</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field"><span className="ux-field-label">Code</span><input className="ux-field-input" value={code} onChange={(e) => setCode(e.target.value)} /></label>
          <label className="ux-field"><span className="ux-field-label">Nom</span><input className="ux-field-input" value={name} onChange={(e) => setName(e.target.value)} required /></label>
          <label className="ux-field"><span className="ux-field-label">Unité</span><input className="ux-field-input" value={unit} onChange={(e) => setUnit(e.target.value)} /></label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Ajouter</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Produits ({items.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead><tr><th>Code</th><th>Nom</th><th>Unité</th><th>Stock min.</th></tr></thead>
            <tbody>
              {items.map((p) => (
                <tr key={p.id}><td><code>{p.code}</code></td><td>{p.name}</td><td>{p.unit}</td><td>{p.minStockLevel}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
