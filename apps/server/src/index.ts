import './load-env.js';
import { serve } from '@hono/node-server';
import { Hono } from 'hono';
import { cors } from 'hono/cors';
import { env } from './env.js';
import { authRoutes } from './routes/auth.js';
import { adminRoutes } from './routes/admin.js';
import { healthRoutes } from './routes/health.js';
import { licenseRoutes } from './routes/license.js';
import { moduleRoutes } from './routes/modules.js';
import { siteRoutes } from './routes/sites.js';
import { settingsRoutes } from './routes/settings.js';
import { businessRoutes } from './routes/business.js';

const app = new Hono();

app.use(
  '*',
  cors({
    origin: env.CLIENT_ORIGIN,
    credentials: true,
  }),
);

app.route('/', healthRoutes);
app.route('/api/v1/auth', authRoutes);
app.route('/api/v1/license', licenseRoutes);
app.route('/api/v1/modules', moduleRoutes);
app.route('/api/v1/admin', adminRoutes);
app.route('/api/v1/sites', siteRoutes);
app.route('/api/v1/settings', settingsRoutes);
app.route('/api/v1', businessRoutes);

serve({ fetch: app.fetch, port: env.PORT }, (info) => {
  console.log(`Raqmi System Server → http://localhost:${info.port}`);
  console.log(`Mode: ${env.DEMO_MODE ? 'demo (sans base de données)' : 'base de données'}`);
});
