import { exportJWK, generateKeyPair, importJWK, SignJWT, jwtVerify, type JWK } from 'jose';

export interface LicenseKeyPair {
  publicKey: JWK;
  privateKey: JWK;
}

export async function generateLicenseKeyPair(): Promise<LicenseKeyPair> {
  const { publicKey, privateKey } = await generateKeyPair('EdDSA', { crv: 'Ed25519', extractable: true });
  return {
    publicKey: await exportJWK(publicKey),
    privateKey: await exportJWK(privateKey),
  };
}

export async function signPayload(payload: Record<string, unknown>, privateKeyJwk: JWK): Promise<string> {
  const privateKey = await importJWK(privateKeyJwk, 'EdDSA');
  return new SignJWT({ license: payload })
    .setProtectedHeader({ alg: 'EdDSA' })
    .setIssuedAt()
    .sign(privateKey);
}

export async function verifySignedPayload(
  token: string,
  publicKeyJwk: JWK,
): Promise<Record<string, unknown>> {
  const publicKey = await importJWK(publicKeyJwk, 'EdDSA');
  const { payload } = await jwtVerify(token, publicKey);
  const license = payload.license;
  if (!license || typeof license !== 'object' || Array.isArray(license)) {
    throw new Error('Format de licence signée invalide');
  }
  return license as Record<string, unknown>;
}
