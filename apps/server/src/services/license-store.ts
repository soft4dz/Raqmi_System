import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { RaqmiLicensePayload } from '@raqmi/licensing/types';
import {
  parseLicenseFile,
  serializeLicenseFile,
  verifyLicenseFile,
  type RaqmiLicenseFile,
} from '@raqmi/licensing/file';
import { computeServerFingerprint } from '@raqmi/licensing/fingerprint';
import type { JWK } from 'jose';
import { env } from '../env.js';

const serverDir = dirname(fileURLToPath(import.meta.url));

/** Clé publique éditeur embarquée (voir installer/assets/public.jwk.json). */
export const EMBEDDED_PUBLIC_KEY: JWK = {
  kty: 'OKP',
  crv: 'Ed25519',
  x: 'RCpZcK7IL8Zk4Xw2mKm9akb3N-R5w_a95AT2Lr8HT6M',
};

let cachedLicense: RaqmiLicensePayload | null = null;
let lastOnlineCheckAt: Date | null = null;

export function getServerFingerprint(): string {
  return computeServerFingerprint(env.DATA_DIR);
}

export async function getActiveLicensePayload(): Promise<RaqmiLicensePayload | null> {
  return loadLicenseFromDisk();
}

export async function loadLicenseFromDisk(): Promise<RaqmiLicensePayload | null> {
  if (cachedLicense) return cachedLicense;

  try {
    const content = await readFile(env.LICENSE_FILE_PATH, 'utf8');
    const file = parseLicenseFile(content);
    const publicKey = await resolvePublicKey();
    const payload = await verifyLicenseFile(file, publicKey);

    if (payload.serverFingerprint && payload.serverFingerprint !== getServerFingerprint()) {
      throw new Error('Empreinte serveur incompatible avec cette licence');
    }

    cachedLicense = payload;
    return payload;
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
      return null;
    }
    throw error;
  }
}

export async function importLicenseFile(content: string): Promise<RaqmiLicensePayload> {
  const file = parseLicenseFile(content);
  const publicKey = await resolvePublicKey();
  const payload = await verifyLicenseFile(file, publicKey);

  if (payload.serverFingerprint && payload.serverFingerprint !== getServerFingerprint()) {
    throw new Error('Cette licence est liée à un autre serveur');
  }

  await mkdir(dirname(env.LICENSE_FILE_PATH), { recursive: true });
  await writeFile(env.LICENSE_FILE_PATH, serializeLicenseFile(file satisfies RaqmiLicenseFile), 'utf8');
  cachedLicense = payload;
  lastOnlineCheckAt = new Date();
  return payload;
}

export function getLastOnlineCheckAt(): Date | null {
  return lastOnlineCheckAt;
}

export function clearLicenseCache(): void {
  cachedLicense = null;
}

async function resolvePublicKey(): Promise<JWK> {
  if (env.LICENSE_PUBLIC_KEY_PATH) {
    const content = await readFile(env.LICENSE_PUBLIC_KEY_PATH, 'utf8');
    return JSON.parse(content) as JWK;
  }

  const bundledKeyPath = join(serverDir, '../../keys/public.jwk.json');
  try {
    const content = await readFile(bundledKeyPath, 'utf8');
    return JSON.parse(content) as JWK;
  } catch {
    return EMBEDDED_PUBLIC_KEY;
  }
}
