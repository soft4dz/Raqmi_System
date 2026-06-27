import type { AuthUser, LicenseStatusResponse, ModuleItem } from '../api';

const FAMILY_LABELS: Record<string, string> = {
  core: 'Core',
  finance: 'Finance',
  hr: 'RH',
  operations: 'Opérations',
  specific: 'Spécifique',
  system: 'Système',
};

interface DashboardPageProps {
  user: AuthUser;
  modules: ModuleItem[];
  license: LicenseStatusResponse;
  onLogout: () => void;
}

export function DashboardPage({ user, modules, license, onLogout }: DashboardPageProps) {
  const enabledCount = modules.filter((module) => module.enabled).length;
  const families = [...new Set(modules.map((module) => module.family))];

  return (
    <div className="shell dashboard">
      <header className="topbar">
        <div className="brand compact">
          <div className="brand-mark">R</div>
          <div>
            <strong>Raqmi System</strong>
            <span>{user.tenant.name}</span>
          </div>
        </div>
        <div className="topbar-actions">
          <span className="user-chip">{user.fullName}</span>
          <button type="button" className="ghost" onClick={onLogout}>
            Déconnexion
          </button>
        </div>
      </header>

      <section className="hero">
        <div>
          <p className="eyebrow">Tableau de bord</p>
          <h2>Bienvenue, {user.fullName}</h2>
          <p className="muted">
            Pack {license.pack?.label ?? license.license.kind} — {enabledCount} modules actifs sur{' '}
            {modules.length}
          </p>
        </div>
        <div className={`license-badge ${license.evaluation.valid ? 'valid' : 'invalid'}`}>
          <span>Licence</span>
          <strong>{license.evaluation.valid ? 'Valide' : 'Refusée'}</strong>
          <small>Expire le {new Date(license.license.expiresAt).toLocaleDateString('fr-FR')}</small>
        </div>
      </section>

      <section className="stats">
        <article>
          <span>Modules actifs</span>
          <strong>{enabledCount}</strong>
        </article>
        <article>
          <span>Familles</span>
          <strong>{families.length}</strong>
        </article>
        <article>
          <span>Pack</span>
          <strong>{license.pack?.label ?? license.license.kind}</strong>
        </article>
        <article>
          <span>Mode</span>
          <strong>{license.evaluation.readonlyMode ? 'Lecture seule' : 'Normal'}</strong>
        </article>
      </section>

      <section className="modules-section">
        <div className="section-head">
          <h3>Modules disponibles</h3>
          <p>Seuls les modules inclus dans votre licence sont activés.</p>
        </div>

        <div className="module-grid">
          {modules.map((module) => (
            <article
              key={module.code}
              className={`module-card ${module.enabled ? 'enabled' : 'disabled'}`}
            >
              <div className="module-card-head">
                <span className="family">{FAMILY_LABELS[module.family] ?? module.family}</span>
                <span className={`status ${module.enabled ? 'on' : 'off'}`}>
                  {module.enabled ? 'Actif' : 'Bloqué'}
                </span>
              </div>
              <h4>{module.label}</h4>
              <p>{module.description}</p>
              <code>{module.code}</code>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}
