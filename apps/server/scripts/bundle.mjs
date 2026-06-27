import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { build } from 'esbuild';

const root = dirname(fileURLToPath(import.meta.url));
const serverRoot = join(root, '..');

await build({
  entryPoints: [join(serverRoot, 'src/index.ts')],
  outfile: join(serverRoot, 'dist/server.mjs'),
  bundle: true,
  platform: 'node',
  format: 'esm',
  target: 'node20',
  sourcemap: true,
  external: ['@prisma/client', '.prisma/client', '.prisma/client/default'],
  banner: {
    js: "import { createRequire } from 'module'; const require = createRequire(import.meta.url);",
  },
});

console.log('Bundle serveur → apps/server/dist/server.mjs');
