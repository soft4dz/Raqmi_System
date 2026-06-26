export interface TenantRecord {
  id: string;
  code: string;
  name: string;
  status: 'active' | 'suspended' | 'archived';
  createdAt: string;
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
  createdAt: string;
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
    allowedModules: [
      'sites',
      'daily_revenue',
      'treasury',
      'billing',
      'receivables',
      'contracts',
      'hr',
      'stocks',
      'purchases',
      'ged',
      'reports',
      'dashboards',
    ],
    limits: {
      maxUsers: 50,
      maxSites: 5,
      maxStorageGb: 100,
      offlineGraceDays: 30,
    },
    createdAt: new Date().toISOString(),
  },
];
