import { FormEvent, useState } from 'react';
import { IconBuilding, IconGlobe, IconShield, IconZap } from '../components/icons';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
import { ThemeToggle } from '../components/ThemeToggle';
import { useI18n } from '../i18n/I18nProvider';

interface LoginPageProps {
  onLogin: (email: string, password: string) => Promise<void>;
  error: string | null;
}

export function LoginPage({ onLogin, error }: LoginPageProps) {
  const { t } = useI18n();
  const [email, setEmail] = useState('admin@demo.raqmi.local');
  const [password, setPassword] = useState('demo1234');
  const [submitting, setSubmitting] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setLocalError(null);
    try {
      await onLogin(email, password);
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : t('loginFailed'));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="login-outer">
      {/* Left — Form */}
      <div className="login-left">
        <div className="login-lang-bar">
          <LanguageSwitcher />
          <ThemeToggle />
        </div>

        <div className="login-brand">
          <div className="brand-mark-lg">R</div>
          <div>
            <div className="login-brand-name">{t('appName')}</div>
            <div className="login-brand-tag">{t('appTagline')}</div>
          </div>
        </div>

        <div className="login-heading">
          <h1 className="login-title">{t('login')}</h1>
          <p className="login-subtitle">{t('loginAsideText')}</p>
        </div>

        <form className="login-form-new" onSubmit={handleSubmit}>
          <label className="field-group">
            <span className="field-label">{t('email')}</span>
            <input
              className="field-input"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              required
            />
          </label>
          <label className="field-group">
            <span className="field-label">{t('password')}</span>
            <input
              className="field-input"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </label>

          {(error || localError) && (
            <p className="error">{localError ?? error}</p>
          )}

          <button className="btn-primary-login ux-btn-login" type="submit" disabled={submitting}>
            {submitting ? t('loggingIn') : t('login')}
          </button>
        </form>

        <p className="login-demo-hint">
          {t('demoAccount')} <code>admin@demo.raqmi.local</code> / <code>demo1234</code>
        </p>
      </div>

      {/* Right — Features */}
      <div className="login-right">
        <div className="login-right-glow-1" aria-hidden="true" />
        <div className="login-right-glow-2" aria-hidden="true" />

        <div className="login-right-content">
          <p className="login-eyebrow">ERP Algérien</p>
          <h2 className="login-right-title">{t('loginAsideTitle')}</h2>
          <p className="login-right-sub">{t('appTagline')}</p>

          <ul className="feature-list">
            <li className="feature-item">
              <span className="feature-icon feature-icon--blue"><IconShield size={20} /></span>
              <div>
                <strong>{t('loginAsideItem2')}</strong>
                <p>Fingerprint machine, fonctionnement garanti hors-ligne</p>
              </div>
            </li>
            <li className="feature-item">
              <span className="feature-icon feature-icon--green"><IconBuilding size={20} /></span>
              <div>
                <strong>{t('loginAsideItem1')}</strong>
                <p>Finance, RH, Opérations, Comptabilité algérienne</p>
              </div>
            </li>
            <li className="feature-item">
              <span className="feature-icon feature-icon--amber"><IconGlobe size={20} /></span>
              <div>
                <strong>Multi-langue & RTL</strong>
                <p>Français, Arabe, Tamazight — Interface adaptative</p>
              </div>
            </li>
            <li className="feature-item">
              <span className="feature-icon feature-icon--teal"><IconZap size={20} /></span>
              <div>
                <strong>{t('loginAsideItem3')}</strong>
                <p>Application Electron native, mises à jour automatiques</p>
              </div>
            </li>
          </ul>

          <div className="login-status-badge">
            <span className="status-dot-green" />
            Système opérationnel
          </div>
        </div>
      </div>
    </div>
  );
}
