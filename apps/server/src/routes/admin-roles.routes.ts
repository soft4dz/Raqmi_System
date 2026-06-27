import { Router } from 'express';
import { permissions, roles } from '../data/in-memory-store';

export const adminRolesRouter = Router();

adminRolesRouter.get('/', (_request, response) => {
  response.json({ data: roles });
});

adminRolesRouter.get('/:code', (request, response) => {
  const role = roles.find((item) => item.code === request.params.code);
  if (!role) {
    response.status(404).json({ error: 'Rôle introuvable.' });
    return;
  }
  response.json({ data: role });
});

export const adminPermissionsRouter = Router();

adminPermissionsRouter.get('/', (_request, response) => {
  response.json({ data: permissions });
});

adminPermissionsRouter.get('/matrix', (_request, response) => {
  response.json({
    data: roles.map((role) => ({
      roleCode: role.code,
      roleLabel: role.label,
      permissions: permissions.map((permission) => ({
        code: permission.code,
        moduleCode: permission.moduleCode,
        label: permission.label,
        allowed: role.permissions.includes(permission.code),
      })),
    })),
  });
});
