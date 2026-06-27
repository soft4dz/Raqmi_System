import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth.js';
import { requireModule } from '../middleware/require-module.js';
import { DEMO_USER } from '../demo-data.js';

type DemoUser = {
  id: string;
  email: string;
  fullName: string;
  roleCode: string;
  active: boolean;
  siteIds: string[];
};

type DemoRole = {
  code: string;
  label: string;
  isSystem: boolean;
  permissions: string[];
};

const DEMO_SITES = [
  { id: 'demo-site-001', code: 'main', name: 'Hotel Demo — Siège', type: 'hotel', city: 'Alger', active: true },
  { id: 'demo-site-002', code: 'annexe', name: 'Annexe Plage', type: 'annexe', city: 'Tipaza', active: true },
];

const demoRoles: DemoRole[] = [
  { code: 'admin', label: 'Administrateur', isSystem: true, permissions: ['*'] },
  {
    code: 'manager',
    label: 'Responsable',
    isSystem: true,
    permissions: ['administration:read', 'sites:read', 'finance:read', 'finance:write', 'hr:read', 'hr:write', 'stocks:read', 'ged:read'],
  },
  { code: 'user', label: 'Utilisateur', isSystem: true, permissions: ['finance:read', 'hr:read', 'stocks:read', 'ged:read'] },
];

const auditLogs: Array<{
  id: string;
  userId?: string;
  action: string;
  moduleCode?: string;
  entityType?: string;
  entityId?: string;
  description: string;
  createdAt: string;
}> = [
  {
    id: 'audit-001',
    userId: DEMO_USER.id,
    action: 'login',
    moduleCode: 'administration',
    entityType: 'User',
    entityId: DEMO_USER.id,
    description: 'Connexion administrateur',
    createdAt: new Date().toISOString(),
  },
];

function pushAudit(action: string, moduleCode: string, entityType: string, entityId: string, description: string) {
  auditLogs.unshift({
    id: crypto.randomUUID(),
    userId: DEMO_USER.id,
    action,
    moduleCode,
    entityType,
    entityId,
    description,
    createdAt: new Date().toISOString(),
  });
}

const demoUsers: DemoUser[] = [
  {
    id: DEMO_USER.id,
    email: DEMO_USER.email,
    fullName: DEMO_USER.fullName,
    roleCode: DEMO_USER.roleCode,
    active: true,
    siteIds: ['demo-site-001', 'demo-site-002'],
  },
  {
    id: 'demo-user-002',
    email: 'manager@demo.raqmi.local',
    fullName: 'Responsable Demo',
    roleCode: 'manager',
    active: true,
    siteIds: ['demo-site-001', 'demo-site-002'],
  },
  {
    id: 'demo-user-003',
    email: 'user@demo.raqmi.local',
    fullName: 'Utilisateur Demo',
    roleCode: 'user',
    active: true,
    siteIds: ['demo-site-002'],
  },
];

function normalizeSiteIds(siteIds?: string[]): string[] {
  if (!siteIds?.length) return ['demo-site-001'];
  const valid = siteIds.filter((id) => DEMO_SITES.some((s) => s.id === id));
  return valid.length ? valid : ['demo-site-001'];
}

export const adminRoutes = new Hono();

adminRoutes.use('*', authMiddleware);
adminRoutes.use('*', requireModule('administration'));

adminRoutes.get('/roles', (c) => c.json({ items: demoRoles }));

adminRoutes.post('/roles', async (c) => {
  if (c.get('roleCode') !== 'admin') return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  const body = await c.req.json<{ code?: string; label?: string; permissions?: string[] }>();
  const code = body.code?.trim().toLowerCase();
  if (!code || !body.label?.trim()) return c.json({ error: 'Code et libellé requis' }, 400);
  if (demoRoles.some((r) => r.code === code)) return c.json({ error: 'Code rôle déjà utilisé' }, 409);
  const role: DemoRole = { code, label: body.label.trim(), isSystem: false, permissions: body.permissions ?? [] };
  demoRoles.push(role);
  pushAudit('create', 'administration', 'Role', code, `Rôle créé : ${role.label}`);
  return c.json(role);
});

adminRoutes.patch('/roles/:code', async (c) => {
  if (c.get('roleCode') !== 'admin') return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  const role = demoRoles.find((r) => r.code === c.req.param('code'));
  if (!role) return c.json({ error: 'Rôle introuvable' }, 404);
  if (role.isSystem) return c.json({ error: 'Rôle système non modifiable' }, 409);
  const body = await c.req.json<{ label?: string; permissions?: string[] }>();
  if (body.label) role.label = body.label.trim();
  if (body.permissions) role.permissions = body.permissions;
  return c.json(role);
});

