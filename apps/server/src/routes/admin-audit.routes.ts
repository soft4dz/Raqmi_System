import { Router } from 'express';
import { auditLogs } from '../data/in-memory-store';

export const adminAuditRouter = Router();

adminAuditRouter.get('/', (request, response) => {
  const tenantId = request.query.tenantId ? String(request.query.tenantId) : null;
  const moduleCode = request.query.moduleCode ? String(request.query.moduleCode) : null;

  const data = auditLogs.filter((item) => {
    if (tenantId && item.tenantId !== tenantId) return false;
    if (moduleCode && item.moduleCode !== moduleCode) return false;
    return true;
  });

  response.json({ data });
});
