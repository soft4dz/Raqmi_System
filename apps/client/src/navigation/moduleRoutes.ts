import type { ModuleItem } from '../api';
import { canReadAdmin, hasPermission, isAdminRole } from '../lib/permissions';

export type AppScreen =
  | 'dashboard'
  | 'admin_users'
  | 'admin_roles'
  | 'admin_audit'
  | 'core_sites'
  | 'core_settings';

export type NavFamily = 'core';

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
  settings:       'core_settings',
  sites:          'core_sites',
};

export function screenForModule(code: string): AppScreen | null {
  return MODULE_TO_SCREEN[code] ?? null;
}

export function buildNavGroups(
  modules: ModuleItem[],
  permissions?: string[],
  roleCode?: string,
): NavGroup[] {
  const enabled = new Set(modules.filter((m) => m.enabled).map((m) => m.code));
  const perms = permissions ?? [];
  const role = roleCode ?? 'user';

  const items: NavItem[] = [{ key: 'dashboard', label: 'Tableau de bord', family: 'core' }];

  if (enabled.has('sites') && (isAdminRole(role) || hasPermission(perms, 'sites', 'read'))) {
    items.push({ key: 'core_sites', label: 'Sites / unités', family: 'core' });
  }
  if (enabled.has('settings')) {
    items.push({ key: 'core_settings', label: 'Paramétrage global', family: 'core' });
  }
  if (enabled.has('administration') && canReadAdmin(perms, role)) {
    items.push({ key: 'admin_users', label: 'Utilisateurs', family: 'core' });
    items.push({ key: 'admin_audit', label: 'Journal d\'audit', family: 'core' });
  }
  if (enabled.has('administration') && isAdminRole(role)) {
    items.push({ key: 'admin_roles', label: 'Rôles & permissions', family: 'core' });
  }

  return [{ family: 'core', items }];
}

export function screenTitle(screen: AppScreen): string {
  const titles: Record<AppScreen, string> = {
    dashboard:      'Tableau de bord',
    admin_users:    'Utilisateurs',
    admin_roles:    'Rôles & permissions',
    admin_audit:    'Journal d\'audit',
    core_sites:     'Sites / unités',
    core_settings:  'Paramétrage global',
  };
  return titles[screen];
}

export function screenFamily(_screen: AppScreen): NavFamily {
  return 'core';
}
