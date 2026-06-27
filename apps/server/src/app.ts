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
import { v1AuthRouter } from './routes/v1-auth.routes';
import { v1CoreRouter } from './routes/v1-core.routes';
import { v1AdminRouter } from './routes/v1-admin.routes';
import { v1SettingsRouter, v1SitesRouter } from './routes/v1-sites-settings.routes';

export function createApp() {
  const app = express();
  app.use(helmet());
  app.use(cors());
  app.use(express.json({ limit: '2mb' }));

  app.get('/', (_request, response) => response.json({ service: 'Raqmi System Server', status: 'running' }));

  app.use('/health', healthRouter);
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

  app.use('/api/v1/auth', v1AuthRouter);
  app.use('/api/v1', v1CoreRouter);
  app.use('/api/v1/admin', v1AdminRouter);
  app.use('/api/v1/sites', v1SitesRouter);
  app.use('/api/v1/settings', v1SettingsRouter);

  app.use((_request, response) => response.status(404).json({ error: 'Route introuvable.' }));
  return app;
}
