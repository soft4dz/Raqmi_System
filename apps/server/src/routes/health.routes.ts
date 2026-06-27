import { Router } from 'express';

export const healthRouter = Router();

healthRouter.get('/', (_request, response) => {
  response.json({
    ok: true,
    service: 'Raqmi System Server',
    version: '0.1.0',
    timestamp: new Date().toISOString(),
  });
});
