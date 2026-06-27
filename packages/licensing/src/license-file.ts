import type { JWK } from 'jose';
import type { RaqmiLicensePayload } from './license-types.js';
import { signPayload, verifySignedPayload } from './license-crypto.js';

export const LICENSE_FILE_VERSION = 1;

export interface RaqmiLicenseFile {
  version: number;
  payload: Omit<RaqmiLicensePayload, 'signature'>;
  signature: string;
}

export function stripSignature(payload: RaqmiLicensePayload): Omit<RaqmiLicensePayload, 'signature'> {
  const { signature: _signature, ...rest } = payload;
  return rest;
}

export async function signLicenseFile(
  payload: RaqmiLicensePayload,
  privateKeyJwk: JWK,
): Promise<RaqmiLicenseFile> {
  const unsigned = stripSignature(payload);
  const signature = await signPayload(unsigned as unknown as Record<string, unknown>, privateKeyJwk);
  return { version: LICENSE_FILE_VERSION, payload: unsigned, signature };
}

export async function verifyLicenseFile(
  file: RaqmiLicenseFile,
  publicKeyJwk: JWK,
): Promise<RaqmiLicensePayload> {
  if (file.version !== LICENSE_FILE_VERSION) {
    throw new Error(`Version de fichier licence non supportée: ${file.version}`);
  }

  const verified = await verifySignedPayload(file.signature, publicKeyJwk);
  if (verified.licenseId !== file.payload.licenseId || verified.tenantId !== file.payload.tenantId) {
    throw new Error('Signature de licence invalide ou altérée');
  }

  return { ...file.payload, signature: file.signature };
}

export function serializeLicenseFile(file: RaqmiLicenseFile): string {
  return JSON.stringify(file, null, 2);
}

export function parseLicenseFile(content: string): RaqmiLicenseFile {
  const parsed = JSON.parse(content) as RaqmiLicenseFile;
  if (!parsed.payload || !parsed.signature) {
    throw new Error('Fichier licence incomplet');
  }
  return parsed;
}

export function buildLicensePayload(
  input: Omit<RaqmiLicensePayload, 'signature' | 'issuedAt'> & { issuedAt?: string },
): RaqmiLicensePayload {
  return {
    ...input,
    issuedAt: input.issuedAt ?? new Date().toISOString(),
  };
}
