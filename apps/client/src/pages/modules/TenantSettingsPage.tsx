import { FormEvent, useEffect, useState, type ReactNode } from 'react';
import { api, type AuthUser, type LicenseStatusResponse, type TenantSettingsDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { Tabs } from '../../components/Tabs';
import { canWriteSettings } from '../../lib/permissions';
import { ALGERIAN_LEGAL_FORMS, LEGAL_FORM_GROUPS, normalizeLegalFormValue } from '../../lib/legalForms';
import { useI18n } from '../../i18n/I18nProvider';

function Field({ label, hint, children, wide }: {
  label: string; hint?: string; children: ReactNode; wide?: boolean;
}) {
  return (
    <label className={`ux-field${wide ? ' ux-field--wide' : ''}`}>
      <span className="ux-field-label">{label}</span>
      {children}
      {hint && <span className="ux-card-desc" style={{ marginTop: 4 }}>{hint}</span>}
    </label>
  );
}

const TABS = [
  { key: 'identity',  label: 'Identification' },
  { key: 'contact',   label: 'Coordonnées' },
  { key: 'billing',   label: 'Facturation' },
  { key: 'security',  label: 'Sécurité' },
  { key: 'storage',   label: 'Stockage & Sauvegarde' },
  { key: 'branding',  label: 'Apparence' },
];

export function TenantSettingsPage({ user }: { user: AuthUser }) {
  const { dateLocale } = useI18n();
  const canWrite = canWriteSettings(user.permissions, user.roleCode);
  const [settings, setSettings] = useState<TenantSettingsDto | null>(null);
  const [license, setLicense] = useState<LicenseStatusResponse | null>(null);
  const [siteCount, setSiteCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  function patch<K extends keyof TenantSettingsDto>(key: K, value: TenantSettingsDto[K]) {
    setSettings((prev) => (prev ? { ...prev, [key]: value } : prev));
  }

  useEffect(() => {
    void (async () => {
      try {
        const [settingsRes, licenseRes, sitesRes] = await Promise.all([
          api.getTenantSettings(),
          api.getLicenseStatus(),
          api.getAdminSites(),
        ]);
        setSettings({ ...settingsRes, legalForm: normalizeLegalFormValue(settingsRes.legalForm) });
        setLicense(licenseRes);
        setSiteCount(sitesRes.items.filter((s) => s.active).length);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!settings || !canWrite) return;
    try {
      setSaving(true);
      setError(null);
      setSuccess(false);
      const updated = await api.updateTenantSettings(settings);
      setSettings(updated);
      setSuccess(true);
      setTimeout(() => setSuccess(false), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setSaving(false);
    }
  }

  if (loading || !settings) return <TableSkeleton rows={10} />;

  return (
    <div className="module-panel">
      {error && <div className="ux-alert" role="alert"><p>{error}</p></div>}
      {success && <div className="ux-alert-success">Paramètres enregistrés avec succès.</div>}

      {/* Vue d'ensemble */}
      <section className="ux-card">
        <div className="ux-card-head">
          <div>
            <h3 className="ux-card-title">Vue d'ensemble</h3>
            <p className="ux-card-desc">{user.tenant.name} · {user.tenant.code}</p>
          </div>
          {!canWrite && (
            <span className="ux-badge ux-badge--user">Lecture seule</span>
          )}
        </div>
        <div style={{ padding: '20px 24px' }}>
          <div className="ux-form-grid">
            <div className="ux-field">
              <span className="ux-field-label">Licence</span>
              <p style={{ fontSize: 14, fontWeight: 600, color: 'var(--text)', marginTop: 4 }}>
                {license?.pack?.label ?? license?.license.kind ?? '—'}
              </p>
            </div>
            <div className="ux-field">
              <span className="ux-field-label">Expiration</span>
              <p style={{ fontSize: 14, fontWeight: 600, color: 'var(--text)', marginTop: 4 }}>
                {license?.license.expiresAt ? new Date(license.license.expiresAt).toLocaleDateString(dateLocale) : '—'}
              </p>
            </div>
            <div className="ux-field">
              <span className="ux-field-label">Sites actifs</span>
              <p style={{ fontSize: 14, fontWeight: 600, color: 'var(--text)', marginTop: 4 }}>{siteCount}</p>
            </div>
            <div className="ux-field">
              <span className="ux-field-label">Modules autorisés</span>
              <p style={{ fontSize: 14, fontWeight: 600, color: 'var(--text)', marginTop: 4 }}>
                {license?.license.allowedModules.length ?? 0}
              </p>
            </div>
            <div className="ux-field">
              <span className="ux-field-label">Dernière mise à jour</span>
              <p style={{ fontSize: 13, color: 'var(--text-3)', marginTop: 4 }}>
                {settings.updatedAt ? new Date(settings.updatedAt).toLocaleString(dateLocale) : '—'}
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Onglets */}
      <form onSubmit={onSubmit}>
        <Tabs tabs={TABS}>
          {(active) => (
            <fieldset disabled={!canWrite} style={{ border: 'none', padding: 0 }}>
              {active === 'identity' && (
                <section className="ux-card">
                  <div className="ux-card-head">
                    <div>
                      <h3 className="ux-card-title">Identification légale</h3>
                      <p className="ux-card-desc">Informations officielles de l'entreprise</p>
                    </div>
                  </div>
                  <div className="ux-form-section">
                    <div className="ux-form-grid">
                      <Field label="Raison sociale">
                        <input className="ux-field-input" value={settings.legalName ?? ''} onChange={(e) => patch('legalName', e.target.value)} />
                      </Field>
                      <Field label="Nom commercial">
                        <input className="ux-field-input" value={settings.tradeName ?? ''} onChange={(e) => patch('tradeName', e.target.value)} />
                      </Field>
                      <Field label="Forme juridique" hint="Personnes physiques et sociétés selon le droit algérien">
                        <select className="ux-field-input" value={normalizeLegalFormValue(settings.legalForm)} onChange={(e) => patch('legalForm', e.target.value)}>
                          {LEGAL_FORM_GROUPS.map((group) => (
                            <optgroup key={group} label={group}>
                              {ALGERIAN_LEGAL_FORMS.filter((f) => f.group === group).map((f) => (
                                <option key={f.value} value={f.value}>{f.label}</option>
                              ))}
                            </optgroup>
                          ))}
                        </select>
                      </Field>
                      <Field label="Secteur d'activité">
                        <input className="ux-field-input" value={settings.activitySector ?? ''} onChange={(e) => patch('activitySector', e.target.value)} />
                      </Field>
                      <Field label="N° registre commerce (RC)">
                        <input className="ux-field-input" value={settings.registrationNumber ?? ''} onChange={(e) => patch('registrationNumber', e.target.value)} />
                      </Field>
                      <Field label="NIF">
                        <input className="ux-field-input" value={settings.taxId ?? ''} onChange={(e) => patch('taxId', e.target.value)} />
                      </Field>
                      <Field label="NIS">
                        <input className="ux-field-input" value={settings.statisticalId ?? ''} onChange={(e) => patch('statisticalId', e.target.value)} />
                      </Field>
                      <Field label="Article d'imposition (AI)">
                        <input className="ux-field-input" value={settings.vatArticle ?? ''} onChange={(e) => patch('vatArticle', e.target.value)} />
                      </Field>
                    </div>
                  </div>
                  {canWrite && <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
                    <button type="submit" className="ux-btn-primary" disabled={saving}>{saving ? 'Enregistrement…' : 'Enregistrer'}</button>
                  </div>}
                </section>
              )}

              {active === 'contact' && (
                <section className="ux-card">
                  <div className="ux-card-head">
                    <div>
                      <h3 className="ux-card-title">Coordonnées</h3>
                      <p className="ux-card-desc">Adresse et contacts de l'entreprise</p>
                    </div>
                  </div>
                  <div className="ux-form-section">
                    <div className="ux-form-grid">
                      <Field label="Email">
                        <input className="ux-field-input" type="email" value={settings.email ?? ''} onChange={(e) => patch('email', e.target.value)} />
                      </Field>
                      <Field label="Téléphone">
                        <input className="ux-field-input" value={settings.phone ?? ''} onChange={(e) => patch('phone', e.target.value)} />
                      </Field>
                      <Field label="Site web">
                        <input className="ux-field-input" value={settings.website ?? ''} onChange={(e) => patch('website', e.target.value)} placeholder="https://..." />
                      </Field>
                      <Field label="Adresse" wide>
                        <input className="ux-field-input" value={settings.address ?? ''} onChange={(e) => patch('address', e.target.value)} />
                      </Field>
                      <Field label="Ville">
                        <input className="ux-field-input" value={settings.city ?? ''} onChange={(e) => patch('city', e.target.value)} />
                      </Field>
                      <Field label="Wilaya">
                        <input className="ux-field-input" value={settings.wilaya ?? ''} onChange={(e) => patch('wilaya', e.target.value)} />
                      </Field>
                      <Field label="Code postal">
                        <input className="ux-field-input" value={settings.postalCode ?? ''} onChange={(e) => patch('postalCode', e.target.value)} />
                      </Field>
                      <Field label="Pays">
                        <input className="ux-field-input" value={settings.country ?? ''} onChange={(e) => patch('country', e.target.value)} />
                      </Field>
                    </div>
                  </div>
                  {canWrite && <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
                    <button type="submit" className="ux-btn-primary" disabled={saving}>{saving ? 'Enregistrement…' : 'Enregistrer'}</button>
                  </div>}
                </section>
              )}

              {active === 'billing' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--sp-5)' }}>
                  <section className="ux-card">
                    <div className="ux-card-head">
                      <div>
                        <h3 className="ux-card-title">Formats & délais</h3>
                        <p className="ux-card-desc">Devise, fuseau horaire et délais de paiement</p>
                      </div>
                    </div>
                    <div className="ux-form-section">
                      <div className="ux-form-grid">
                        <Field label="Devise">
                          <input className="ux-field-input" value={settings.currency} onChange={(e) => patch('currency', e.target.value)} />
                        </Field>
                        <Field label="Fuseau horaire">
                          <input className="ux-field-input" value={settings.timezone} onChange={(e) => patch('timezone', e.target.value)} />
                        </Field>
                        <Field label="Format date">
                          <input className="ux-field-input" value={settings.dateFormat} onChange={(e) => patch('dateFormat', e.target.value)} />
                        </Field>
                        <Field label="Format nombre">
                          <input className="ux-field-input" value={settings.numberFormat} onChange={(e) => patch('numberFormat', e.target.value)} placeholder="fr-DZ" />
                        </Field>
                        <Field label="Délai paiement (jours)">
                          <input className="ux-field-input" type="number" min={0} value={settings.paymentDelayDays} onChange={(e) => patch('paymentDelayDays', Number(e.target.value))} />
                        </Field>
                        <Field label="Délai relance (jours)">
                          <input className="ux-field-input" type="number" min={0} value={settings.reminderDelayDays} onChange={(e) => patch('reminderDelayDays', Number(e.target.value))} />
                        </Field>
                      </div>
                    </div>
                  </section>

                  <section className="ux-card">
                    <div className="ux-card-head">
                      <div>
                        <h3 className="ux-card-title">Numérotation & TVA</h3>
                        <p className="ux-card-desc">Préfixes, compteurs et taux par défaut</p>
                      </div>
                    </div>
                    <div className="ux-form-section">
                      <div className="ux-form-grid">
                        <Field label="Préfixe factures">
                          <input className="ux-field-input" value={settings.invoicePrefix} onChange={(e) => patch('invoicePrefix', e.target.value)} />
                        </Field>
                        <Field label="Préfixe devis">
                          <input className="ux-field-input" value={settings.quotePrefix} onChange={(e) => patch('quotePrefix', e.target.value)} />
                        </Field>
                        <Field label="Prochain n° facture">
                          <input className="ux-field-input" type="number" min={1} value={settings.nextInvoiceNumber} onChange={(e) => patch('nextInvoiceNumber', Number(e.target.value))} />
                        </Field>
                        <Field label="Mois début exercice">
                          <select className="ux-field-input" value={settings.fiscalYearStartMonth} onChange={(e) => patch('fiscalYearStartMonth', Number(e.target.value))}>
                            {Array.from({ length: 12 }, (_, i) => (
                              <option key={i + 1} value={i + 1}>{new Date(2000, i, 1).toLocaleString(dateLocale, { month: 'long' })}</option>
                            ))}
                          </select>
                        </Field>
                        <Field label="TVA par défaut (%)">
                          <input className="ux-field-input" type="number" min={0} step={0.5} value={settings.defaultVatRate} onChange={(e) => patch('defaultVatRate', Number(e.target.value))} />
                        </Field>
                        <Field label="Modes de paiement" hint="Séparés par des virgules">
                          <input className="ux-field-input" value={settings.acceptedPaymentMethods ?? ''} onChange={(e) => patch('acceptedPaymentMethods', e.target.value)} />
                        </Field>
                        <Field label="Pied de page facture" wide>
                          <textarea className="ux-field-input" rows={3} value={settings.invoiceFooter ?? ''} onChange={(e) => patch('invoiceFooter', e.target.value)} />
                        </Field>
                      </div>
                    </div>
                  </section>

                  {canWrite && <button type="submit" className="ux-btn-primary" style={{ width: 'fit-content' }} disabled={saving}>{saving ? 'Enregistrement…' : 'Enregistrer'}</button>}
                </div>
              )}

              {active === 'security' && (
                <section className="ux-card">
                  <div className="ux-card-head">
                    <div>
                      <h3 className="ux-card-title">Sécurité & sessions</h3>
                      <p className="ux-card-desc">Politique de mots de passe et expiration des sessions</p>
                    </div>
                  </div>
                  <div className="ux-form-section">
                    <div className="ux-form-grid">
                      <Field label="Expiration session (minutes)">
                        <input className="ux-field-input" type="number" min={15} value={settings.sessionTimeoutMinutes} onChange={(e) => patch('sessionTimeoutMinutes', Number(e.target.value))} />
                      </Field>
                      <Field label="Longueur mot de passe min.">
                        <input className="ux-field-input" type="number" min={6} value={settings.passwordMinLength} onChange={(e) => patch('passwordMinLength', Number(e.target.value))} />
                      </Field>
                      <Field label="Renouvellement mot de passe (jours)" hint="0 = désactivé">
                        <input className="ux-field-input" type="number" min={0} value={settings.forcePasswordChangeDays} onChange={(e) => patch('forcePasswordChangeDays', Number(e.target.value))} />
                      </Field>
                    </div>
                  </div>
                  {canWrite && <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
                    <button type="submit" className="ux-btn-primary" disabled={saving}>{saving ? 'Enregistrement…' : 'Enregistrer'}</button>
                  </div>}
                </section>
              )}

              {active === 'storage' && (
                <section className="ux-card">
                  <div className="ux-card-head">
                    <div>
                      <h3 className="ux-card-title">Stockage & sauvegarde</h3>
                      <p className="ux-card-desc">Configuration du stockage fichiers et des sauvegardes automatiques</p>
                    </div>
                  </div>
                  <div className="ux-form-section">
                    <div className="ux-form-grid">
                      <Field label="Driver stockage">
                        <select className="ux-field-input" value={settings.storageDriver} onChange={(e) => patch('storageDriver', e.target.value)}>
                          <option value="local">Local</option>
                          <option value="s3">S3 / compatible</option>
                          <option value="azure">Azure Blob</option>
                        </select>
                      </Field>
                      <Field label="Taille max. upload (Mo)">
                        <input className="ux-field-input" type="number" min={1} value={settings.maxUploadSizeMb} onChange={(e) => patch('maxUploadSizeMb', Number(e.target.value))} />
                      </Field>
                      <Field label="Fréquence sauvegarde">
                        <select className="ux-field-input" value={settings.backupFrequency} onChange={(e) => patch('backupFrequency', e.target.value)}>
                          <option value="daily">Quotidienne</option>
                          <option value="weekly">Hebdomadaire</option>
                          <option value="monthly">Mensuelle</option>
                        </select>
                      </Field>
                      <Field label="Rétention sauvegardes (jours)">
                        <input className="ux-field-input" type="number" min={1} value={settings.backupRetentionDays} onChange={(e) => patch('backupRetentionDays', Number(e.target.value))} />
                      </Field>
                    </div>
                  </div>
                  {canWrite && <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
                    <button type="submit" className="ux-btn-primary" disabled={saving}>{saving ? 'Enregistrement…' : 'Enregistrer'}</button>
                  </div>}
                </section>
              )}

              {active === 'branding' && (
                <section className="ux-card">
                  <div className="ux-card-head">
                    <div>
                      <h3 className="ux-card-title">Apparence & branding</h3>
                      <p className="ux-card-desc">Couleurs et logo utilisés dans les documents générés</p>
                    </div>
                  </div>
                  <div className="ux-form-section">
                    <div className="ux-form-grid">
                      <Field label="Couleur principale">
                        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                          <input type="color" value={settings.brandPrimaryColor ?? '#2563eb'} onChange={(e) => patch('brandPrimaryColor', e.target.value)} style={{ width: 44, height: 36, border: '1px solid var(--border)', borderRadius: 'var(--r-sm)', cursor: 'pointer', padding: 2 }} />
                          <input className="ux-field-input" value={settings.brandPrimaryColor ?? '#2563eb'} onChange={(e) => patch('brandPrimaryColor', e.target.value)} style={{ flex: 1 }} />
                        </div>
                      </Field>
                      <Field label="Couleur secondaire">
                        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                          <input type="color" value={settings.brandSecondaryColor ?? '#0f766e'} onChange={(e) => patch('brandSecondaryColor', e.target.value)} style={{ width: 44, height: 36, border: '1px solid var(--border)', borderRadius: 'var(--r-sm)', cursor: 'pointer', padding: 2 }} />
                          <input className="ux-field-input" value={settings.brandSecondaryColor ?? '#0f766e'} onChange={(e) => patch('brandSecondaryColor', e.target.value)} style={{ flex: 1 }} />
                        </div>
                      </Field>
                      <Field label="URL logo" wide>
                        <input className="ux-field-input" value={settings.brandLogoUrl ?? ''} onChange={(e) => patch('brandLogoUrl', e.target.value)} placeholder="https://..." />
                      </Field>
                    </div>
                  </div>
                  {canWrite && <div style={{ padding: '16px 24px', borderTop: '1px solid var(--border)' }}>
                    <button type="submit" className="ux-btn-primary" disabled={saving}>{saving ? 'Enregistrement…' : 'Enregistrer'}</button>
                  </div>}
                </section>
              )}
            </fieldset>
          )}
        </Tabs>
      </form>
    </div>
  );
}
