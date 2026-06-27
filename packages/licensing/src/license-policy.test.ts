import { describe, expect, it } from 'vitest';
import { RAQMI_LICENSE_PACKS } from './license-packs';
import { evaluateLicense } from './license-policy';
import type { RaqmiLicensePayload } from './license-types';

const now = new Date('2026-06-26T12:00:00Z');
const professionalPack = RAQMI_LICENSE_PACKS.find((p) => p.kind === 'professional')!;

function buildLicense(overrides: Partial<RaqmiLicensePayload> = {}): RaqmiLicensePayload {
  return {
    licenseId: 'test-001',
    tenantId: 'tenant-001',
    tenantName: 'Hotel Test',
    kind: 'professional',
    mode: 'offline',
    status: 'active',
    issuedAt: '2026-01-01T00:00:00Z',
    startsAt: '2026-01-01T00:00:00Z',
    expiresAt: '2027-01-01T00:00:00Z',
    allowedModules: professionalPack.modules,
    limits: professionalPack.defaultLimits,
    ...overrides,
  };
}

describe('evaluateLicense', () => {
  it('accepte une licence Professional valide', () => {
    const result = evaluateLicense(buildLicense(), {
      now,
      usersCount: 5,
      sitesCount: 2,
      storageUsedGb: 10,
    });

    expect(result.valid).toBe(true);
    expect(result.readonlyMode).toBe(false);
    expect(result.allowedModules).toEqual(professionalPack.modules);
  });

  it('refuse une licence expirée', () => {
    const result = evaluateLicense(buildLicense(), {
      now: new Date('2028-01-01'),
      usersCount: 5,
      sitesCount: 1,
      storageUsedGb: 5,
    });

    expect(result.valid).toBe(false);
    expect(result.reason).toBe('Licence expirée');
    expect(result.readonlyMode).toBe(true);
  });

  it('refuse un dépassement du nombre d’utilisateurs', () => {
    const result = evaluateLicense(buildLicense(), {
      now,
      usersCount: 100,
      sitesCount: 1,
      storageUsedGb: 5,
    });

    expect(result.valid).toBe(false);
    expect(result.reason).toBe('Nombre maximum d’utilisateurs dépassé');
  });

  it('refuse un module non inclus dans la licence', () => {
    const result = evaluateLicense(buildLicense(), {
      now,
      usersCount: 5,
      sitesCount: 1,
      storageUsedGb: 5,
      requestedModule: 'portmaster',
    });

    expect(result.valid).toBe(false);
    expect(result.reason).toBe('Module non inclus dans la licence: portmaster');
    expect(result.readonlyMode).toBe(false);
  });

  it('refuse une licence suspendue', () => {
    const result = evaluateLicense(buildLicense({ status: 'suspended' }), {
      now,
      usersCount: 5,
      sitesCount: 1,
      storageUsedGb: 5,
    });

    expect(result.valid).toBe(false);
    expect(result.reason).toBe('Licence suspended');
    expect(result.allowedModules).toEqual([]);
  });
});

describe('RAQMI_LICENSE_PACKS', () => {
  it('expose les packs Starter, Professional et Enterprise', () => {
    expect(RAQMI_LICENSE_PACKS.map((p) => p.kind)).toEqual([
      'starter',
      'professional',
      'enterprise',
    ]);
  });

  it('Enterprise inclut PortMaster et le stockage cloud', () => {
    const enterprise = RAQMI_LICENSE_PACKS.find((p) => p.kind === 'enterprise')!;
    expect(enterprise.modules).toContain('portmaster');
    expect(enterprise.modules).toContain('cloud_storage');
  });
});
