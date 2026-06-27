import { RAQMI_MODULES } from '@raqmi/shared';
import { RAQMI_LICENSE_PACKS } from '@raqmi/licensing/packs';
import bcrypt from 'bcryptjs';
import { prisma } from './index.js';

async function main() {
  for (const module of RAQMI_MODULES) {
    await prisma.moduleDefinition.upsert({
      where: { code: module.code },
      update: {
        label: module.label,
        family: module.family,
        commercial: module.commercial,
        description: module.description,
      },
      create: {
        code: module.code,
        label: module.label,
        family: module.family,
        commercial: module.commercial,
        description: module.description,
      },
    });
  }

  const tenant = await prisma.tenant.upsert({
    where: { code: 'demo-hotel' },
    update: { name: 'Hotel Demo Raqmi' },
    create: {
      code: 'demo-hotel',
      name: 'Hotel Demo Raqmi',
      legalName: 'Hotel Demo Raqmi SARL',
      email: 'contact@demo.raqmi.local',
    },
  });

  await prisma.site.upsert({
    where: { tenantId_code: { tenantId: tenant.id, code: 'main' } },
    update: { name: 'Site principal' },
    create: {
      tenantId: tenant.id,
      code: 'main',
      name: 'Site principal',
      city: 'Casablanca',
    },
  });

  const pack = RAQMI_LICENSE_PACKS.find((p) => p.kind === 'professional')!;
  const moduleRows = await prisma.moduleDefinition.findMany({
    where: { code: { in: pack.modules } },
  });

  const existingLicense = await prisma.license.findFirst({
    where: { tenantId: tenant.id, status: 'ACTIVE' },
  });

  const license =
    existingLicense ??
    (await prisma.license.create({
      data: {
        tenantId: tenant.id,
        kind: 'PROFESSIONAL',
        mode: 'OFFLINE',
        status: 'ACTIVE',
        startsAt: new Date('2026-01-01'),
        expiresAt: new Date('2027-12-31'),
        maxUsers: pack.defaultLimits.maxUsers,
        maxSites: pack.defaultLimits.maxSites,
        maxStorageGb: pack.defaultLimits.maxStorageGb,
        offlineGraceDays: pack.defaultLimits.offlineGraceDays,
        modules: {
          create: moduleRows.map((module) => ({ moduleId: module.id })),
        },
      },
    }));

  if (existingLicense) {
    console.log('Licence existante conservée:', license.id);
  }

  const passwordHash = await bcrypt.hash('demo1234', 10);
  await prisma.user.upsert({
    where: { tenantId_email: { tenantId: tenant.id, email: 'admin@demo.raqmi.local' } },
    update: { passwordHash, fullName: 'Administrateur Demo' },
    create: {
      tenantId: tenant.id,
      email: 'admin@demo.raqmi.local',
      passwordHash,
      fullName: 'Administrateur Demo',
      roleCode: 'admin',
    },
  });

  console.log('Seed terminé pour le tenant:', tenant.code);
}

main()
  .catch((error) => {
    console.error(error);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
