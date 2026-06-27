import bcrypt from 'bcryptjs';
import { Hono } from 'hono';
import { SignJWT } from 'jose';
import { prisma } from '@raqmi/database';
import { DEMO_PASSWORD_HASH, DEMO_TENANT, DEMO_USER } from '../demo-data.js';
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
    if (email !== DEMO_USER.email || !(await bcrypt.compare(password, DEMO_PASSWORD_HASH))) {
      return c.json({ error: 'Identifiants invalides' }, 401);
    }

    const token = await signToken({
      sub: DEMO_USER.id,
      tenantId: DEMO_TENANT.id,
      email: DEMO_USER.email,
      fullName: DEMO_USER.fullName,
      roleCode: DEMO_USER.roleCode,
    });

    return c.json({
      token,
      user: {
        id: DEMO_USER.id,
        email: DEMO_USER.email,
        fullName: DEMO_USER.fullName,
        roleCode: DEMO_USER.roleCode,
        tenant: DEMO_TENANT,
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
    return c.json({
      user: {
        id: payload.sub,
        email: payload.email,
        fullName: payload.fullName,
        roleCode: payload.roleCode,
        tenantId: payload.tenantId,
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
