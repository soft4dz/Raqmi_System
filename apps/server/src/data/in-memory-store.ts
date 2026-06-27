export type Status = 'active' | 'inactive' | 'suspended' | 'archived';

export interface TenantRecord {
  id: string;
  code: string;
  name: string;
  status: Status;
  createdAt: string;
}

export interface SiteRecord {
  id: string;
  tenantId: string;
  code: string;
  name: string;
  type: 'head_office' | 'hotel' | 'port' | 'residence' | 'agency' | 'other';
  city?: string;
  active: boolean;
}

export interface UserRecord {
  id: string;
  tenantId: string;
  fullName: string;
  email: string;
  roleCodes: string[];
  siteIds: string[];
  active: boolean;
  forceChangeSecret: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface RoleRecord {
  code: string;
  label: string;
  system: boolean;
  description: string;
  permissions: string[];
}

export interface PermissionRecord {
  code: string;
  moduleCode: string;
  action: 'read' | 'create' | 'update' | 'delete' | 'validate' | 'export' | 'manage';
  label: string;
}

export interface LicenseRecord {
  id: string;
  tenantId: string;
  tenantName: string;
  kind: 'trial' | 'starter' | 'professional' | 'enterprise' | 'custom';
  mode: 'online' | 'offline' | 'hybrid';
  status: 'draft' | 'active' | 'suspended' | 'expired' | 'revoked';
  startsAt: string;
  expiresAt: string;
  allowedModules: string[];
  limits: {
    maxUsers: number;
    maxSites: number;
    maxStorageGb: number;
    offlineGraceDays: number;
  };
  lastOnlineCheckAt?: string;
  createdAt: string;
}

export interface SettingRecord {
  tenantId: string;
  key: string;
  value: string;
  category: 'company' | 'security' | 'numbering' | 'documents' | 'server' | 'storage' | 'backup' | 'appearance';
}

export interface AuditRecord {
  id: string;
  tenantId?: string;
  userId?: string;
  moduleCode?: string;
  action: string;
  entityType?: string;
  entityId?: string;
  description: string;
  createdAt: string;
}

export interface BackupRecord {
  id: string;
  tenantId: string;
  type: 'manual' | 'scheduled';
  status: 'pending' | 'running' | 'success' | 'failed';
  storage: 'local' | 'cloud' | 'hybrid';
  startedAt: string;
  finishedAt?: string;
  sizeMb?: number;
  note?: string;
}

export const tenants: TenantRecord[] = [
  {
    id: 'tenant-demo-hotel',
    code: 'DEMO-HOTEL',
    name: 'Client Démo Hôtel',
    status: 'active',
    createdAt: new Date().toISOString(),
  },
];

export const sites: SiteRecord[] = [
  {
    id: 'site-demo-main',
    tenantId: 'tenant-demo-hotel',
    code: 'MAIN',
    name: 'Site principal',
    type: 'hotel',
    city: 'Alger',
    active: true,
  },
];

export const permissions: PermissionRecord[] = [
  { code: 'admin.dashboard.read', moduleCode: 'administration', action: 'read', label: 'Consulter tableau de bord administration' },
  { code: 'admin.users.manage', moduleCode: 'administration', action: 'manage', label: 'Gérer les utilisateurs' },
  { code: 'admin.roles.manage', moduleCode: 'administration', action: 'manage', label: 'Gérer les rôles' },
  { code: 'admin.permissions.read', moduleCode: 'administration', action: 'read', label: 'Consulter les permissions' },
  { code: 'admin.sites.manage', moduleCode: 'sites', action: 'manage', label: 'Gérer les sites' },
  { code: 'admin.settings.manage', moduleCode: 'settings', action: 'manage', label: 'Gérer les paramètres' },
  { code: 'core.audit.read', moduleCode: 'administration', action: 'read', label: 'Consulter le journal audit' },
  { code: 'core.backup.manage', moduleCode: 'administration', action: 'manage', label: 'Gérer les sauvegardes' },
  { code: 'core.license.read', moduleCode: 'administration', action: 'read', label: 'Consulter la licence' },
  { code: 'core.system.read', moduleCode: 'administration', action: 'read', label: 'Consulter santé système' },
];

export const roles: RoleRecord[] = [
  {
    code: 'TENANT_ADMIN',
    label: 'Administrateur client',
    system: true,
    description: 'Administre les utilisateurs, paramètres et sites du client.',
    permissions: permissions.map((permission) => permission.code),
  },
  {
    code: 'DIRECTION_GENERALE',
    label: 'Direction générale',
    system: true,
    description: 'Consulte le pilotage et les rapports.',
    permissions: ['admin.dashboard.read', 'core.license.read', 'core.system.read', 'core.audit.read'],
  },
  {
    code: 'LECTURE_SEULE',
    label: 'Lecture seule',
    system: true,
    description: 'Accès consultation uniquement.',
    permissions: ['admin.dashboard.read', 'core.license.read'],
  },
];

export const users: UserRecord[] = [
  {
    id: 'user-demo-admin',
    tenantId: 'tenant-demo-hotel',
    fullName: 'Administrateur Démo',
    email: 'admin@demo.local',
    roleCodes: ['TENANT_ADMIN'],
    siteIds: ['site-demo-main'],
    active: true,
    forceChangeSecret: false,
    createdAt: new Date().toISOString(),
  },
];

export const licenses: LicenseRecord[] = [
  {
    id: 'lic-demo-professional',
    tenantId: 'tenant-demo-hotel',
    tenantName: 'Client Démo Hôtel',
    kind: 'professional',
    mode: 'hybrid',
    status: 'active',
    startsAt: new Date().toISOString(),
    expiresAt: new Date(Date.now() + 365 * 86_400_000).toISOString(),
    allowedModules: ['sites', 'daily_revenue', 'treasury', 'billing', 'receivables', 'contracts', 'hr', 'stocks', 'purchases', 'ged', 'reports', 'dashboards'],
    limits: {
      maxUsers: 50,
      maxSites: 5,
      maxStorageGb: 100,
      offlineGraceDays: 30,
    },
    lastOnlineCheckAt: new Date().toISOString(),
    createdAt: new Date().toISOString(),
  },
];

export const settings: SettingRecord[] = [
  { tenantId: 'tenant-demo-hotel', category: 'company', key: 'company.name', value: 'Client Démo Hôtel' },
  { tenantId: 'tenant-demo-hotel', category: 'company', key: 'company.currency', value: 'DZD' },
  { tenantId: 'tenant-demo-hotel', category: 'security', key: 'security.sessionMinutes', value: '60' },
  { tenantId: 'tenant-demo-hotel', category: 'storage', key: 'storage.driver', value: 'local' },
  { tenantId: 'tenant-demo-hotel', category: 'backup', key: 'backup.frequency', value: 'daily' },
];

export const auditLogs: AuditRecord[] = [
  {
    id: 'audit-demo-001',
    tenantId: 'tenant-demo-hotel',
    userId: 'user-demo-admin',
    moduleCode: 'administration',
    action: 'system.bootstrap',
    entityType: 'system',
    entityId: 'core-admin',
    description: 'Initialisation du socle Core et Administration.',
    createdAt: new Date().toISOString(),
  },
];

export const backups: BackupRecord[] = [
  {
    id: 'backup-demo-001',
    tenantId: 'tenant-demo-hotel',
    type: 'scheduled',
    status: 'success',
    storage: 'local',
    startedAt: new Date(Date.now() - 86_400_000).toISOString(),
    finishedAt: new Date(Date.now() - 86_400_000 + 60_000).toISOString(),
    sizeMb: 128,
    note: 'Sauvegarde démo quotidienne.',
  },
];

export function addAuditLog(log: Omit<AuditRecord, 'id' | 'createdAt'>): AuditRecord {
  const item: AuditRecord = {
    id: `audit-${Date.now()}`,
    createdAt: new Date().toISOString(),
    ...log,
  };
  auditLogs.unshift(item);
  return item;
}
