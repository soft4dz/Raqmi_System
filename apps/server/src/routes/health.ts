import { Hono } from 'hono';
import { env } from '../env.js';

export const healthRoutes = new Hono();

healthRoutes.get('/health', (c) =>
  c.json({
    status: 'ok',
    service: 'raqmi-system-server',
    version: '0.1.0',
    mode: env.DEMO_MODE ? 'demo' : 'database',
    storage: env.FILE_STORAGE_DRIVER,
    timestamp: new Date().toISOString(),
  }),
);
