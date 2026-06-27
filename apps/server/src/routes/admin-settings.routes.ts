import { Router } from 'express';
import { addAuditLog, settings } from '../data/in-memory-store';

export const adminSettingsRouter = Router();

adminSettingsRouter.get('/', (request, response) => {
  const tenantId = String(request.query.tenantId ?? 'tenant-demo-hotel');
  response.json({ data: settings.filter((item) => item.tenantId === tenantId) });
});

adminSettingsRouter.patch('/:key', (request, response) => {
  const tenantId = String(request.body?.tenantId ?? 'tenant-demo-hotel');
  const key = request.params.key;
  const setting = settings.find((item) => item.tenantId === tenantId && item.key === key);

  if (!setting) {
    response.status(404).json({ error: 'Paramètre introuvable.' });
    return;
  }

  setting.value = String(request.body?.value ?? setting.value);
  addAuditLog({
    tenantId,
    moduleCode: 'settings',
    action: 'admin.setting.update',
    entityType: 'setting',
    entityId: key,
    description: `Paramètre modifié: ${key}`,
  });

  response.json({ data: setting });
});
