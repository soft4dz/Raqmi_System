import { Router } from 'express';
import { tenants, users } from '../data/in-memory-store';

export const v1AuthRouter = Router();

v1AuthRouter.post('/login', (request, response) => {
  const email = String(request.body?.email ?? '').toLowerCase();
  const user = users.find((item) => item.email.toLowerCase() === email && item.active) ?? users[0];
  const tenant = tenants.find((item) => item.id === user.tenantId) ?? tenants[0];

  response.json({
    token: `dev-token-${user.id}`,
    user: {
      id: user.id,
      email: user.email,
      fullName: user.fullName,
      roleCode: user.roleCodes[0] ?? 'TENANT_ADMIN',
      tenant: { id: tenant.id, code: tenant.code, name: tenant.name },
    },
  });
});

v1AuthRouter.get('/me', (_request, response) => {
  const user = users[0];
  response.json({
    user: {
      id: user.id,
      email: user.email,
      fullName: user.fullName,
      roleCode: user.roleCodes[0] ?? 'TENANT_ADMIN',
      tenantId: user.tenantId,
    },
  });
});
