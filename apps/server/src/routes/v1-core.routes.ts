import { Router } from 'express';
import { licenses, tenants } from '../data/in-memory-store';

const moduleCatalog = [
  { code: 'administration', label: 'Administration', family: 'core', commercial: false, description: 'Utilisateurs, roles et parametres.' },
  { code: 'settings', label: 'Parametrage', family: 'core', commercial: false, description: 'Parametres generaux.' },
  { code: 'sites', label: 'Sites', family: 'core', commercial: true, description: 'Gestion sites et unites.' },
  { code: 'daily_revenue', label: 'Recettes journalieres', family: 'finance', commercial: true, description: 'Saisie du chiffre d affaires.' },
  { code: 'treasury', label: 'Tresorerie', family: 'finance', commercial: true, description: 'Caisses et banques.' },
  { code: 'billing', label: 'Facturation', family: 'finance', commercial: true, description: 'Factures et avoirs.' },
  { code: 'receivables', label: 'Creances', family: 'finance', commercial: true, description: 'Recouvrement et relances.' },
  { code: 'hr', label: 'RH', family: 'hr', commercial: true, description: 'Personnel et affectations.' },
  { code: 'ged', label: 'GED', family: 'operations', commercial: true, description: 'Documents et pieces jointes.' },
  { code: 'reports', label: 'Rapports', family: 'system', commercial: true, description: 'Exports et editions.' },
  { code: 'dashboards', label: 'Dashboards', family: 'system', commercial: true, description: 'Pilotage directionnel.' },
];

export const v1CoreRouter = Router();

v1CoreRouter.get('/modules', (_request, response) => {
  const license = licenses[0];
  response.json({
    modules: moduleCatalog.map((item) => ({
      ...item,
      enabled: !item.commercial || license.allowedModules.includes(item.code),
    })),
  });
});

v1CoreRouter.get('/license/status', (_request, response) => {
  const license = licenses[0];
  const tenant = tenants.find((item) => item.id === license.tenantId) ?? tenants[0];
  const now = new Date();
  const expiresAt = new Date(license.expiresAt);
  const valid = license.status === 'active' && now <= expiresAt;

  response.json({
    tenant: { id: tenant.id, code: tenant.code, name: tenant.name },
    license: {
      kind: license.kind,
      expiresAt: license.expiresAt,
      allowedModules: license.allowedModules,
    },
    evaluation: {
      valid,
      readonlyMode: !valid,
      reason: valid ? undefined : 'Licence inactive ou expiree',
    },
    pack: {
      label: license.kind,
      description: 'Pack demo connecte au socle Core/Admin.',
    },
  });
});
