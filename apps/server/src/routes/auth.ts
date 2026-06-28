import bcrypt from 'bcryptjs';
import { Hono } from 'hono';
import { SignJWT } from 'jose';
import { prisma } from '@raqmi/database';
import { DEMO_TENANT } from '../demo-data.js';
import {
  findDemoUserByEmail,
  getUserSiteIds,
  permissionsForRole,
  pushAudit,
} from '../demo-stores.js';
import { env } from '../env.js';

export const authRoutes = new Hono();

authRoutes.post('/login', async (c) => {
  const body = await c.req.json<{ email?: string; password?: string }>();
  const email = body.email?.trim().toLowerCase();
  const password = body.password ?? '';

  if (!email || !password) {
    return c.json({ error: 'Email et mot de passe requis' }, 400);
  }

  if (env.DEMO_MODE) {
    const user = findDemoUserByEmail(email);
    if (!user || !user.active || user.password !== password) {
      return c.json({ error: 'Identifiants invalides' }, 401);
    }

    pushAudit('login', 'administration', 'User', user.id, `Connexion : ${user.email}`, user.id);

    const token = await signToken({
      sub: user.id,
      tenantId: DEMO_TENANT.id,
      email: user.email,
      fullName: user.fullName,
      roleCode: user.roleCode,
    });

    return c.json({
      token,
      user: {
        id: user.id,
        email: user.email,
        fullName: user.fullName,
        roleCode: user.roleCode,
        tenant: DEMO_TENANT,
        siteIds: user.siteIds,
        permissions: permissionsForRole(user.roleCode),
      },
    });
  }

  const user = await prisma.user.findFirst({
    where: { email, active: true },
    include: { tenant: true },
  });

  if (!user || !(await bcrypt.compare(password, user.passwordHash))) {
    return c.json({ error: 'Identifiants invalides' }, 401);
  }

  const token = await signToken({
    sub: user.id,
    tenantId: user.tenantId,
    email: user.email,
    fullName: user.fullName,
    roleCode: user.roleCode,
  });

  return c.json({
    token,
    user: {
      id: user.id,
      email: user.email,
      fullName: user.fullName,
      roleCode: user.roleCode,
      tenant: {
        id: user.tenant.id,
        code: user.tenant.code,
        name: user.tenant.name,
      },
    },
  });
});

authRoutes.get('/me', async (c) => {
  const header = c.req.header('Authorization');
  const token = header?.startsWith('Bearer ') ? header.slice(7) : null;
  if (!token) return c.json({ error: 'Non authentifié' }, 401);

  try {
    const payload = await verifyToken(token);
    const siteIds = env.DEMO_MODE ? getUserSiteIds(payload.sub) : [];
    const permissions = env.DEMO_MODE ? permissionsForRole(payload.roleCode) : ['*'];

    return c.json({
      user: {
        id: payload.sub,
        email: payload.email,
        fullName: payload.fullName,
        roleCode: payload.roleCode,
        tenantId: payload.tenantId,
        siteIds,
        permissions,
      },
    });
  } catch {
    return c.json({ error: 'Session invalide' }, 401);
  }
});

interface TokenPayload {
  sub: string;
  tenantId: string;
  email: string;
  fullName: string;
  roleCode: string;
}

async function signToken(payload: TokenPayload) {
  const secret = new TextEncoder().encode(env.JWT_SECRET);
  return new SignJWT(payload)
    .setProtectedHeader({ alg: 'HS256' })
    .setIssuedAt()
    .setExpirationTime('8h')
    .sign(secret);
}

async function verifyToken(token: string) {
  const { jwtVerify } = await import('jose');
  const secret = new TextEncoder().encode(env.JWT_SECRET);
  const { payload } = await jwtVerify(token, secret);
  return payload as TokenPayload & { sub: string };
}

export { verifyToken };
