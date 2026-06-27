import { FormEvent, useEffect, useState } from 'react';
import { api, type TenantSettingsDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';

export function TenantSettingsPage() {
  const [settings, setSettings] = useState<TenantSettingsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    void (async () => {
      try {
        setSettings(await api.getTenantSettings());
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!settings) return;
    try {
      setSaving(true);
      setError(null);
      setSuccess(false);
      const updated = await api.updateTenantSettings(settings);
      setSettings(updated);
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setSaving(false);
    }
  }

  if (loading || !settings) return <TableSkeleton rows={6} />;

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}
      {success && <div className="ux-alert-success">Paramètres enregistrés.</div>}

      <form className="ux-card ux-form" onSubmit={onSubmit}>
        <h3 className="ux-card-title">Entreprise</h3>
        <div className="ux-form-grid">
          <label className="ux-field">
            <span className="ux-field-label">Raison sociale</span>
            <input className="ux-field-input" value={settings.legalName ?? ''} onChange={(e) => setSettings({ ...settings, legalName: e.target.value })} />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Email</span>
            <input className="ux-field-input" type="email" value={settings.email ?? ''} onChange={(e) => setSettings({ ...settings, email: e.target.value })} />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Téléphone</span>
            <input className="ux-field-input" value={settings.phone ?? ''} onChange={(e) => setSettings({ ...settings, phone: e.target.value })} />
          </label>
          <label className="ux-field ux-field--wide">
            <span className="ux-field-label">Adresse</span>
            <input className="ux-field-input" value={settings.address ?? ''} onChange={(e) => setSettings({ ...settings, address: e.target.value })} />
          </label>
        </div>

        <h3 className="ux-card-title" style={{ marginTop: 20 }}>Formats & délais</h3>
        <div className="ux-form-grid">
          <label className="ux-field">
            <span className="ux-field-label">Devise</span>
            <input className="ux-field-input" value={settings.currency} onChange={(e) => setSettings({ ...settings, currency: e.target.value })} />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Fuseau horaire</span>
            <input className="ux-field-input" value={settings.timezone} onChange={(e) => setSettings({ ...settings, timezone: e.target.value })} />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Format date</span>
            <input className="ux-field-input" value={settings.dateFormat} onChange={(e) => setSettings({ ...settings, dateFormat: e.target.value })} />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Délai paiement (jours)</span>
            <input className="ux-field-input" type="number" min={0} value={settings.paymentDelayDays} onChange={(e) => setSettings({ ...settings, paymentDelayDays: Number(e.target.value) })} />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">Délai relance (jours)</span>
            <input className="ux-field-input" type="number" min={0} value={settings.reminderDelayDays} onChange={(e) => setSettings({ ...settings, reminderDelayDays: Number(e.target.value) })} />
          </label>
        </div>

        <h3 className="ux-card-title" style={{ marginTop: 20 }}>Branding</h3>
        <div className="ux-form-grid">
          <label className="ux-field">
            <span className="ux-field-label">Couleur principale</span>
            <input className="ux-field-input" type="color" value={settings.brandPrimaryColor ?? '#2563eb'} onChange={(e) => setSettings({ ...settings, brandPrimaryColor: e.target.value })} />
          </label>
          <label className="ux-field">
            <span className="ux-field-label">URL logo</span>
            <input className="ux-field-input" value={settings.brandLogoUrl ?? ''} onChange={(e) => setSettings({ ...settings, brandLogoUrl: e.target.value })} placeholder="https://..." />
          </label>
        </div>

        <button type="submit" className="db-primary-btn ux-btn-primary" disabled={saving} style={{ marginTop: 16 }}>
          {saving ? 'Enregistrement…' : 'Enregistrer les paramètres'}
        </button>
      </form>
    </div>
  );
}
