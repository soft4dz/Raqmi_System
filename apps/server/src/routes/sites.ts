import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth.js';
import { requireModule } from '../middleware/require-module.js';

type DemoSite = {
  id: string;
  code: string;
  name: string;
  type: string;
  city?: string;
  active: boolean;
};

const MAX_SITES = 5;

const demoSites: DemoSite[] = [
  { id: 'demo-site-001', code: 'main', name: 'Hotel Demo — Siège', type: 'hotel', city: 'Alger', active: true },
  { id: 'demo-site-002', code: 'annexe', name: 'Annexe Plage', type: 'annexe', city: 'Tipaza', active: true },
];

const userSites: Record<string, string[]> = {
  'demo-user-001': ['demo-site-001', 'demo-site-002'],
  'demo-user-002': ['demo-site-001', 'demo-site-002'],
  'demo-user-003': ['demo-site-002'],
};

export const siteRoutes = new Hono();

siteRoutes.use('*', authMiddleware);
siteRoutes.use('*', requireModule('sites'));

siteRoutes.get('/', (c) => {
  const roleCode = c.get('roleCode') as string;
  const userId = c.get('userId') as string;
  const items = roleCode === 'admin'
    ? demoSites
    : demoSites.filter((s) => userSites[userId]?.includes(s.id));
  return c.json({ items });
});

siteRoutes.post('/', async (c) => {
  if (c.get('roleCode') !== 'admin') return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  const body = await c.req.json<{ code?: string; name?: string; type?: string; city?: string }>();
  if (demoSites.filter((s) => s.active).length >= MAX_SITES) {
    return c.json({ error: `Limite de ${MAX_SITES} site(s) atteinte` }, 409);
  }
  const code = body.code?.trim().toLowerCase();
  const name = body.name?.trim();
  if (!code || !name) return c.json({ error: 'Code et nom requis' }, 400);
  if (demoSites.some((s) => s.code === code)) return c.json({ error: 'Code site déjà utilisé' }, 409);
  const site: DemoSite = {
    id: crypto.randomUUID(),
    code,
    name,
    type: body.type?.trim().toLowerCase() ?? 'site',
    city: body.city?.trim(),
    active: true,
  };
  demoSites.push(site);
  return c.json(site);
});

siteRoutes.patch('/:id', async (c) => {
  if (c.get('roleCode') !== 'admin') return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  const site = demoSites.find((s) => s.id === c.req.param('id'));
  if (!site) return c.json({ error: 'Site introuvable' }, 404);
  const body = await c.req.json<{ name?: string; type?: string; city?: string; active?: boolean }>();
  if (body.name) site.name = body.name.trim();
  if (body.type) site.type = body.type.trim().toLowerCase();
  if (body.city !== undefined) site.city = body.city?.trim() || undefined;
  if (typeof body.active === 'boolean') site.active = body.active;
  return c.json(site);
});
