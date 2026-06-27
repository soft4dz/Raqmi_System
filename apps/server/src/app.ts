import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import { healthRouter } from './routes/health.routes';
import { modulesRouter } from './routes/modules.routes';
import { tenantsRouter } from './routes/tenants.routes';
import { licensesRouter } from './routes/licenses.routes';
import { adminDashboardRouter } from './routes/admin-dashboard.routes';
import { adminUsersRouter } from './routes/admin-users.routes';
import { adminRolesRouter, adminPermissionsRouter } from './routes/admin-roles.routes';
import { adminSitesRouter } from './routes/admin-sites.routes';
import { adminSettingsRouter } from './routes/admin-settings.routes';
import { adminAuditRouter } from './routes/admin-audit.routes';
import { adminBackupsRouter } from './routes/admin-backups.routes';
import { coreSystemHealthRouter } from './routes/core-system-health.routes';
import { coreLicenseStatusRouter } from './routes/core-license-status.routes';

export function createApp() {
  const app = express();

  app.use(helmet());
  app.use(cors());
  app.use(express.json({ limit: '2mb' }));

  app.get('/', (_request, response) => {
    response.json({ service: 'Raqmi System Server', status: 'running' });
  });

  app.use('/api/health', healthRouter);
  app.use('/api/modules', modulesRouter);
  app.use('/api/tenants', tenantsRouter);
  app.use('/api/licenses', licensesRouter);

  app.use('/api/core/system-health', coreSystemHealthRouter);
  app.use('/api/core/license-status', coreLicenseStatusRouter);

  app.use('/api/admin/dashboard', adminDashboardRouter);
  app.use('/api/admin/users', adminUsersRouter);
  app.use('/api/admin/roles', adminRolesRouter);
  app.use('/api/admin/permissions', adminPermissionsRouter);
  app.use('/api/admin/sites', adminSitesRouter);
  app.use('/api/admin/settings', adminSettingsRouter);
  app.use('/api/admin/audit-logs', adminAuditRouter);
  app.use('/api/admin/backups', adminBackupsRouter);

  app.use((_request, response) => {
    response.status(404).json({ error: 'Route introuvable.' });
  });

  return app;
}
