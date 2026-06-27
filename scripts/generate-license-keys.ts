#!/usr/bin/env tsx
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { generateLicenseKeyPair } from '../packages/licensing/src/license-crypto.ts';

const outDir = resolve(process.cwd(), 'keys');
const publicPath = resolve(outDir, 'public.jwk.json');
const privatePath = resolve(outDir, 'private.jwk.json');
const serverKeysDir = resolve(process.cwd(), 'apps/server/keys');
const installerPublic = resolve(process.cwd(), 'apps/server/installer/assets/public.jwk.json');

const keys = await generateLicenseKeyPair();
await mkdir(outDir, { recursive: true });
await mkdir(serverKeysDir, { recursive: true });
await mkdir(dirname(installerPublic), { recursive: true });
const publicJson = JSON.stringify(keys.publicKey, null, 2);
await writeFile(publicPath, publicJson, 'utf8');
await writeFile(privatePath, JSON.stringify(keys.privateKey, null, 2), 'utf8');
await writeFile(resolve(serverKeysDir, 'public.jwk.json'), publicJson, 'utf8');
await writeFile(installerPublic, publicJson, 'utf8');

console.log('Clés générées:');
console.log('  Public :', publicPath);
console.log('  Private:', privatePath);
console.log('');
console.log('Serveur: LICENSE_PUBLIC_KEY_PATH=' + publicPath);
