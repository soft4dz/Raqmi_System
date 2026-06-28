import { useEffect, useState } from 'react';
import { api, type AuditLogDto, type AuthUser, type LicenseStatusResponse, type ModuleItem } from '../api';
import { useI18n } from '../i18n/I18nProvider';

const IMPLEMENTED_MODULES = new Set(['administration', 'settings', 'sites']);

const FAMILY_BG: Record<string, string> = {
  core:       'var(--icon-blue)',
  finance:    'var(--icon-gold)',
  hr:         'var(--icon-green)',
  operations: 'var(--icon-amber)',
  specific:   'var(--icon-purple)',
  system:     'var(--icon-gray)',
};

const MOD_ICON: Record<string, string> = {
  administration: 'M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z',
  settings:       'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
  sites:          'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4',
};

const QUICK_LINKS = [
  { label: 'Utilisateurs',      screen: 'admin_users',   icon: 'M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z', color: 'var(--blue)' },
  { label: 'Rôles & permissions', screen: 'admin_roles', icon: 'M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z', color: 'var(--purple)' },
  { label: 'Sites / unités',    screen: 'core_sites',    icon: 'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4', color: 'var(--teal)' },
  { label: 'Paramétrage',       screen: 'core_settings', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065zM15 12a3 3 0 11-6 0 3 3 0 016 0z', color: 'var(--amber)' },
  { label: 'Journal d\'audit',  screen: 'admin_audit',   icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01', color: 'var(--green)' },
] as const;

const ACTION_COLOR: Record<string, string> = {
  login: 'var(--blue-l)', create: 'var(--green)', update: 'var(--amber)', delete: 'var(--red-text)',
};

interface DashboardPageProps {
  user: AuthUser;
  modules: ModuleItem[];
  license: LicenseStatusResponse;
  onNavigate?: (screen: string) => void;
}

export function DashboardPage({ user, modules, license, onNavigate }: DashboardPageProps) {
  const { t, tf, dateLocale } = useI18n();
  const [auditLogs, setAuditLogs] = useState<AuditLogDto[]>([]);

  useEffect(() => {
    void api.getAuditLogs({ }).then((r) => setAuditLogs(r.items.slice(0, 8))).catch(() => {});
  }, []);

  const visibleModules = modules.filter((m) => IMPLEMENTED_MODULES.has(m.code));
  const enabledCount = visibleModules.filter((m) => m.enabled).length;
  const families = [...new Set(visibleModules.map((m) => m.family))];
  const packLabel = license.pack?.label ?? license.license.kind;
  const expiryDate = new Date(license.license.expiresAt).toLocaleDateString(dateLocale, {
    day: '2-digit', month: 'long', year: 'numeric',
  });

  const byFamily = families.map((fam) => ({
    key: fam,
    label: tf(fam as Parameters<typeof tf>[0]),
    modules: visibleModules.filter((m) => m.family === fam),
  }));

  return (
    <div className="db-modules">
      {/* Hero */}
      <div className="db-hero">
        <div className="db-hero-glow" aria-hidden="true" />
        <div className="db-hero-glow-2" aria-hidden="true" />
        <div style={{ position: 'relative', zIndex: 1 }}>
          <p className="db-eyebrow">{t('dashboard')}</p>
          <h1 className="db-hero-title">{t('welcome', { name: user.fullName })}</h1>
          <p className="db-hero-sub">
            {packLabel} · {enabledCount} module{enabledCount > 1 ? 's' : ''} actif{enabledCount > 1 ? 's' : ''} sur {visibleModules.length}
          </p>
        </div>
        <div className={`db-lic-badge ${license.evaluation.valid ? 'db-lic-valid' : 'db-lic-invalid'}`} style={{ position: 'relative', zIndex: 1 }}>
          <span className="db-lic-label">{t('license')}</span>
          <strong className="db-lic-status">
            {license.evaluation.valid ? `✓ ${t('licenseValid')}` : `✗ ${t('licenseInvalid')}`}
          </strong>
          <span className="db-lic-date">{t('expiresOn', { date: expiryDate })}</span>
        </div>
      </div>

      {/* KPIs */}
      <div className="db-kpis">
        <div className="db-kpi db-kpi--blue">
          <div className="db-kpi-top">
            <span className="db-kpi-label">{t('activeModules')}</span>
            <div className="db-kpi-icon" style={{ background: 'var(--icon-blue)' }}>
              <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2" style={{ color: 'var(--blue)' }}><path d="M4 6h16M4 12h16M4 18h7"/></svg>
            </div>
          </div>
          <strong className="db-kpi-value">{enabledCount}</strong>
          <span className="db-kpi-sub">sur {visibleModules.length} disponibles</span>
        </div>
        <div className="db-kpi db-kpi--green">
          <div className="db-kpi-top">
            <span className="db-kpi-label">Sites actifs</span>
            <div className="db-kpi-icon" style={{ background: 'var(--icon-green)' }}>
              <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2" style={{ color: 'var(--green)' }}><path d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"/></svg>
            </div>
          </div>
          <strong className="db-kpi-value">{user.siteIds?.length ?? '—'}</strong>
          <span className="db-kpi-sub">sites affectés</span>
        </div>
        <div className="db-kpi db-kpi--amber">
          <div className="db-kpi-top">
            <span className="db-kpi-label">{t('pack')}</span>
            <div className="db-kpi-icon" style={{ background: 'var(--icon-gold)' }}>
              <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2" style={{ color: 'var(--gold)' }}><path d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z"/></svg>
            </div>
          </div>
          <strong className="db-kpi-value db-kpi-value--sm">{packLabel}</strong>
          <span className="db-kpi-sub">{license.evaluation.valid ? t('licenseValid') : t('licenseInvalid')}</span>
        </div>
        <div className="db-kpi db-kpi--teal">
          <div className="db-kpi-top">
            <span className="db-kpi-label">{t('mode')}</span>
            <div className="db-kpi-icon" style={{ background: 'var(--icon-teal)' }}>
              <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2" style={{ color: 'var(--teal)' }}><path d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
            </div>
          </div>
          <strong className="db-kpi-value db-kpi-value--sm">
            {license.evaluation.readonlyMode ? t('readonly') : t('normal')}
          </strong>
          <span className="db-kpi-sub">Mode opérationnel</span>
        </div>
      </div>

      {/* Corps principal : 2 colonnes */}
      <div className="db-body-grid">

        {/* Colonne gauche : modules */}
        <div className="db-body-main">
          <div className="db-section-head" style={{ marginBottom: 16 }}>
            <div>
              <h2 className="db-section-title">{t('availableModules')}</h2>
              <p className="db-section-sub">{t('modulesHint')}</p>
            </div>
          </div>

          {byFamily.map(({ key, label, modules: fmods }) => (
            <div key={key} className="db-family-block" style={{ marginBottom: 20 }}>
              <div className="db-family-label">
                {label}
                <span style={{ marginLeft: 'auto', fontSize: 10, fontWeight: 500 }}>
                  {fmods.filter(m => m.enabled).length}/{fmods.length}
                </span>
              </div>
              <div className="db-module-grid">
                {fmods.map((mod) => (
                  <article key={mod.code} className={`db-mod-card ${mod.enabled ? 'db-mod-on' : 'db-mod-off'}`}>
                    <div className="db-mod-head">
                      <span className="db-mod-icon" style={{ background: FAMILY_BG[mod.family] ?? 'var(--icon-gray)' }}>
                        <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8" style={{ color: 'var(--blue)' }}>
                          <path d={MOD_ICON[mod.code] ?? 'M4 6h16M4 12h16M4 18h7'} />
                        </svg>
                      </span>
                      <span className={`db-status-pill ${mod.enabled ? 'pill-green' : 'pill-red'}`}>
                        <span className="pill-dot" />
                        {mod.enabled ? t('active') : t('blocked')}
                      </span>
                    </div>
                    <div>
                      <h4 className="db-mod-name">{mod.label}</h4>
                      <p className="db-mod-desc">{mod.description}</p>
                    </div>
                  </article>
                ))}
              </div>
            </div>
          ))}
        </div>

        {/* Colonne droite */}
        <aside className="db-body-aside">

          {/* Accès rapides */}
          <div className="ux-card db-aside-card">
            <div className="db-aside-head">
              <span className="db-aside-title">Accès rapides</span>
            </div>
            <nav className="db-quicklinks">
              {QUICK_LINKS.map((link) => (
                <button
                  key={link.screen}
                  type="button"
                  className="db-quicklink"
                  onClick={() => onNavigate?.(link.screen)}
                >
                  <span className="db-quicklink-icon" style={{ background: `${link.color}18` }}>
                    <svg width="15" height="15" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="1.8" style={{ color: link.color }}>
                      <path d={link.icon} />
                    </svg>
                  </span>
                  <span className="db-quicklink-label">{link.label}</span>
                  <svg className="db-quicklink-chevron" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2"><path d="M9 5l7 7-7 7"/></svg>
                </button>
              ))}
            </nav>
          </div>

          {/* Dernière activité */}
          <div className="ux-card db-aside-card">
            <div className="db-aside-head">
              <span className="db-aside-title">Activité récente</span>
            </div>
            {auditLogs.length === 0 ? (
              <p className="db-aside-empty">Aucun événement récent.</p>
            ) : (
              <ul className="db-activity-list">
                {auditLogs.map((log) => (
                  <li key={log.id} className="db-activity-item">
                    <span
                      className="db-activity-dot"
                      style={{ background: ACTION_COLOR[log.action] ?? 'var(--border-2)' }}
                    />
                    <div className="db-activity-body">
                      <p className="db-activity-desc">{log.description}</p>
                      <span className="db-activity-meta">
                        {log.action} · {new Date(log.createdAt).toLocaleString(dateLocale, { hour: '2-digit', minute: '2-digit', day: '2-digit', month: 'short' })}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

        </aside>
      </div>
    </div>
  );
}
