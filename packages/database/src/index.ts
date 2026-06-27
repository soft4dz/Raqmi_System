import { PrismaClient } from '@prisma/client';

const globalForPrisma = globalThis as typeof globalThis & { prisma?: PrismaClient };

export const prisma = globalForPrisma.prisma ?? new PrismaClient();

if (process.env.NODE_ENV !== 'production') {
  globalForPrisma.prisma = prisma;
}

export { PrismaClient };
export type { Tenant, User, License, Site } from '@prisma/client';
