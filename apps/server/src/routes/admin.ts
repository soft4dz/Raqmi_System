import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth.js';
import { requireModule } from '../middleware/require-module.js';
import {
  auditLogs,
  demoRoles,
  demoSites,
  demoUsers,
  findDemoUserByEmail,
  getUserSiteIds,
  hasPermission,
  isAdmin,
  normalizeSiteIds,
  PERMISSION_CATALOG,
  pushAudit,
  toPublicUser,
  userSharesSites,
  type DemoRole,
} from '../demo-stores.js';

export const adminRoutes = new Hono();

adminRoutes.use('*', authMiddleware);
adminRoutes.use('*', requireModule('administration'));

function callerId(c: { get: (k: 'userId' | 'roleCode') => string }) {
  return { userId: c.get('userId'), roleCode: c.get('roleCode') };
}

function requireAdminWrite(c: { get: (k: 'roleCode') => string; json: (body: unknown, status?: number) => Response }) {
  const { roleCode } = callerId(c);
  if (!isAdmin(roleCode) && !hasPermission(roleCode, 'administration', 'write')) {
    return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  }
  return null;
}

function requireAdminRead(c: { get: (k: 'roleCode') => string; json: (body: unknown, status?: number) => Response }) {
  const { roleCode } = callerId(c);
  if (!isAdmin(roleCode) && !hasPermission(roleCode, 'administration', 'read')) {
    return c.json({ error: 'Accès refusé' }, 403);
  }
  return null;
}

adminRoutes.get('/permissions', (c) => c.json({ items: PERMISSION_CATALOG }));

adminRoutes.get('/roles', (c) => {
  const denied = requireAdminRead(c);
  if (denied) return denied;
  return c.json({ items: demoRoles });
});

adminRoutes.post('/roles', async (c) => {
  const denied = requireAdminWrite(c);
  if (denied) return denied;
  const body = await c.req.json<{ code?: string; label?: string; permissions?: string[] }>();
  const code = body.code?.trim().toLowerCase();
  if (!code || !body.label?.trim()) return c.json({ error: 'Code et libellé requis' }, 400);
  if (demoRoles.some((r) => r.code === code)) return c.json({ error: 'Code rôle déjà utilisé' }, 409);
  const role: DemoRole = {
    code,
    label: body.label.trim(),
    isSystem: false,
    permissions: body.permissions ?? [],
  };
  demoRoles.push(role);
  pushAudit('create', 'administration', 'Role', code, `Rôle créé : ${role.label}`, c.get('userId'));
  return c.json(role);
});

adminRoutes.patch('/roles/:code', async (c) => {
  const denied = requireAdminWrite(c);
  if (denied) return denied;
  const role = demoRoles.find((r) => r.code === c.req.param('code'));
  if (!role) return c.json({ error: 'Rôle introuvable' }, 404);
  const body = await c.req.json<{ label?: string; permissions?: string[] }>();
  if (role.isSystem && body.permissions) {
    return c.json({ error: 'Les permissions des rôles système ne sont pas modifiables' }, 409);
  }
  if (body.label) role.label = body.label.trim();
  if (body.permissions && !role.isSystem) role.permissions = body.permissions;
  pushAudit('update', 'administration', 'Role', role.code, `Rôle modifié : ${role.label}`, c.get('userId'));
  return c.json(role);
});

adminRoutes.delete('/roles/:code', (c) => {
  const denied = requireAdminWrite(c);
  if (denied) return denied;
  const code = c.req.param('code');
  const index = demoRoles.findIndex((r) => r.code === code);
  if (index < 0) return c.json({ error: 'Rôle introuvable' }, 404);
  if (demoRoles[index].isSystem) return c.json({ error: 'Impossible de supprimer un rôle système' }, 409);
  const removed = demoRoles[index];
  demoRoles.splice(index, 1);
  pushAudit('delete', 'administration', 'Role', code, `Rôle supprimé : ${removed.label}`, c.get('userId'));
  return c.body(null, 204);
});

adminRoutes.get('/audit-logs', (c) => {
  const denied = requireAdminRead(c);
  if (denied) return denied;

  const action = c.req.query('action')?.trim().toLowerCase();
  const moduleCode = c.req.query('moduleCode')?.trim().toLowerCase();
  const q = c.req.query('q')?.trim().toLowerCase();

  let items = auditLogs;
  if (action) items = items.filter((log) => log.action.toLowerCase() === action);
  if (moduleCode) items = items.filter((log) => log.moduleCode?.toLowerCase() === moduleCode);
  if (q) items = items.filter((log) => log.description.toLowerCase().includes(q));

  return c.json({ items: items.slice(0, 100) });
});

adminRoutes.get('/sites', (c) => {
  const { roleCode, userId } = callerId(c);
  const items =
    isAdmin(roleCode)
      ? demoSites.filter((s) => s.active)
      : demoSites.filter((s) => s.active && getUserSiteIds(userId).includes(s.id));
  return c.json({ items });
});

adminRoutes.get('/users', (c) => {
  const denied = requireAdminRead(c);
  if (denied) return denied;

  const { roleCode, userId } = callerId(c);
  let items = demoUsers.map(toPublicUser);
  if (!isAdmin(roleCode)) {
    const callerSites = getUserSiteIds(userId);
    items = demoUsers.filter((u) => userSharesSites(u, callerSites)).map(toPublicUser);
  }
  return c.json({ items });
});

adminRoutes.post('/users', async (c) => {
  const denied = requireAdminWrite(c);
  if (denied) return denied;

  const body = await c.req.json<{ email?: string; fullName?: string; roleCode?: string; password?: string; siteIds?: string[] }>();
  const email = body.email?.trim().toLowerCase();
  const fullName = body.fullName?.trim();
  const roleCode = (body.roleCode ?? 'user').trim().toLowerCase();
  const password = body.password?.trim() || 'demo1234';

  if (!email || !fullName) return c.json({ error: 'Email et nom requis' }, 400);
  if (!demoRoles.some((r) => r.code === roleCode)) return c.json({ error: 'Rôle invalide' }, 400);
  if (demoUsers.some((u) => u.email === email)) return c.json({ error: 'Email déjà utilisé' }, 409);

  const user = {
    id: crypto.randomUUID(),
    email,
    fullName,
    roleCode,
    active: true,
    password,
    siteIds: normalizeSiteIds(body.siteIds),
  };
  demoUsers.push(user);
  pushAudit('create', 'administration', 'User', user.id, `Utilisateur créé : ${user.email}`, c.get('userId'));
  return c.json(toPublicUser(user));
});

adminRoutes.patch('/users/:id', async (c) => {
  const denied = requireAdminWrite(c);
  if (denied) return denied;

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
  if (body.password?.trim()) user.password = body.password.trim();

  pushAudit('update', 'administration', 'User', id, `Utilisateur modifié : ${user.fullName}`, c.get('userId'));
  return c.json(toPublicUser(user));
});
