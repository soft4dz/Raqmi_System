import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth.js';
import { requireModule } from '../middleware/require-module.js';
import {
  demoSites,
  getUserSiteIds,
  hasPermission,
  isAdmin,
  MAX_DEMO_SITES,
  pushAudit,
} from '../demo-stores.js';

export const siteRoutes = new Hono();

siteRoutes.use('*', authMiddleware);
siteRoutes.use('*', requireModule('sites'));

siteRoutes.get('/', (c) => {
  const roleCode = c.get('roleCode') as string;
  const userId = c.get('userId') as string;
  const items = isAdmin(roleCode)
    ? demoSites
    : demoSites.filter((s) => getUserSiteIds(userId).includes(s.id));
  return c.json({ items });
});

siteRoutes.post('/', async (c) => {
  if (!isAdmin(c.get('roleCode')) && !hasPermission(c.get('roleCode'), 'sites', 'write')) {
    return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  }
  const body = await c.req.json<{ code?: string; name?: string; type?: string; city?: string }>();
  if (demoSites.filter((s) => s.active).length >= MAX_DEMO_SITES) {
    return c.json({ error: `Limite de ${MAX_DEMO_SITES} site(s) atteinte` }, 409);
  }
  const code = body.code?.trim().toLowerCase();
  const name = body.name?.trim();
  if (!code || !name) return c.json({ error: 'Code et nom requis' }, 400);
  if (demoSites.some((s) => s.code === code)) return c.json({ error: 'Code site déjà utilisé' }, 409);
  const site = {
    id: crypto.randomUUID(),
    code,
    name,
    type: body.type?.trim().toLowerCase() ?? 'site',
    city: body.city?.trim(),
    active: true,
  };
  demoSites.push(site);
  pushAudit('create', 'sites', 'Site', site.id, `Site créé : ${site.name}`, c.get('userId'));
  return c.json(site);
});

siteRoutes.patch('/:id', async (c) => {
  if (!isAdmin(c.get('roleCode')) && !hasPermission(c.get('roleCode'), 'sites', 'write')) {
    return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  }
  const site = demoSites.find((s) => s.id === c.req.param('id'));
  if (!site) return c.json({ error: 'Site introuvable' }, 404);
  const body = await c.req.json<{ name?: string; type?: string; city?: string; active?: boolean }>();
  if (body.name) site.name = body.name.trim();
  if (body.type) site.type = body.type.trim().toLowerCase();
  if (body.city !== undefined) site.city = body.city?.trim() || undefined;
  if (typeof body.active === 'boolean') site.active = body.active;
  pushAudit('update', 'sites', 'Site', site.id, `Site modifié : ${site.name}`, c.get('userId'));
  return c.json(site);
});
