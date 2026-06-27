import { RAQMI_LICENSE_PACKS } from '@raqmi/licensing/packs';
import type { RaqmiLicensePayload } from '@raqmi/licensing/types';
import { RAQMI_MODULES } from '@raqmi/shared';
import bcrypt from 'bcryptjs';

export const DEMO_TENANT = {
  id: 'demo-tenant-001',
  code: 'demo-hotel',
  name: 'Hotel Demo Raqmi',
};

export const DEMO_USER = {
  id: 'demo-user-001',
  tenantId: DEMO_TENANT.id,
  email: 'admin@demo.raqmi.local',
  fullName: 'Administrateur Demo',
  roleCode: 'admin',
  password: 'demo1234',
};

const professionalPack = RAQMI_LICENSE_PACKS.find((p) => p.kind === 'professional')!;

export const DEMO_LICENSE: RaqmiLicensePayload = {
  licenseId: 'demo-license-001',
  tenantId: DEMO_TENANT.id,
  tenantName: DEMO_TENANT.name,
  kind: 'professional',
  mode: 'offline',
  status: 'active',
  issuedAt: '2026-01-01T00:00:00Z',
  startsAt: '2026-01-01T00:00:00Z',
  expiresAt: '2027-12-31T23:59:59Z',
  allowedModules: professionalPack.modules,
  limits: professionalPack.defaultLimits,
};

export const DEMO_USAGE = {
  usersCount: 3,
  sitesCount: 1,
  storageUsedGb: 12,
};

export const DEMO_PASSWORD_HASH = bcrypt.hashSync(DEMO_USER.password, 10);

export function getDemoModules() {
  return RAQMI_MODULES.map((module) => ({
    ...module,
    enabled: !module.commercial || DEMO_LICENSE.allowedModules.includes(module.code),
  }));
}
