import { Router } from 'express';
import { addAuditLog, auditLogs, permissions, roles, settings, sites, users } from '../data/in-memory-store';

function toUserDto(item: (typeof users)[number]) {
  return {
    id: item.id,
    email: item.email,
    fullName: item.fullName,
    roleCode: item.roleCodes[0] ?? 'LECTURE_SEULE',
    active: item.active,
    siteIds: item.siteIds,
  };
}

function toRoleDto(item: (typeof roles)[number]) {
  return {
    code: item.code,
    label: item.label,
    isSystem: item.system,
    permissions: item.permissions,
  };
}

export const v1AdminRouter = Router();

v1AdminRouter.get('/users', (_request, response) => {
  response.json({ items: users.map(toUserDto) });
});

v1AdminRouter.post('/users', (request, response) => {
  const body = request.body ?? {};
  const item = {
    id: `user-${Date.now()}`,
    tenantId: 'tenant-demo-hotel',
    email: String(body.email ?? '').toLowerCase(),
    fullName: String(body.fullName ?? 'Nouvel utilisateur'),
    roleCodes: [String(body.roleCode ?? 'LECTURE_SEULE')],
    siteIds: Array.isArray(body.siteIds) ? body.siteIds : [],
    active: true,
    forceChangeSecret: true,
    createdAt: new Date().toISOString(),
  };
  users.push(item);
  addAuditLog({ tenantId: item.tenantId, userId: item.id, moduleCode: 'administration', action: 'admin.user.create', entityType: 'user', entityId: item.id, description: `Utilisateur cree: ${item.email}` });
  response.status(201).json(toUserDto(item));
});

v1AdminRouter.patch('/users/:id', (request, response) => {
  const item = users.find((user) => user.id === request.params.id);
  if (!item) {
    response.status(404).json({ error: 'Utilisateur introuvable.' });
    return;
  }

  const body = request.body ?? {};
  if (typeof body.fullName === 'string') item.fullName = body.fullName;
  if (typeof body.roleCode === 'string') item.roleCodes = [body.roleCode];
  if (typeof body.active === 'boolean') item.active = body.active;
  if (Array.isArray(body.siteIds)) item.siteIds = body.siteIds;

  addAuditLog({ tenantId: item.tenantId, userId: item.id, moduleCode: 'administration', action: 'admin.user.update', entityType: 'user', entityId: item.id, description: `Utilisateur modifie: ${item.email}` });
  response.json(toUserDto(item));
});

v1AdminRouter.get('/roles', (_request, response) => {
  response.json({ items: roles.map(toRoleDto) });
});

v1AdminRouter.post('/roles', (request, response) => {
  const body = request.body ?? {};
  const item = {
    code: String(body.code ?? '').toUpperCase(),
    label: String(body.label ?? 'Nouveau role'),
    system: false,
    description: 'Role cree depuis Administration.',
    permissions: Array.isArray(body.permissions) ? body.permissions : [],
  };
  roles.push(item);
  addAuditLog({ tenantId: 'tenant-demo-hotel', moduleCode: 'administration', action: 'admin.role.create', entityType: 'role', entityId: item.code, description: `Role cree: ${item.code}` });
  response.status(201).json(toRoleDto(item));
});

v1AdminRouter.patch('/roles/:code', (request, response) => {
  const item = roles.find((role) => role.code === request.params.code);
  if (!item) {
    response.status(404).json({ error: 'Role introuvable.' });
    return;
  }

  const body = request.body ?? {};
  if (typeof body.label === 'string') item.label = body.label;
  if (Array.isArray(body.permissions)) item.permissions = body.permissions;

  addAuditLog({ tenantId: 'tenant-demo-hotel', moduleCode: 'administration', action: 'admin.role.update', entityType: 'role', entityId: item.code, description: `Role modifie: ${item.code}` });
  response.json(toRoleDto(item));
});

v1AdminRouter.delete('/roles/:code', (request, response) => {
  const index = roles.findIndex((role) => role.code === request.params.code && !role.system);
  if (index === -1) {
    response.status(404).json({ error: 'Role introuvable ou systeme.' });
    return;
  }
  const [removed] = roles.splice(index, 1);
  addAuditLog({ tenantId: 'tenant-demo-hotel', moduleCode: 'administration', action: 'admin.role.delete', entityType: 'role', entityId: removed.code, description: `Role supprime: ${removed.code}` });
  response.status(204).send();
});

v1AdminRouter.get('/permissions', (_request, response) => {
  response.json({ items: permissions });
});

v1AdminRouter.get('/sites', (_request, response) => {
  response.json({ items: sites });
});

v1AdminRouter.get('/audit-logs', (_request, response) => {
  response.json({ items: auditLogs });
});

v1AdminRouter.get('/settings', (_request, response) => {
  response.json({ items: settings });
});
