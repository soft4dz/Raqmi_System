import { Router } from 'express';
import { licenses } from '../data/in-memory-store';

export const coreLicenseStatusRouter = Router();

coreLicenseStatusRouter.get('/', (request, response) => {
  const tenantId = String(request.query.tenantId ?? 'tenant-demo-hotel');
  const license = licenses.find((item) => item.tenantId === tenantId && item.status === 'active') ?? null;

  if (!license) {
    response.status(404).json({ error: 'Aucune licence active.' });
    return;
  }

  response.json({
    data: {
      id: license.id,
      tenantId: license.tenantId,
      kind: license.kind,
      mode: license.mode,
      status: license.status,
      startsAt: license.startsAt,
      expiresAt: license.expiresAt,
      allowedModules: license.allowedModules,
      limits: license.limits,
      lastOnlineCheckAt: license.lastOnlineCheckAt,
    },
  });
});
