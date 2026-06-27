import { FormEvent, useEffect, useMemo, useState } from 'react';
import { getServerUrl, normalizeServerUrl, setServerUrl, testServerUrl } from '../api';
import { LanguageSwitcher } from '../components/LanguageSwitcher';
import { useI18n } from '../i18n/I18nProvider';

interface SettingsPageProps {
  onConfigured: () => void;
}

export function SettingsPage({ onConfigured }: SettingsPageProps) {
  const { t } = useI18n();
  const [serverAddress, setServerAddress] = useState('http://localhost:3000');
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void getServerUrl().then(setServerAddress);
  }, []);

  const resolvedUrl = useMemo(() => {
    const normalized = normalizeServerUrl(serverAddress);
    return normalized || null;
  }, [serverAddress]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setTesting(true);
    setError(null);
    try {
      const normalized = normalizeServerUrl(serverAddress);
      if (!normalized) {
        setError(t('serverInvalid'));
        return;
      }
      const ok = await testServerUrl(normalized);
      if (!ok) {
        setError(t('serverUnreachable'));
        return;
      }
      await setServerUrl(normalized);
      onConfigured();
    } catch (err) {
      setError(err instanceof Error ? err.message : t('loginFailed'));
    } finally {
      setTesting(false);
    }
  }

  return (
    <div className="shell center">
      <LanguageSwitcher />
      <section className="login-panel ux-settings-panel" style={{ maxWidth: 520 }}>
        <h1>{t('serverSettings')}</h1>
        <p className="muted">{t('serverSettingsHint')}</p>
        <form className="login-form" onSubmit={handleSubmit}>
          <label>
            {t('serverUrl')}
            <input
              type="text"
              inputMode="url"
              autoComplete="off"
              spellCheck={false}
              value={serverAddress}
              onChange={(event) => setServerAddress(event.target.value)}
              placeholder={t('serverUrlPlaceholder')}
              required
            />
          </label>
          <p className="muted ux-server-examples">{t('serverUrlExamples')}</p>
          {resolvedUrl && resolvedUrl !== serverAddress.trim() && (
            <p className="ux-server-resolved">
              {t('serverUrlResolved', { url: resolvedUrl })}
            </p>
          )}
          {error && <p className="error">{error}</p>}
          <button type="submit" disabled={testing}>
            {testing ? t('testing') : t('saveContinue')}
          </button>
        </form>
      </section>
    </div>
  );
}
