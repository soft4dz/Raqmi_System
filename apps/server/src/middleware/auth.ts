import { createMiddleware } from 'hono/factory';
import { verifyToken } from '../routes/auth.js';

export const authMiddleware = createMiddleware<{
  Variables: {
    userId: string;
    tenantId: string;
    email: string;
    fullName: string;
    roleCode: string;
  };
}>(async (c, next) => {
  const header = c.req.header('Authorization');
  const token = header?.startsWith('Bearer ') ? header.slice(7) : null;

  if (!token) {
    return c.json({ error: 'Non authentifié' }, 401);
  }

  try {
    const payload = await verifyToken(token);
    c.set('userId', payload.sub);
    c.set('tenantId', payload.tenantId);
    c.set('email', payload.email);
    c.set('fullName', payload.fullName);
    c.set('roleCode', payload.roleCode);
    await next();
  } catch {
    return c.json({ error: 'Session invalide' }, 401);
  }
});
