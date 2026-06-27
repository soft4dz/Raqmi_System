import { createMiddleware } from 'hono/factory';
import type { RaqmiModuleCode } from '@raqmi/shared';
import { evaluateLicense } from '@raqmi/licensing';
import { loadLicenseFromDisk, getLastOnlineCheckAt } from '../services/license-store.js';
import { env } from '../env.js';
import { DEMO_LICENSE, DEMO_USAGE } from '../demo-data.js';

export async function getActiveLicensePayload() {
  if (env.DEMO_MODE) return DEMO_LICENSE;
  return loadLicenseFromDisk();
}

export async function evaluateActiveLicense(requestedModule?: RaqmiModuleCode) {
  const license = await getActiveLicensePayload();
  if (!license) {
    return {
      valid: false,
      readonlyMode: true,
      reason: 'Aucune licence importée',
      allowedModules: [] as RaqmiModuleCode[],
      blockedModules: [] as RaqmiModuleCode[],
    };
  }

  return evaluateLicense(license, {
    now: new Date(),
    usersCount: env.DEMO_MODE ? DEMO_USAGE.usersCount : 0,
    sitesCount: env.DEMO_MODE ? DEMO_USAGE.sitesCount : 0,
    storageUsedGb: env.DEMO_MODE ? DEMO_USAGE.storageUsedGb : 0,
    requestedModule,
    lastOnlineCheckAt: getLastOnlineCheckAt(),
  });
}

export const requireModule = (moduleCode: RaqmiModuleCode) =>
  createMiddleware(async (c, next) => {
    const evaluation = await evaluateActiveLicense(moduleCode);
    if (!evaluation.valid) {
      return c.json({ error: evaluation.reason ?? 'Licence invalide' }, 403);
    }
    await next();
  });
