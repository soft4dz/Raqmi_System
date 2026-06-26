import { Router } from 'express';
import { licenses } from '../data/in-memory-store';

export const licensesRouter = Router();

licensesRouter.get('/', (_request, response) => {
  response.json({ data: licenses });
});

licensesRouter.get('/:id', (request, response) => {
  const license = licenses.find((item) => item.id === request.params.id);
  if (!license) {
    response.status(404).json({ error: 'Licence introuvable.' });
    return;
  }
  response.json({ data: license });
});

licensesRouter.get('/:id/modules/:moduleCode/check', (request, response) => {
  const license = licenses.find((item) => item.id === request.params.id);
  if (!license) {
    response.status(404).json({ error: 'Licence introuvable.' });
    return;
  }

  const moduleCode = request.params.moduleCode;
  const allowed = license.status === 'active' && license.allowedModules.includes(moduleCode);

  response.json({
    data: {
      licenseId: license.id,
      moduleCode,
      allowed,
      reason: allowed ? null : 'Module non inclus dans la licence ou licence inactive.',
    },
  });
});
