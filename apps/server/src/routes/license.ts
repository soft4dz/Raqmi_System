import { evaluateLicense } from '@raqmi/licensing';
import { RAQMI_LICENSE_PACKS } from '@raqmi/licensing/packs';
import type { RaqmiLicensePayload } from '@raqmi/licensing/types';
import { Hono } from 'hono';
import { prisma } from '@raqmi/database';
import { DEMO_LICENSE, DEMO_TENANT, DEMO_USAGE } from '../demo-data.js';
import { env } from '../env.js';
import { authMiddleware } from '../middleware/auth.js';
import {
  evaluateActiveLicense,
  getActiveLicensePayload,
} from '../middleware/require-module.js';
import {
  getServerFingerprint,
  importLicenseFile,
  loadLicenseFromDisk,
} from '../services/license-store.js';

export const licenseRoutes = new Hono();

licenseRoutes.get('/fingerprint', (c) =>
  c.json({ fingerprint: getServerFingerprint() }),
);

licenseRoutes.use('/status', authMiddleware);
licenseRoutes.use('/import', authMiddleware);

licenseRoutes.get('/status', async (c) => {
  if (env.DEMO_MODE) {
    const evaluation = evaluateLicense(DEMO_LICENSE, {
      now: new Date(),
      ...DEMO_USAGE,
    });

    return c.json({
      tenant: DEMO_TENANT,
      license: DEMO_LICENSE,
      evaluation,
      pack: RAQMI_LICENSE_PACKS.find((p) => p.kind === DEMO_LICENSE.kind),
      mode: env.LICENSE_MODE,
    });
  }

  const fileLicense = await loadLicenseFromDisk();
  if (fileLicense) {
    const evaluation = await evaluateActiveLicense();
    return c.json({
      tenant: { id: fileLicense.tenantId, code: fileLicense.tenantId, name: fileLicense.tenantName },
      license: fileLicense,
      evaluation,
      pack: RAQMI_LICENSE_PACKS.find((p) => p.kind === fileLicense.kind),
      mode: env.LICENSE_MODE,
      fingerprint: getServerFingerprint(),
    });
  }

  const tenantId = c.get('tenantId');
  const license = await loadTenantLicense(tenantId);
  if (!license) {
    return c.json({ error: 'Aucune licence active' }, 404);
  }

  const [usersCount, sitesCount] = await Promise.all([
    prisma.user.count({ where: { tenantId, active: true } }),
    prisma.site.count({ where: { tenantId, active: true } }),
  ]);

  const evaluation = evaluateLicense(license.payload, {
    now: new Date(),
    usersCount,
    sitesCount,
    storageUsedGb: 0,
  });

  return c.json({
    tenant: license.tenant,
    license: license.payload,
    evaluation,
    pack: RAQMI_LICENSE_PACKS.find((p) => p.kind === license.payload.kind),
    mode: env.LICENSE_MODE,
  });
});

licenseRoutes.post('/import', async (c) => {
  if (c.get('roleCode') !== 'admin') {
    return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  }

  const body = await c.req.json<{ content?: string }>();
  if (!body.content) {
    return c.json({ error: 'Contenu licence requis' }, 400);
  }

  try {
    const payload = await importLicenseFile(body.content);
    const evaluation = await evaluateActiveLicense();
    return c.json({
      message: 'Licence importée',
      license: payload,
      evaluation,
      fingerprint: getServerFingerprint(),
    });
  } catch (error) {
    return c.json(
      { error: error instanceof Error ? error.message : 'Import licence impossible' },
      400,
    );
  }
});

async function loadTenantLicense(tenantId: string) {
  const tenant = await prisma.tenant.findUnique({ where: { id: tenantId } });
  if (!tenant) return null;

  const license = await prisma.license.findFirst({
    where: { tenantId, status: 'ACTIVE' },
    include: {
      modules: {
        include: { module: true },
        where: { enabled: true },
      },
    },
  });

  if (!license) return null;

  const payload: RaqmiLicensePayload = {
    licenseId: license.id,
    tenantId: tenant.id,
    tenantName: tenant.name,
    kind: license.kind.toLowerCase() as RaqmiLicensePayload['kind'],
    mode: license.mode.toLowerCase() as RaqmiLicensePayload['mode'],
    status: license.status.toLowerCase() as RaqmiLicensePayload['status'],
    issuedAt: license.createdAt.toISOString(),
    startsAt: license.startsAt.toISOString(),
    expiresAt: license.expiresAt.toISOString(),
    allowedModules: license.modules.map((entry) => entry.module.code) as RaqmiLicensePayload['allowedModules'],
    limits: {
      maxUsers: license.maxUsers,
      maxSites: license.maxSites,
      maxStorageGb: license.maxStorageGb,
      offlineGraceDays: license.offlineGraceDays,
    },
  };

  return { tenant: { id: tenant.id, code: tenant.code, name: tenant.name }, payload };
}

export { getActiveLicensePayload } from '../services/license-store.js';
