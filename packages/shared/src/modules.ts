export type RaqmiModuleCode =
  | 'administration'
  | 'settings'
  | 'sites'
  | 'daily_revenue'
  | 'treasury'
  | 'billing'
  | 'receivables'
  | 'contracts'
  | 'hr'
  | 'payroll'
  | 'stocks'
  | 'purchases'
  | 'maintenance'
  | 'ged'
  | 'parking'
  | 'beach_pool'
  | 'portmaster'
  | 'quality'
  | 'checklists'
  | 'reports'
  | 'dashboards'
  | 'sync'
  | 'cloud_storage';

export interface RaqmiModuleDefinition {
  code: RaqmiModuleCode;
  label: string;
  family: 'core' | 'finance' | 'hr' | 'operations' | 'specific' | 'system';
  commercial: boolean;
  description: string;
}

export const RAQMI_MODULES: RaqmiModuleDefinition[] = [
  { code: 'administration', label: 'Administration & utilisateurs', family: 'core', commercial: false, description: 'Utilisateurs, rôles et permissions.' },
  { code: 'settings', label: 'Paramétrage global', family: 'core', commercial: false, description: 'Paramètres entreprise, devise, délais, branding.' },
  { code: 'sites', label: 'Sites / unités', family: 'core', commercial: true, description: 'Gestion multi-sites, hôtels, unités, agences ou structures.' },
  { code: 'daily_revenue', label: 'Recettes journalières', family: 'finance', commercial: true, description: 'Saisie et validation du chiffre d’affaires quotidien.' },
  { code: 'treasury', label: 'Trésorerie', family: 'finance', commercial: true, description: 'Encaissements, caisse, banques et rapprochements.' },
  { code: 'billing', label: 'Facturation', family: 'finance', commercial: true, description: 'Factures, avoirs, paiements et exports.' },
  { code: 'receivables', label: 'Créances & recouvrement', family: 'finance', commercial: true, description: 'Balance âgée, relances, litiges et recouvrement.' },
  { code: 'contracts', label: 'Contrats & conventions', family: 'finance', commercial: true, description: 'Contrats clients, conventions, tarifs contractuels.' },
  { code: 'hr', label: 'Ressources humaines', family: 'hr', commercial: true, description: 'Employés, contrats, affectations, absences, pointage.' },
  { code: 'payroll', label: 'Paie', family: 'hr', commercial: true, description: 'Préparation paie, variables, retenues et états.' },
  { code: 'stocks', label: 'Stocks', family: 'operations', commercial: true, description: 'Produits, mouvements, seuils et inventaires.' },
  { code: 'purchases', label: 'Achats', family: 'operations', commercial: true, description: 'Fournisseurs, demandes, bons de commande, réception.' },
  { code: 'maintenance', label: 'Maintenance', family: 'operations', commercial: true, description: 'Interventions, équipements et maintenance préventive.' },
  { code: 'ged', label: 'Gestion documentaire', family: 'operations', commercial: true, description: 'Documents, pièces jointes et archivage.' },
  { code: 'parking', label: 'Parking', family: 'specific', commercial: true, description: 'Tickets, abonnements, encaissements parking.' },
  { code: 'beach_pool', label: 'Plage & piscine', family: 'specific', commercial: true, description: 'Accès, ventes, équipements et suivi exploitation.' },
  { code: 'portmaster', label: 'PortMaster', family: 'specific', commercial: true, description: 'Port de plaisance, bateaux, contrats, amarrages.' },
  { code: 'quality', label: 'Qualité & réclamations', family: 'operations', commercial: true, description: 'Réclamations, actions correctives, indicateurs qualité.' },
  { code: 'checklists', label: 'Checklists de contrôle', family: 'operations', commercial: true, description: 'Contrôles terrain, preuves, plans d’action.' },
  { code: 'reports', label: 'Rapports & exports', family: 'system', commercial: true, description: 'Rapports PDF/Excel, états standards et exports.' },
  { code: 'dashboards', label: 'Dashboards directionnels', family: 'system', commercial: true, description: 'KPI, tableaux de bord et pilotage directionnel.' },
  { code: 'sync', label: 'Synchronisation', family: 'system', commercial: true, description: 'Synchronisation multi-postes ou cloud.' },
  { code: 'cloud_storage', label: 'Stockage cloud', family: 'system', commercial: true, description: 'Upload cloud des fichiers, sauvegardes et archives.' },
];

export function isKnownRaqmiModule(code: string): code is RaqmiModuleCode {
  return RAQMI_MODULES.some((module) => module.code === code);
}