adminRoutes.delete('/roles/:code', (c) => {
  if (c.get('roleCode') !== 'admin') return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  const code = c.req.param('code');
  const index = demoRoles.findIndex((r) => r.code === code);
  if (index < 0) return c.json({ error: 'Rôle introuvable' }, 404);
  if (demoRoles[index].isSystem) return c.json({ error: 'Impossible de supprimer un rôle système' }, 409);
  demoRoles.splice(index, 1);
  return c.body(null, 204);
});

adminRoutes.get('/audit-logs', (c) => {
  if (c.get('roleCode') !== 'admin') return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  return c.json({ items: auditLogs.slice(0, 100) });
});

adminRoutes.get('/sites', (c) => c.json({ items: DEMO_SITES }));

adminRoutes.get('/users', (c) => c.json({ items: demoUsers }));

adminRoutes.post('/users', async (c) => {
  if (c.get('roleCode') !== 'admin') {
    return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  }

  const body = await c.req.json<{ email?: string; fullName?: string; roleCode?: string; siteIds?: string[] }>();
  const email = body.email?.trim().toLowerCase();
  const fullName = body.fullName?.trim();
  const roleCode = (body.roleCode ?? 'user').trim().toLowerCase();

  if (!email || !fullName) {
    return c.json({ error: 'Email et nom requis' }, 400);
  }

  if (!demoRoles.some((r) => r.code === roleCode)) {
    return c.json({ error: 'Rôle invalide' }, 400);
  }

  if (demoUsers.some((u) => u.email === email)) {
    return c.json({ error: 'Email déjà utilisé' }, 409);
  }

  const user: DemoUser = {
    id: crypto.randomUUID(),
    email,
    fullName,
    roleCode,
    active: true,
    siteIds: normalizeSiteIds(body.siteIds),
  };
  demoUsers.push(user);
  pushAudit('create', 'administration', 'User', user.id, `Utilisateur créé : ${user.email}`);
  return c.json(user);
});

adminRoutes.patch('/users/:id', async (c) => {
  if (c.get('roleCode') !== 'admin') {
    return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  }

  const id = c.req.param('id');
  const body = await c.req.json<{ fullName?: string; roleCode?: string; active?: boolean; siteIds?: string[]; password?: string }>();
  const user = demoUsers.find((u) => u.id === id);
  if (!user) return c.json({ error: 'Utilisateur introuvable' }, 404);

  if (body.fullName) user.fullName = body.fullName.trim();
  if (body.roleCode) {
    const roleCode = body.roleCode.trim().toLowerCase();
    if (!demoRoles.some((r) => r.code === roleCode)) return c.json({ error: 'Rôle invalide' }, 400);
    user.roleCode = roleCode;
  }
  if (typeof body.active === 'boolean') user.active = body.active;
  if (body.siteIds) user.siteIds = normalizeSiteIds(body.siteIds);
  pushAudit('update', 'administration', 'User', id, `Utilisateur modifié : ${user.fullName}`);

  return c.json(user);
});

export const businessStubRoutes = new Hono();
businessStubRoutes.use('*', authMiddleware);

businessStubRoutes.get('/finance/daily-revenue', requireModule('daily_revenue'), (c) => c.json({ items: [] }));
businessStubRoutes.get('/finance/invoices', requireModule('billing'), (c) => c.json({ items: [] }));
businessStubRoutes.get('/finance/treasury/movements', requireModule('treasury'), (c) =>
  c.json({ items: [], balance: 0 }),
);
businessStubRoutes.get('/hr/employees', requireModule('hr'), (c) => c.json({ items: [] }));
businessStubRoutes.get('/hr/attendance', requireModule('hr'), (c) => c.json({ items: [] }));
businessStubRoutes.get('/stocks/products', requireModule('stocks'), (c) => c.json({ items: [] }));
businessStubRoutes.get('/stocks/movements', requireModule('stocks'), (c) =>
  c.json({ items: [], stockByProduct: [] }),
);
businessStubRoutes.get('/stocks/inventories', requireModule('stocks'), (c) => c.json({ items: [] }));
businessStubRoutes.get('/ged/documents', requireModule('ged'), (c) => c.json({ items: [] }));
