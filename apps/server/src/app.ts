import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import { healthRouter } from './routes/health.routes';
import { modulesRouter } from './routes/modules.routes';
import { tenantsRouter } from './routes/tenants.routes';
import { licensesRouter } from './routes/licenses.routes';

export function createApp() {
  const app = express();

  app.use(helmet());
  app.use(cors());
  app.use(express.json({ limit: '2mb' }));

  app.get('/', (_request, response) => {
    response.json({
      service: 'Raqmi System Server',
      status: 'running',
      docs: '/api/health',
    });
  });

  app.use('/api/health', healthRouter);
  app.use('/api/modules', modulesRouter);
  app.use('/api/tenants', tenantsRouter);
  app.use('/api/licenses', licensesRouter);

  app.use((_request, response) => {
    response.status(404).json({ error: 'Route introuvable.' });
  });

  return app;
}
