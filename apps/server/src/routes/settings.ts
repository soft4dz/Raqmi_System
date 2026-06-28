import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth.js';
import { requireModule } from '../middleware/require-module.js';
import { hasPermission, isAdmin, pushAudit } from '../demo-stores.js';
import { defaultTenantSettings, type TenantSettings } from '../tenant-settings-defaults.js';

let settings: TenantSettings = defaultTenantSettings();

export const settingsRoutes = new Hono();

settingsRoutes.use('*', authMiddleware);
settingsRoutes.use('*', requireModule('settings'));

settingsRoutes.get('/', (c) => c.json(settings));

settingsRoutes.patch('/', async (c) => {
  const roleCode = c.get('roleCode');
  if (!isAdmin(roleCode) && !hasPermission(roleCode, 'settings', 'write')) {
    return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  }
  const body = await c.req.json<Partial<TenantSettings>>();
  settings = { ...settings, ...body, updatedAt: new Date().toISOString() };
  pushAudit('update', 'settings', 'TenantSettings', 'demo-tenant-001', 'Paramètres entreprise mis à jour', c.get('userId'));
  return c.json(settings);
});
