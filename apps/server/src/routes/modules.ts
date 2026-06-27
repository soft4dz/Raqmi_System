import { RAQMI_MODULES } from '@raqmi/shared';
import { Hono } from 'hono';
import { prisma } from '@raqmi/database';
import { getDemoModules } from '../demo-data.js';
import { env } from '../env.js';
import { authMiddleware } from '../middleware/auth.js';
import { getActiveLicensePayload } from '../services/license-store.js';

export const moduleRoutes = new Hono();

moduleRoutes.use('*', authMiddleware);

moduleRoutes.get('/', async (c) => {
  if (env.DEMO_MODE) {
    return c.json({ modules: getDemoModules() });
  }

  const fileLicense = await getActiveLicensePayload();
  if (fileLicense) {
    const allowed = new Set(fileLicense.allowedModules);
    const modules = RAQMI_MODULES.map((module) => ({
      ...module,
      enabled: !module.commercial || allowed.has(module.code),
    }));
    return c.json({ modules });
  }

  const tenantId = c.get('tenantId');
  const license = await prisma.license.findFirst({
    where: { tenantId, status: 'ACTIVE' },
    include: {
      modules: {
        include: { module: true },
        where: { enabled: true },
      },
    },
  });

  const allowedCodes = new Set(license?.modules.map((entry) => entry.module.code) ?? []);

  const modules = RAQMI_MODULES.map((module) => ({
    ...module,
    enabled: !module.commercial || allowedCodes.has(module.code),
  }));

  return c.json({ modules });
});

moduleRoutes.get('/catalog', (c) => c.json({ modules: RAQMI_MODULES }));
