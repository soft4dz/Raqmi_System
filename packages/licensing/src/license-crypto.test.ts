import { describe, expect, it } from 'vitest';
import { generateLicenseKeyPair } from './license-crypto.js';
import { buildLicensePayload, parseLicenseFile, serializeLicenseFile, signLicenseFile, verifyLicenseFile } from './license-file.js';
import { computeServerFingerprint } from './server-fingerprint.js';

describe('license crypto', () => {
  it('génère une empreinte serveur stable', () => {
    const fp = computeServerFingerprint('test');
    expect(fp).toHaveLength(32);
    expect(computeServerFingerprint('test')).toBe(fp);
  });

  it('signe et vérifie un fichier licence', async () => {
    const keys = await generateLicenseKeyPair();
    const payload = buildLicensePayload({
      licenseId: 'lic-001',
      tenantId: 'tenant-001',
      tenantName: 'Hotel Demo',
      kind: 'professional',
      mode: 'offline',
      status: 'active',
      startsAt: '2026-01-01T00:00:00Z',
      expiresAt: '2027-01-01T00:00:00Z',
      allowedModules: ['billing', 'reports'],
      limits: { maxUsers: 50, maxSites: 5, maxStorageGb: 100, offlineGraceDays: 30 },
    });

    const file = await signLicenseFile(payload, keys.privateKey);
    const verified = await verifyLicenseFile(file, keys.publicKey);
    expect(verified.tenantName).toBe('Hotel Demo');
    expect(verified.signature).toBeTruthy();

    const roundTrip = parseLicenseFile(serializeLicenseFile(file));
    expect(roundTrip.payload.licenseId).toBe('lic-001');
  });
});
