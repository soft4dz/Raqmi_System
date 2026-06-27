const DEFAULT_PORT = 3000;

/** Accepte une URL complète ou une IP / nom d'hôte local (port 3000 par défaut). */
export function normalizeServerUrl(input: string, defaultPort = DEFAULT_PORT): string {
  let value = input.trim().replace(/\/+$/, '');
  if (!value) return '';

  if (/^https?:\/\//i.test(value)) {
    return value;
  }

  if (value.startsWith('[')) {
    const withPort = /]:\d+$/.test(value) ? value : `${value}:${defaultPort}`;
    return `http://${withPort}`;
  }

  const match = value.match(/^([^:/]+)(?::(\d+))?$/);
  if (!match) {
    return `http://${value}`;
  }

  const host = match[1];
  const port = match[2] ?? String(defaultPort);
  return `http://${host}:${port}`;
}

export function isPlausibleServerUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}
