import { Router } from 'express';
import { addAuditLog, users } from '../data/in-memory-store';

export const adminUsersRouter = Router();

adminUsersRouter.get('/', (_request, response) => {
  response.json({ data: users });
});

adminUsersRouter.get('/:id', (request, response) => {
  const user = users.find((item) => item.id === request.params.id);
  if (!user) {
    response.status(404).json({ error: 'Utilisateur introuvable.' });
    return;
  }
  response.json({ data: user });
});

adminUsersRouter.patch('/:id/status', (request, response) => {
  const user = users.find((item) => item.id === request.params.id);
  if (!user) {
    response.status(404).json({ error: 'Utilisateur introuvable.' });
    return;
  }

  user.active = Boolean(request.body?.active);
  addAuditLog({
    tenantId: user.tenantId,
    userId: user.id,
    moduleCode: 'administration',
    action: 'admin.user.status.update',
    entityType: 'user',
    entityId: user.id,
    description: `Statut utilisateur modifié: ${user.active ? 'actif' : 'inactif'}`,
  });

  response.json({ data: user });
});
