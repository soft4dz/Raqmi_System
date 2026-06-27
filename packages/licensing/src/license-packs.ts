import type { RaqmiModuleCode } from '@raqmi/shared';
import type { LicenseKind, LicenseLimits } from './license-types';

export interface LicensePackDefinition {
  kind: Exclude<LicenseKind, 'trial' | 'custom'>;
  label: string;
  description: string;
  defaultLimits: LicenseLimits;
  modules: RaqmiModuleCode[];
}

export const RAQMI_LICENSE_PACKS: LicensePackDefinition[] = [
  {
    kind: 'starter',
    label: 'Starter',
    description: 'Pack pour petite structure avec modules essentiels.',
    defaultLimits: {
      maxUsers: 10,
      maxSites: 1,
      maxStorageGb: 10,
      offlineGraceDays: 15,
    },
    modules: ['sites', 'daily_revenue', 'billing', 'reports', 'dashboards'],
  },
  {
    kind: 'professional',
    label: 'Professional',
    description: 'Pack hôtel ou entreprise moyenne avec finance, RH et GED.',
    defaultLimits: {
      maxUsers: 50,
      maxSites: 5,
      maxStorageGb: 100,
      offlineGraceDays: 30,
    },
    modules: [
      'sites',
      'daily_revenue',
      'treasury',
      'billing',
      'receivables',
      'contracts',
      'hr',
      'stocks',
      'purchases',
      'ged',
      'reports',
      'dashboards',
    ],
  },
  {
    kind: 'enterprise',
    label: 'Enterprise',
    description: 'Pack multi-sites complet avec exploitation avancée, cloud et synchronisation.',
    defaultLimits: {
      maxUsers: 250,
      maxSites: 25,
      maxStorageGb: 1000,
      offlineGraceDays: 45,
    },
    modules: [
      'sites',
      'daily_revenue',
      'treasury',
      'billing',
      'receivables',
      'contracts',
      'hr',
      'payroll',
      'stocks',
      'purchases',
      'maintenance',
      'ged',
      'parking',
      'beach_pool',
      'portmaster',
      'quality',
      'checklists',
      'reports',
      'dashboards',
      'sync',
      'cloud_storage',
    ],
  },
];
