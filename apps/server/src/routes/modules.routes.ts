import { Router } from 'express';

const raqmiModules = [
  { code: 'administration', label: 'Administration & utilisateurs', family: 'core', commercial: false },
  { code: 'settings', label: 'Paramétrage global', family: 'core', commercial: false },
  { code: 'sites', label: 'Sites / unités', family: 'core', commercial: true },
  { code: 'daily_revenue', label: 'Recettes journalières', family: 'finance', commercial: true },
  { code: 'treasury', label: 'Trésorerie', family: 'finance', commercial: true },
  { code: 'billing', label: 'Facturation', family: 'finance', commercial: true },
  { code: 'receivables', label: 'Créances & recouvrement', family: 'finance', commercial: true },
  { code: 'contracts', label: 'Contrats & conventions', family: 'finance', commercial: true },
  { code: 'hr', label: 'Ressources humaines', family: 'hr', commercial: true },
  { code: 'payroll', label: 'Paie', family: 'hr', commercial: true },
  { code: 'stocks', label: 'Stocks', family: 'operations', commercial: true },
  { code: 'purchases', label: 'Achats', family: 'operations', commercial: true },
  { code: 'maintenance', label: 'Maintenance', family: 'operations', commercial: true },
  { code: 'ged', label: 'Gestion documentaire', family: 'operations', commercial: true },
  { code: 'parking', label: 'Parking', family: 'specific', commercial: true },
  { code: 'beach_pool', label: 'Plage & piscine', family: 'specific', commercial: true },
  { code: 'portmaster', label: 'PortMaster', family: 'specific', commercial: true },
  { code: 'quality', label: 'Qualité & réclamations', family: 'operations', commercial: true },
  { code: 'checklists', label: 'Checklists de contrôle', family: 'operations', commercial: true },
  { code: 'reports', label: 'Rapports & exports', family: 'system', commercial: true },
  { code: 'dashboards', label: 'Dashboards directionnels', family: 'system', commercial: true },
  { code: 'sync', label: 'Synchronisation', family: 'system', commercial: true },
  { code: 'cloud_storage', label: 'Stockage cloud', family: 'system', commercial: true },
];

export const modulesRouter = Router();

modulesRouter.get('/', (_req, res) => {
  res.json({ data: raqmiModules });
});
