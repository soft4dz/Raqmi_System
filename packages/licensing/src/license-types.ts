import type { RaqmiModuleCode } from '@raqmi/shared';

export type LicenseKind = 'trial' | 'starter' | 'professional' | 'enterprise' | 'custom';
export type LicenseMode = 'online' | 'offline' | 'hybrid';
export type LicenseStatus = 'draft' | 'active' | 'suspended' | 'expired' | 'revoked';

export interface LicenseLimits {
  maxUsers: number;
  maxSites: number;
  maxStorageGb: number;
  offlineGraceDays: number;
}

export interface RaqmiLicensePayload {
  licenseId: string;
  tenantId: string;
  tenantName: string;
  kind: LicenseKind;
  mode: LicenseMode;
  status: LicenseStatus;
  issuedAt: string;
  startsAt: string;
  expiresAt: string;
  allowedModules: RaqmiModuleCode[];
  limits: LicenseLimits;
  serverFingerprint?: string;
  signature?: string;
}

export interface LicenseEvaluationContext {
  now: Date;
  usersCount: number;
  sitesCount: number;
  storageUsedGb: number;
  requestedModule?: RaqmiModuleCode;
  lastOnlineCheckAt?: Date | null;
}

export interface LicenseEvaluationResult {
  valid: boolean;
  readonlyMode: boolean;
  reason?: string;
  allowedModules: RaqmiModuleCode[];
  blockedModules: RaqmiModuleCode[];
}
