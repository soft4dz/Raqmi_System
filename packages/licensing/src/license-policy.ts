import { RAQMI_MODULES, type RaqmiModuleCode } from '@raqmi/shared';
import type { LicenseEvaluationContext, LicenseEvaluationResult, RaqmiLicensePayload } from './license-types';

function daysBetween(a: Date, b: Date): number {
  return Math.floor((a.getTime() - b.getTime()) / 86_400_000);
}

export function evaluateLicense(
  license: RaqmiLicensePayload,
  context: LicenseEvaluationContext,
): LicenseEvaluationResult {
  const now = context.now;
  const startsAt = new Date(license.startsAt);
  const expiresAt = new Date(license.expiresAt);
  const allCommercialModules = RAQMI_MODULES.filter((module) => module.commercial).map((module) => module.code);
  const blockedModules = allCommercialModules.filter(
    (code) => !license.allowedModules.includes(code as RaqmiModuleCode),
  ) as RaqmiModuleCode[];

  if (license.status !== 'active') {
    return {
      valid: false,
      readonlyMode: true,
      reason: `Licence ${license.status}`,
      allowedModules: [],
      blockedModules: allCommercialModules as RaqmiModuleCode[],
    };
  }

  if (now < startsAt) {
    return {
      valid: false,
      readonlyMode: true,
      reason: 'Licence pas encore active',
      allowedModules: [],
      blockedModules: allCommercialModules as RaqmiModuleCode[],
    };
  }

  if (now > expiresAt) {
    return {
      valid: false,
      readonlyMode: true,
      reason: 'Licence expirée',
      allowedModules: license.allowedModules,
      blockedModules,
    };
  }

  if (context.usersCount > license.limits.maxUsers) {
    return {
      valid: false,
      readonlyMode: true,
      reason: 'Nombre maximum d’utilisateurs dépassé',
      allowedModules: license.allowedModules,
      blockedModules,
    };
  }

  if (context.sitesCount > license.limits.maxSites) {
    return {
      valid: false,
      readonlyMode: true,
      reason: 'Nombre maximum de sites dépassé',
      allowedModules: license.allowedModules,
      blockedModules,
    };
  }

  if (context.storageUsedGb > license.limits.maxStorageGb) {
    return {
      valid: false,
      readonlyMode: true,
      reason: 'Quota de stockage dépassé',
      allowedModules: license.allowedModules,
      blockedModules,
    };
  }

  if (license.mode !== 'offline' && context.lastOnlineCheckAt) {
    const offlineDays = daysBetween(now, context.lastOnlineCheckAt);
    if (offlineDays > license.limits.offlineGraceDays) {
      return {
        valid: false,
        readonlyMode: true,
        reason: 'Délai de tolérance hors ligne dépassé',
        allowedModules: license.allowedModules,
        blockedModules,
      };
    }
  }

  if (context.requestedModule && !license.allowedModules.includes(context.requestedModule)) {
    return {
      valid: false,
      readonlyMode: false,
      reason: `Module non inclus dans la licence: ${context.requestedModule}`,
      allowedModules: license.allowedModules,
      blockedModules,
    };
  }

  return {
    valid: true,
    readonlyMode: false,
    allowedModules: license.allowedModules,
    blockedModules,
  };
}
