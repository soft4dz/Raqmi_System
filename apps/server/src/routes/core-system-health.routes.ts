import { Router } from 'express';
import { backups, licenses, settings } from '../data/in-memory-store';

export const coreSystemHealthRouter = Router();

coreSystemHealthRouter.get('/', (_request, response) => {
  const activeLicense = licenses.find((item) => item.status === 'active') ?? null;
  const lastBackup = backups[0] ?? null;
  const storageDriver = settings.find((item) => item.key === 'storage.driver')?.value ?? 'local';

  response.json({
    data: {
      status: 'ok',
      serverTime: new Date().toISOString(),
      database: 'in-memory-dev-store',
      storageDriver,
      license: activeLicense
        ? {
            id: activeLicense.id,
            status: activeLicense.status,
            expiresAt: activeLicense.expiresAt,
            lastOnlineCheckAt: activeLicense.lastOnlineCheckAt,
          }
        : null,
      lastBackup,
    },
  });
});
