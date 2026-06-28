import { FormEvent, useEffect, useState } from 'react';
import { api, type EmployeeDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';

export function EmployeesPage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const [items, setItems] = useState<EmployeeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [department, setDepartment] = useState('');

  async function load() {
    try {
      setError(null);
      setItems((await api.getEmployees()).items);
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
      await api.createEmployee({ firstName, lastName, department });
      setFirstName('');
      setLastName('');
      setDepartment('');
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
        <h3 className="ux-card-title">Nouvel employé</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field"><span className="ux-field-label">Prénom</span><input className="ux-field-input" value={firstName} onChange={(e) => setFirstName(e.target.value)} required /></label>
          <label className="ux-field"><span className="ux-field-label">Nom</span><input className="ux-field-input" value={lastName} onChange={(e) => setLastName(e.target.value)} required /></label>
          <label className="ux-field"><span className="ux-field-label">Service</span><input className="ux-field-input" value={department} onChange={(e) => setDepartment(e.target.value)} /></label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Ajouter</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Employés ({items.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead><tr><th>Matricule</th><th>Nom</th><th>Service</th><th>Statut</th></tr></thead>
            <tbody>
              {items.map((e) => (
                <tr key={e.id}>
                  <td><code>{e.matricule}</code></td>
                  <td>{e.firstName} {e.lastName}</td>
                  <td>{e.department}</td>
                  <td>{e.status}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
