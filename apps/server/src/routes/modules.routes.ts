import { Router } from 'express';

export const modulesRouter = Router();

modulesRouter.get('/', (_request, response) => {
  response.json({
    data: [
      { code: 'administration', label: 'Administration', family: 'core', commercial: false },
      { code: 'settings', label: 'Parametrage', family: 'core', commercial: false },
      { code: 'sites', label: 'Sites', family: 'core', commercial: true },
      { code: 'daily_revenue', label: 'Recettes journalieres', family: 'finance', commercial: true },
      { code: 'treasury', label: 'Tresorerie', family: 'finance', commercial: true },
      { code: 'billing', label: 'Facturation', family: 'finance', commercial: true },
      { code: 'receivables', label: 'Creances', family: 'finance', commercial: true },
      { code: 'contracts', label: 'Contrats', family: 'finance', commercial: true },
      { code: 'hr', label: 'Ressources humaines', family: 'hr', commercial: true },
      { code: 'stocks', label: 'Stocks', family: 'operations', commercial: true },
      { code: 'purchases', label: 'Achats', family: 'operations', commercial: true },
      { code: 'maintenance', label: 'Maintenance', family: 'operations', commercial: true },
      { code: 'ged', label: 'GED', family: 'operations', commercial: true },
      { code: 'portmaster', label: 'PortMaster', family: 'specific', commercial: true },
      { code: 'reports', label: 'Rapports', family: 'system', commercial: true },
      { code: 'dashboards', label: 'Dashboards', family: 'system', commercial: true },
      { code: 'sync', label: 'Synchronisation', family: 'system', commercial: true },
      { code: 'cloud_storage', label: 'Stockage cloud', family: 'system', commercial: true },
    ],
  });
});
