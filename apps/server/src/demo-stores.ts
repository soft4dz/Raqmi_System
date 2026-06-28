import { DEMO_TENANT, DEMO_USER } from './demo-data.js';

export type DemoSite = {
  id: string;
  code: string;
  name: string;
  type: string;
  city?: string;
  active: boolean;
};

export type DemoUser = {
  id: string;
  email: string;
  fullName: string;
  roleCode: string;
  active: boolean;
  password: string;
  siteIds: string[];
};

export type DemoRole = {
  code: string;
  label: string;
  isSystem: boolean;
  permissions: string[];
};

export type AuditLog = {
  id: string;
  userId?: string;
  action: string;
  moduleCode?: string;
  entityType?: string;
  entityId?: string;
  description: string;
  createdAt: string;
};

export const PERMISSION_CATALOG = [
  { key: 'administration:read',  label: 'Administration — lecture',   module: 'administration' },
  { key: 'administration:write', label: 'Administration — écriture',  module: 'administration' },
  { key: 'sites:read',           label: 'Sites — lecture',            module: 'sites' },
  { key: 'sites:write',          label: 'Sites — écriture',           module: 'sites' },
  { key: 'settings:read',        label: 'Paramètres — lecture',       module: 'settings' },
  { key: 'settings:write',       label: 'Paramètres — écriture',      module: 'settings' },
  // Chaque module métier ajoutera ses permissions ici lors de son développement
] as const;

export const MAX_DEMO_SITES = 5;

export const demoSites: DemoSite[] = [
  { id: 'demo-site-001', code: 'main', name: 'Hotel Demo — Siège', type: 'hotel', city: 'Alger', active: true },
  { id: 'demo-site-002', code: 'annexe', name: 'Annexe Plage', type: 'annexe', city: 'Tipaza', active: true },
];

export const demoRoles: DemoRole[] = [
  {
    code: 'admin',
    label: 'Administrateur',
    isSystem: true,
    permissions: ['*'],
  },
  {
    code: 'manager',
    label: 'Responsable',
    isSystem: true,
    permissions: [
      'administration:read',
      'sites:read',
      'sites:write',
      'settings:read',
    ],
  },
  {
    code: 'user',
    label: 'Utilisateur',
    isSystem: true,
    permissions: [
      'sites:read',
    ],
  },
];

export const demoUsers: DemoUser[] = [
  {
    id: DEMO_USER.id,
    email: DEMO_USER.email,
    fullName: DEMO_USER.fullName,
    roleCode: DEMO_USER.roleCode,
    active: true,
    password: DEMO_USER.password,
    siteIds: ['demo-site-001', 'demo-site-002'],
  },
  {
    id: 'demo-user-002',
    email: 'manager@demo.raqmi.local',
    fullName: 'Responsable Demo',
    roleCode: 'manager',
    active: true,
    password: 'demo1234',
    siteIds: ['demo-site-001', 'demo-site-002'],
  },
  {
    id: 'demo-user-003',
    email: 'user@demo.raqmi.local',
    fullName: 'Utilisateur Demo',
    roleCode: 'user',
    active: true,
    password: 'demo1234',
    siteIds: ['demo-site-002'],
  },
];

export const auditLogs: AuditLog[] = [
  {
    id: 'audit-001',
    userId: DEMO_USER.id,
    action: 'login',
    moduleCode: 'administration',
    entityType: 'User',
    entityId: DEMO_USER.id,
    description: 'Connexion administrateur',
    createdAt: new Date().toISOString(),
  },
];

export function hasPermission(roleCode: string, module: string, action: string): boolean {
  const role = demoRoles.find((r) => r.code === roleCode);
  if (!role) return false;
  if (role.permissions.includes('*')) return true;
  return (
    role.permissions.includes(`${module}:${action}`) ||
    role.permissions.includes(`${module}:*`)
  );
}

export function permissionsForRole(roleCode: string): string[] {
  return demoRoles.find((r) => r.code === roleCode)?.permissions ?? [];
}

export function isAdmin(roleCode: string): boolean {
  return roleCode === 'admin';
}

export function findDemoUserByEmail(email: string): DemoUser | undefined {
  return demoUsers.find((u) => u.email === email.toLowerCase());
}

export function getUserSiteIds(userId: string): string[] {
  return demoUsers.find((u) => u.id === userId)?.siteIds ?? [];
}

export function normalizeSiteIds(siteIds?: string[]): string[] {
  if (!siteIds?.length) return ['demo-site-001'];
  const valid = siteIds.filter((id) => demoSites.some((s) => s.id === id));
  return valid.length ? valid : ['demo-site-001'];
}

export function pushAudit(
  action: string,
  moduleCode: string,
  entityType: string,
  entityId: string,
  description: string,
  userId?: string,
) {
  auditLogs.unshift({
    id: crypto.randomUUID(),
    userId: userId ?? DEMO_USER.id,
    action,
    moduleCode,
    entityType,
    entityId,
    description,
    createdAt: new Date().toISOString(),
  });
  if (auditLogs.length > 500) auditLogs.pop();
}

export function userSharesSites(user: DemoUser, siteIds: string[]): boolean {
  return user.siteIds.some((id) => siteIds.includes(id));
}

export function toPublicUser(user: DemoUser) {
  return {
    id: user.id,
    email: user.email,
    fullName: user.fullName,
    roleCode: user.roleCode,
    active: user.active,
    siteIds: user.siteIds,
  };
}

export const DEMO_TENANT_ID = DEMO_TENANT.id;
