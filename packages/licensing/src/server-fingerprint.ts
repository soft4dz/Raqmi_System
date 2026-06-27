import { createHash } from 'node:crypto';
import { hostname, platform, arch } from 'node:os';

export function computeServerFingerprint(extra?: string): string {
  const raw = [hostname(), platform(), arch(), extra ?? 'raqmi-v1'].join('|');
  return createHash('sha256').update(raw).digest('hex').slice(0, 32);
}
