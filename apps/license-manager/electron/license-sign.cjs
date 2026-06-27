const { importJWK, SignJWT, generateKeyPair, exportJWK } = require('jose');

const LICENSE_FILE_VERSION = 1;

async function generateLicenseKeyPair() {
  const { publicKey, privateKey } = await generateKeyPair('EdDSA', { crv: 'Ed25519', extractable: true });
  return {
    publicKey: await exportJWK(publicKey),
    privateKey: await exportJWK(privateKey),
  };
}

function stripSignature(payload) {
  const { signature: _signature, ...rest } = payload;
  return rest;
}

function buildLicensePayload(input) {
  return {
    ...input,
    issuedAt: input.issuedAt ?? new Date().toISOString(),
  };
}

async function signLicenseFile(payload, privateKeyJwk) {
  const unsigned = stripSignature(payload);
  const privateKey = await importJWK(privateKeyJwk, 'EdDSA');
  const signature = await new SignJWT({ license: unsigned })
    .setProtectedHeader({ alg: 'EdDSA' })
    .setIssuedAt()
    .sign(privateKey);
  return { version: LICENSE_FILE_VERSION, payload: unsigned, signature };
}

function serializeLicenseFile(file) {
  return JSON.stringify(file, null, 2);
}

module.exports = {
  LICENSE_FILE_VERSION,
  generateLicenseKeyPair,
  buildLicensePayload,
  signLicenseFile,
  serializeLicenseFile,
};
