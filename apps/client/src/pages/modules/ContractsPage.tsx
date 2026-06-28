import { FormEvent, useEffect, useState } from 'react';
import { api, type ContractDto, type EmployeeDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useI18n } from '../../i18n/I18nProvider';

export function ContractsPage() {
  const { dateLocale } = useI18n();
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [employeeId, setEmployeeId] = useState('');
  const [contracts, setContracts] = useState<ContractDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [contractType, setContractType] = useState('cdi');
  const [baseSalary, setBaseSalary] = useState('65000');

  useEffect(() => {
    void (async () => {
      try {
        const res = await api.getEmployees();
        setEmployees(res.items);
        if (res.items[0]) setEmployeeId(res.items[0].id);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!employeeId) return;
    void (async () => {
      try {
        setContracts((await api.getContracts(employeeId)).items);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur');
      }
    })();
  }, [employeeId]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!employeeId) return;
    try {
      await api.createContract(employeeId, { contractType, baseSalary: Number(baseSalary) });
      setContracts((await api.getContracts(employeeId)).items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={5} />;

  const selected = employees.find((e) => e.id === employeeId);

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Contrats employé</h3>
        <label className="ux-field">
          <span className="ux-field-label">Employé</span>
          <select className="ux-field-input" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
            {employees.map((e) => (
              <option key={e.id} value={e.id}>{e.firstName} {e.lastName} ({e.matricule})</option>
            ))}
          </select>
        </label>
        {selected && <p className="ux-card-desc">{selected.department} — {selected.status}</p>}
      </section>

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Nouveau contrat</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field">
            <span className="ux-field-label">Type</span>
            <select className="ux-field-input" value={contractType} onChange={(e) => setContractType(e.target.value)}>
              <option value="cdi">CDI</option>
              <option value="cdd">CDD</option>
              <option value="stage">Stage</option>
            </select>
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Salaire de base</span>
            <input className="ux-field-input" type="number" min={0} value={baseSalary} onChange={(e) => setBaseSalary(e.target.value)} />
          </label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Créer contrat</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Contrats ({contracts.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead><tr><th>Type</th><th>Début</th><th>Salaire</th></tr></thead>
            <tbody>
              {contracts.map((c) => (
                <tr key={c.id}>
                  <td>{c.contractType.toUpperCase()}</td>
                  <td>{new Date(c.startDate).toLocaleDateString(dateLocale)}</td>
                  <td>{c.baseSalary.toLocaleString(dateLocale)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
