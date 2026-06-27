import { config } from 'dotenv';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';

const envCandidates = [
  resolve(process.cwd(), '.env'),
  resolve(process.cwd(), '../.env'),
  resolve(process.cwd(), '../../.env'),
  resolve(import.meta.dirname, '../../../.env'),
];

for (const envPath of envCandidates) {
  if (existsSync(envPath)) {
    config({ path: envPath });
    break;
  }
}
