import type { ModuleItem } from '../api';

export type AppScreen =
  | 'dashboard'
  | 'admin_users'
  | 'admin_roles'
  | 'admin_audit'
  | 'core_sites'
  | 'core_settings'
  | 'daily_revenue'
  | 'billing'
  | 'treasury'
  | 'hr_employees'
  | 'hr_contracts'
  | 'hr_attendance'
  | 'stocks_products'
  | 'stocks_movements'
  | 'stocks_inventory'
  | 'ged';

export type NavFamily = 'core' | 'finance' | 'hr' | 'operations' | 'system';

export interface NavItem {
  key: AppScreen;
  label: string;
  family: NavFamily;
}

export interface NavGroup {
  family: NavFamily;
  items: NavItem[];
}

const MODULE_TO_SCREEN: Record<string, AppScreen> = {
  administration: 'admin_users',
  settings: 'core_settings',
  sites: 'core_sites',
  daily_revenue: 'daily_revenue',
  billing: 'billing',
  treasury: 'treasury',
  hr: 'hr_employees',
  stocks: 'stocks_products',
  ged: 'ged',
};

export function screenForModule(code: string): AppScreen | null {
  return MODULE_TO_SCREEN[code] ?? null;
}

export function buildNavGroups(modules: ModuleItem[]): NavGroup[] {
  const enabled = new Set(modules.filter((m) => m.enabled).map((m) => m.code));
  const groups: NavGroup[] = [];

  const core: NavItem[] = [{ key: 'dashboard', label: 'Dashboard', family: 'core' }];
  if (enabled.has('sites')) core.push({ key: 'core_sites', label: 'Sites / unités', family: 'core' });
  if (enabled.has('settings')) core.push({ key: 'core_settings', label: 'Paramétrage global', family: 'core' });
  if (enabled.has('administration')) {
    core.push({ key: 'admin_users', label: 'Utilisateurs', family: 'core' });
    core.push({ key: 'admin_roles', label: 'Rôles & permissions', family: 'core' });
    core.push({ key: 'admin_audit', label: 'Journal d\'audit', family: 'core' });
  }
  if (core.length > 0) groups.push({ family: 'core', items: core });

  const finance: NavItem[] = [];
  if (enabled.has('daily_revenue')) finance.push({ key: 'daily_revenue', label: 'Recettes journalières', family: 'finance' });
  if (enabled.has('billing')) finance.push({ key: 'billing', label: 'Facturation', family: 'finance' });
  if (enabled.has('treasury')) finance.push({ key: 'treasury', label: 'Trésorerie', family: 'finance' });
  if (finance.length > 0) groups.push({ family: 'finance', items: finance });

  if (enabled.has('hr')) {
    groups.push({
      family: 'hr',
      items: [
        { key: 'hr_employees', label: 'Employés', family: 'hr' },
        { key: 'hr_contracts', label: 'Contrats', family: 'hr' },
        { key: 'hr_attendance', label: 'Pointage', family: 'hr' },
      ],
    });
  }

  if (enabled.has('stocks')) {
    groups.push({
      family: 'operations',
      items: [
        { key: 'stocks_products', label: 'Produits', family: 'operations' },
        { key: 'stocks_movements', label: 'Mouvements stocks', family: 'operations' },
        { key: 'stocks_inventory', label: 'Inventaire', family: 'operations' },
      ],
    });
  }

  if (enabled.has('ged')) {
    groups.push({
      family: 'system',
      items: [{ key: 'ged', label: 'Documents', family: 'system' }],
    });
  }

  return groups;
}

export function screenTitle(screen: AppScreen): string {
  const titles: Record<AppScreen, string> = {
    dashboard: 'Dashboard',
    admin_users: 'Utilisateurs',
    admin_roles: 'Rôles & permissions',
    admin_audit: 'Journal d\'audit',
    core_sites: 'Sites / unités',
    core_settings: 'Paramétrage global',
    daily_revenue: 'Recettes journalières',
    billing: 'Facturation',
    treasury: 'Trésorerie',
    hr_employees: 'Employés',
    hr_contracts: 'Contrats',
    hr_attendance: 'Pointage',
    stocks_products: 'Produits',
    stocks_movements: 'Mouvements stocks',
    stocks_inventory: 'Inventaire',
    ged: 'Documents',
  };
  return titles[screen];
}

export function screenFamily(screen: AppScreen): NavFamily | null {
  if (screen === 'dashboard' || screen.startsWith('admin_') || screen.startsWith('core_')) return 'core';
  if (screen === 'daily_revenue' || screen === 'billing' || screen === 'treasury') return 'finance';
  if (screen.startsWith('hr_')) return 'hr';
  if (screen.startsWith('stocks_')) return 'operations';
  if (screen === 'ged') return 'system';
  return null;
}
