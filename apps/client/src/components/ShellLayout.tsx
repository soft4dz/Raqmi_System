import { useEffect, useState, type ReactNode } from 'react';
import type { AuthUser, LicenseStatusResponse, ModuleItem } from '../api';
import { LanguageSwitcher } from './LanguageSwitcher';
import { ThemeToggle } from './ThemeToggle';
import { useI18n } from '../i18n/I18nProvider';
import {
  buildNavGroups,
  screenTitle,
  type AppScreen,
} from '../navigation/moduleRoutes';
import { useSiteContext } from '../context/SiteContext';
import {
  IconClose,
  IconDashboard,
  IconLicense,
  IconLogout,
  IconMenu,
} from './icons';
import { PageHeader } from './PageHeader';

interface ShellLayoutProps {
  user: AuthUser;
  modules: ModuleItem[];
  license: LicenseStatusResponse;
  screen: AppScreen;
  onNavigate: (screen: AppScreen) => void;
  onLogout: () => void;
  children: ReactNode;
}


export function ShellLayout({
  user,
  modules,
  license,
  screen,
  onNavigate,
  onLogout,
  children,
}: ShellLayoutProps) {
  const { t } = useI18n();
  const { sites, activeSiteId, setActiveSiteId, loading: sitesLoading } = useSiteContext();
  const navGroups = buildNavGroups(modules, user.permissions, user.roleCode);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => { setMobileOpen(false); }, [screen]);

  const initials = user.fullName
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
    .toUpperCase();

  const crumbs =
    screen === 'dashboard'
      ? [{ label: t('dashboard') }]
      : [
          { label: t('dashboard'), screen: 'dashboard' as AppScreen },
          { label: screenTitle(screen) },
        ];

  function renderSidebar(className: string) {
    return (
      <nav className={className} aria-label="Navigation principale">
        {navGroups.map((group) =>
          group.items.map((item) => (
            <button
              key={item.key}
              type="button"
              className={`db-nav-item ${item.key === 'dashboard' ? 'db-nav-item--dashboard' : ''} ${screen === item.key ? 'db-nav-active' : ''}`}
              onClick={() => onNavigate(item.key)}
            >
              {item.key === 'dashboard'
                ? <span className="db-nav-icon-wrap"><IconDashboard size={17} /></span>
                : <span className="db-nav-active-rail" aria-hidden="true" />
              }
              {item.label}
            </button>
          ))
        )}

        <div className="db-sidebar-spacer" />

        <div className="db-nav-footer">
          <div className={`db-lic-mini ${license.evaluation.valid ? 'db-lic-mini--valid' : 'db-lic-mini--invalid'}`}>
            <IconLicense size={15} />
            <div>
              <span className="db-lic-mini-label">{t('license')}</span>
              <strong>{license.evaluation.valid ? t('licenseValid') : t('licenseInvalid')}</strong>
            </div>
          </div>
          <button className="db-nav-item db-nav-logout" type="button" onClick={onLogout}>
            <span className="db-nav-icon-wrap"><IconLogout size={17} /></span>
            {t('logout')}
          </button>
        </div>
      </nav>
    );
  }

  return (
    <div className="db-shell">
      <header className="db-topbar">
        <div className="db-topbar-start">
          <button
            type="button"
            className="db-menu-btn"
            aria-label="Ouvrir le menu"
            aria-expanded={mobileOpen}
            onClick={() => setMobileOpen(true)}
          >
            <IconMenu size={20} />
          </button>
          <div className="db-brand">
            <div className="brand-mark-sm">R</div>
            <div>
              <span className="db-brand-name">Raqmi System</span>
              <span className="db-tenant">{user.tenant.name}</span>
            </div>
          </div>
        </div>

        <div className="db-topbar-actions">
          {sites.length > 0 && (
            <label className="ux-site-selector">
              <span className="ux-site-selector-label">Site</span>
              <select
                className="ux-site-selector-input"
                value={activeSiteId ?? ''}
                disabled={sitesLoading}
                onChange={(e) => setActiveSiteId(e.target.value || null)}
              >
                {user.roleCode === 'admin' && <option value="">Tous les sites</option>}
                {sites.filter((s) => s.active).map((site) => (
                  <option key={site.id} value={site.id}>{site.name}</option>
                ))}
              </select>
            </label>
          )}
          <LanguageSwitcher />
          <ThemeToggle />
          <div className="db-user-menu">
            <span className="db-user-chip">{user.fullName}</span>
            <button className="db-avatar" title={user.fullName} type="button" aria-label={user.fullName}>
              {initials}
            </button>
          </div>
          <button className="db-ghost-btn db-logout-desktop" type="button" onClick={onLogout}>
            {t('logout')}
          </button>
        </div>
      </header>

      <div className="db-body">
        {renderSidebar('db-sidebar db-sidebar--desktop')}

        {mobileOpen && (
          <>
            <button
              type="button"
              className="db-sidebar-overlay"
              aria-label="Fermer le menu"
              onClick={() => setMobileOpen(false)}
            />
            <div className="db-sidebar-drawer">
              <div className="db-drawer-head">
                <strong>Navigation</strong>
                <button type="button" className="db-drawer-close" onClick={() => setMobileOpen(false)} aria-label="Fermer">
                  <IconClose size={18} />
                </button>
              </div>
              {renderSidebar('db-sidebar db-sidebar--drawer')}
            </div>
          </>
        )}

        <main className="db-main ux-page-enter">
          {screen !== 'dashboard' && (
            <PageHeader
              title={screenTitle(screen)}
              subtitle={`${user.tenant.name} · ${user.fullName}`}
              crumbs={crumbs}
              onNavigate={onNavigate}
            />
          )}
          {children}
        </main>
      </div>

      <nav className="db-mobile-bar" aria-label="Navigation mobile">
        <button
          type="button"
          className={`db-mobile-bar-item ${screen === 'dashboard' ? 'db-mobile-bar-active' : ''}`}
          onClick={() => onNavigate('dashboard')}
        >
          <IconDashboard size={20} />
          <span>{t('dashboard')}</span>
        </button>
        <button type="button" className="db-mobile-bar-item" onClick={() => setMobileOpen(true)}>
          <IconMenu size={20} />
          <span>Modules</span>
        </button>
        <button type="button" className="db-mobile-bar-item db-mobile-bar-logout" onClick={onLogout}>
          <IconLogout size={20} />
          <span>{t('logout')}</span>
        </button>
      </nav>
    </div>
  );
}
