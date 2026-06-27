import { Router } from 'express';
import { auditLogs, permissions, roles, settings, sites, users } from '../data/in-memory-store';

export const v1AdminRouter = Router();

v1AdminRouter.get('/users', (_request, response) => {
  response.json({
    items: users.map((item) => ({
      id: item.id,
      email: item.email,
      fullName: item.fullName,
      roleCode: item.roleCodes[0] ?? 'LECTURE_SEULE',
      active: item.active,
      siteIds: item.siteIds,
    })),
  });
});

v1AdminRouter.get('/roles', (_request, response) => {
  response.json({
    items: roles.map((item) => ({
      code: item.code,
      label: item.label,
      isSystem: item.system,
      permissions: item.permissions,
    })),
  });
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
