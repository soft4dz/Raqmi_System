import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { detectLocale, formatMessage, getMessages, LOCALE_DATE, type Locale, type Messages } from './messages';

const STORAGE_KEY = 'raqmi_locale';

interface I18nContextValue {
  locale: Locale;
  messages: Messages;
  dateLocale: string;
  dir: 'ltr' | 'rtl';
  setLocale: (locale: Locale) => void;
  t: (key: keyof Messages, vars?: Record<string, string | number>) => string;
  tf: (family: keyof Messages['family']) => string;
}

const I18nContext = createContext<I18nContextValue | null>(null);

async function loadStoredLocale(): Promise<Locale> {
  if (window.raqmi?.getConfig) {
    const config = await window.raqmi.getConfig();
    if (config.locale === 'fr' || config.locale === 'en' || config.locale === 'ar') {
      return config.locale;
    }
  }

  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === 'fr' || stored === 'en' || stored === 'ar') return stored;
  return detectLocale();
}

async function persistLocale(locale: Locale): Promise<void> {
  localStorage.setItem(STORAGE_KEY, locale);
  if (window.raqmi?.getConfig && window.raqmi?.setConfig) {
    const config = await window.raqmi.getConfig();
    await window.raqmi.setConfig({ ...config, locale });
  }
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>('fr');
  const [ready, setReady] = useState(false);

  useEffect(() => {
    void loadStoredLocale().then((value) => {
      setLocaleState(value);
      setReady(true);
    });
  }, []);

  const messages = useMemo(() => getMessages(locale), [locale]);
  const dateLocale = LOCALE_DATE[locale];
  const dir = locale === 'ar' ? 'rtl' : 'ltr';

  useEffect(() => {
    document.documentElement.lang = locale;
    document.documentElement.dir = dir;
  }, [locale, dir]);

  const value = useMemo<I18nContextValue>(
    () => ({
      locale,
      messages,
      dateLocale,
      dir,
      setLocale: (next) => {
        setLocaleState(next);
        void persistLocale(next);
      },
      t: (key, vars) => {
        const raw = messages[key];
        if (typeof raw !== 'string') return String(key);
        return vars ? formatMessage(raw, vars) : raw;
      },
      tf: (family) => messages.family[family] ?? family,
    }),
    [locale, messages, dateLocale, dir],
  );

  if (!ready) return null;

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error('useI18n must be used within I18nProvider');
  return ctx;
}
