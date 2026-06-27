import { LOCALE_LABELS, type Locale } from '../i18n/messages';
import { useI18n } from '../i18n/I18nProvider';

export function LanguageSwitcher() {
  const { locale, setLocale, t } = useI18n();

  return (
    <label className="lang-switcher">
      <span>{t('language')}</span>
      <select value={locale} onChange={(event) => setLocale(event.target.value as Locale)}>
        {(Object.keys(LOCALE_LABELS) as Locale[]).map((code) => (
          <option key={code} value={code}>
            {LOCALE_LABELS[code]}
          </option>
        ))}
      </select>
    </label>
  );
}
