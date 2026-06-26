import { Router } from 'express';
import { tenants } from '../data/in-memory-store';

export const tenantsRouter = Router();

tenantsRouter.get('/', (_request, response) => {
  response.json({ data: tenants });
});
