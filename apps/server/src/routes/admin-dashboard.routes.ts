import { Router } from 'express';
import { auditLogs, backups, licenses, roles, sites, users } from '../data/in-memory-store';

export const adminDashboardRouter = Router();

adminDashboardRouter.get('/', (_request, response) => {
  const activeLicense = licenses.find((item) => item.status === 'active');
  const lastBackup = backups[0] ?? null;

  response.json({
    data: {
      users: {
        total: users.length,
        active: users.filter((item) => item.active).length,
        inactive: users.filter((item) => !item.active).length,
      },
      roles: roles.length,
      sites: sites.length,
      license: activeLicense
        ? {
            id: activeLicense.id,
            kind: activeLicense.kind,
            mode: activeLicense.mode,
            expiresAt: activeLicense.expiresAt,
            modules: activeLicense.allowedModules.length,
          }
        : null,
      lastBackup,
      recentAudit: auditLogs.slice(0, 5),
    },
  });
});
