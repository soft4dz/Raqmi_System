import { IconChevron } from './icons';
import type { AppScreen } from '../navigation/moduleRoutes';

interface Crumb {
  label: string;
  screen?: AppScreen;
}

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  crumbs: Crumb[];
  onNavigate?: (screen: AppScreen) => void;
  actions?: React.ReactNode;
}

export function PageHeader({ title, subtitle, crumbs, onNavigate, actions }: PageHeaderProps) {
  return (
    <header className="ux-page-header">
      <nav className="ux-breadcrumb" aria-label="Fil d'Ariane">
        {crumbs.map((crumb, index) => {
          const isLast = index === crumbs.length - 1;
          return (
            <span key={`${crumb.label}-${index}`} className="ux-breadcrumb-item">
              {index > 0 && <IconChevron size={14} className="ux-breadcrumb-sep" />}
              {crumb.screen && !isLast && onNavigate ? (
                <button type="button" className="ux-breadcrumb-link" onClick={() => onNavigate(crumb.screen!)}>
                  {crumb.label}
                </button>
              ) : (
                <span className={isLast ? 'ux-breadcrumb-current' : 'ux-breadcrumb-muted'}>{crumb.label}</span>
              )}
            </span>
          );
        })}
      </nav>
      <div className="ux-page-header-row">
        <div>
          <h1 className="ux-page-title">{title}</h1>
          {subtitle && <p className="ux-page-subtitle">{subtitle}</p>}
        </div>
        {actions && <div className="ux-page-actions">{actions}</div>}
      </div>
    </header>
  );
}
