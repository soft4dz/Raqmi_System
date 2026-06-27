import { Router } from 'express';
import { sites } from '../data/in-memory-store';

export const adminSitesRouter = Router();

adminSitesRouter.get('/', (_request, response) => {
  response.json({ data: sites });
});

adminSitesRouter.get('/:id', (request, response) => {
  const site = sites.find((item) => item.id === request.params.id);
  if (!site) {
    response.status(404).json({ error: 'Site introuvable.' });
    return;
  }
  response.json({ data: site });
});
