import { FormEvent, useEffect, useState } from 'react';
import { api, type AttendanceDto, type EmployeeDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { useI18n } from '../../i18n/I18nProvider';

export function AttendancePage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<AttendanceDto[]>([]);
  const [employees, setEmployees] = useState<EmployeeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [employeeId, setEmployeeId] = useState('');
  const [checkIn, setCheckIn] = useState('08:00');
  const [checkOut, setCheckOut] = useState('17:00');

  async function load() {
    try {
      setError(null);
      const [att, emps] = await Promise.all([api.getAttendance(), api.getEmployees()]);
      setItems(att.items);
      setEmployees(emps.items);
      if (!employeeId && emps.items[0]) setEmployeeId(emps.items[0].id);
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
      await api.createAttendance({ employeeId, checkIn, checkOut });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  if (loading) return <TableSkeleton rows={5} />;

  const empName = (id: string) => {
    const e = employees.find((x) => x.id === id);
    return e ? `${e.firstName} ${e.lastName}` : id;
  };

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}
      {activeSite && <p className="ux-card-desc">Site actif : <strong>{activeSite.name}</strong></p>}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Pointage du jour</h3>
        <form className="ux-form-grid" onSubmit={onSubmit}>
          <label className="ux-field">
            <span className="ux-field-label">Employé</span>
            <select className="ux-field-input" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
              {employees.map((e) => <option key={e.id} value={e.id}>{e.firstName} {e.lastName}</option>)}
            </select>
          </label>
          <label className="ux-field"><span className="ux-field-label">Entrée</span><input className="ux-field-input" type="time" value={checkIn} onChange={(e) => setCheckIn(e.target.value)} /></label>
          <label className="ux-field"><span className="ux-field-label">Sortie</span><input className="ux-field-input" type="time" value={checkOut} onChange={(e) => setCheckOut(e.target.value)} /></label>
          <button type="submit" className="db-primary-btn ux-btn-primary">Enregistrer</button>
        </form>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Pointages ({items.length})</h3>
        <div className="module-table-wrap ux-table-wrap">
          <table className="module-table ux-table">
            <thead><tr><th>Date</th><th>Employé</th><th>Entrée</th><th>Sortie</th></tr></thead>
            <tbody>
              {items.map((a) => (
                <tr key={a.id}>
                  <td>{new Date(a.workDate).toLocaleDateString(dateLocale)}</td>
                  <td>{empName(a.employeeId)}</td>
                  <td>{a.checkIn ?? '—'}</td>
                  <td>{a.checkOut ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
