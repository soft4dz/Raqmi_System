import type { AuthUser, LicenseStatusResponse, ModuleItem } from '../api';
import type { Messages } from '../i18n/messages';
import { useI18n } from '../i18n/I18nProvider';
import { FamilyIcon, IconArrowRight, IconCheck } from '../components/icons';
import { screenForModule } from '../navigation/moduleRoutes';

const MODULE_FAMILIES = new Set<string>(['core', 'finance', 'hr', 'operations', 'specific', 'system']);

function moduleFamilyKey(family: string): keyof Messages['family'] {
  return MODULE_FAMILIES.has(family) ? (family as keyof Messages['family']) : 'core';
}

const FAMILY_COLORS: Record<string, string> = {
  core: 'var(--icon-blue)',
  finance: 'var(--icon-teal)',
  hr: 'var(--icon-green)',
  operations: 'var(--icon-amber)',
  specific: 'var(--icon-purple)',
  system: 'var(--icon-gray)',
};

interface DashboardPageProps {
  user: AuthUser;
  modules: ModuleItem[];
  license: LicenseStatusResponse;
  onModuleOpen: (code: string) => void;
}

export function DashboardPage({ user, modules, license, onModuleOpen }: DashboardPageProps) {
  const { t, tf, dateLocale } = useI18n();
  const enabledCount = modules.filter((m) => m.enabled).length;
  const families = [...new Set(modules.map((m) => m.family))];
  const packLabel = license.pack?.label ?? license.license.kind;

  const navigable = modules.filter((m) => screenForModule(m.code) !== null);

  const byFamily = families
    .map((fam) => ({
      key: fam,
      label: tf(moduleFamilyKey(fam)),
      modules: navigable.filter((m) => m.family === fam),
    }))
    .filter((g) => g.modules.length > 0);

  return (
    <>
      <section className="db-hero ux-hero-enhanced">
        <div className="db-hero-glow" aria-hidden="true" />
        <div className="db-hero-content">
          <p className="db-eyebrow">{t('dashboard')}</p>
          <h2 className="db-hero-title">{t('welcome', { name: user.fullName.split(' ')[0] })}</h2>
          <p className="db-hero-sub">
            {t('packSummary', { pack: packLabel, enabled: enabledCount, total: modules.length })}
          </p>
        </div>
        <div className={`db-lic-badge ux-lic-card ${license.evaluation.valid ? 'db-lic-valid' : 'db-lic-invalid'}`}>
          <span className="db-lic-label">{t('license')}</span>
          <strong className="db-lic-status">
            {license.evaluation.valid ? (
              <span className="ux-inline-icon"><IconCheck size={14} /> {t('licenseValid')}</span>
            ) : (
              t('licenseInvalid')
            )}
          </strong>
          <span className="db-lic-date">
            {t('expiresOn', {
              date: new Date(license.license.expiresAt).toLocaleDateString(dateLocale),
            })}
          </span>
        </div>
      </section>

      <section className="db-kpis ux-kpi-grid">
        <article className="db-kpi db-kpi--blue ux-kpi-card">
          <span className="db-kpi-label">{t('activeModules')}</span>
          <strong className="db-kpi-value ux-kpi-num">{enabledCount}</strong>
          <span className="db-kpi-sub">sur {modules.length} disponibles</span>
        </article>
        <article className="db-kpi db-kpi--green ux-kpi-card">
          <span className="db-kpi-label">{t('families')}</span>
          <strong className="db-kpi-value ux-kpi-num">{families.length}</strong>
          <span className="db-kpi-sub">{families.slice(0, 3).join(', ')}</span>
        </article>
        <article className="db-kpi db-kpi--amber ux-kpi-card">
          <span className="db-kpi-label">{t('pack')}</span>
          <strong className="db-kpi-value db-kpi-value--sm">{packLabel}</strong>
          <span className="db-kpi-sub">{license.evaluation.valid ? t('licenseValid') : t('licenseInvalid')}</span>
        </article>
        <article className="db-kpi db-kpi--teal ux-kpi-card">
          <span className="db-kpi-label">{t('mode')}</span>
          <strong className="db-kpi-value db-kpi-value--sm">
            {license.evaluation.readonlyMode ? t('readonly') : t('normal')}
          </strong>
          <span className="db-kpi-sub">Mode opérationnel</span>
        </article>
      </section>

      <section className="db-modules">
        <div className="db-section-head">
          <h3 className="db-section-title">{t('availableModules')}</h3>
          <p className="db-section-sub">{t('modulesHint')}</p>
        </div>

        {byFamily.map(({ key, label, modules: fmods }) => (
          <div key={key} className="db-family-block">
            <div className="db-family-label">
              <span className="db-family-icon" style={{ background: FAMILY_COLORS[key] ?? 'var(--icon-gray)' }}>
                <FamilyIcon family={key} size={15} />
              </span>
              {label}
            </div>
            <div className="db-module-grid">
              {fmods.map((mod) => {
                const clickable = mod.enabled && screenForModule(mod.code) !== null;
                return (
                  <article
                    key={mod.code}
                    className={`db-mod-card ux-mod-card ${mod.enabled ? 'db-mod-on' : 'db-mod-off'} ${clickable ? 'db-mod-clickable' : ''}`}
                    role={clickable ? 'button' : undefined}
                    tabIndex={clickable ? 0 : undefined}
                    onClick={() => {
                      if (clickable) onModuleOpen(mod.code);
                    }}
                    onKeyDown={(e) => {
                      if (clickable && (e.key === 'Enter' || e.key === ' ')) {
                        e.preventDefault();
                        onModuleOpen(mod.code);
                      }
                    }}
                  >
                    <div className="db-mod-head">
                      <span
                        className="db-mod-icon"
                        style={{ background: FAMILY_COLORS[mod.family] ?? 'var(--icon-gray)' }}
                      >
                        <FamilyIcon family={mod.family} size={16} />
                      </span>
                      <span className={`db-status-pill ${mod.enabled ? 'pill-green' : 'pill-red'}`}>
                        <span className="pill-dot" />
                        {mod.enabled ? t('active') : t('blocked')}
                      </span>
                    </div>
                    <h4 className="db-mod-name">{mod.label}</h4>
                    <p className="db-mod-desc">{mod.description}</p>
                    {clickable && (
                      <span className="ux-mod-cta">
                        Ouvrir <IconArrowRight size={14} />
                      </span>
                    )}
                    <code className="db-mod-code">{mod.code}</code>
                  </article>
                );
              })}
            </div>
          </div>
        ))}
      </section>
    </>
  );
}
