export type TenantSettings = {
  legalName: string;
  tradeName: string;
  legalForm: string;
  registrationNumber: string;
  taxId: string;
  statisticalId: string;
  vatArticle: string;
  activitySector: string;
  email: string;
  phone: string;
  website: string;
  address: string;
  city: string;
  wilaya: string;
  postalCode: string;
  country: string;
  currency: string;
  dateFormat: string;
  numberFormat: string;
  timezone: string;
  paymentDelayDays: number;
  reminderDelayDays: number;
  invoicePrefix: string;
  quotePrefix: string;
  nextInvoiceNumber: number;
  fiscalYearStartMonth: number;
  defaultVatRate: number;
  invoiceFooter: string;
  acceptedPaymentMethods: string;
  brandPrimaryColor: string;
  brandLogoUrl: string | null;
  brandSecondaryColor: string;
  sessionTimeoutMinutes: number;
  passwordMinLength: number;
  forcePasswordChangeDays: number;
  storageDriver: string;
  maxUploadSizeMb: number;
  backupFrequency: string;
  backupRetentionDays: number;
  updatedAt: string;
};

export const defaultTenantSettings = (): TenantSettings => ({
  legalName: 'Hotel Demo Raqmi SARL',
  tradeName: 'Hôtel Demo Raqmi',
  legalForm: 'sarl',
  registrationNumber: '16/00-1234567B23',
  taxId: '123456789012345',
  statisticalId: '1234567890',
  vatArticle: 'A123456789',
  activitySector: 'Hôtellerie & restauration',
  email: 'contact@demo.raqmi.local',
  phone: '+213 555 000 000',
  website: 'https://demo.raqmi.io',
  address: '12 Rue Didouche Mourad',
  city: 'Alger',
  wilaya: 'Alger',
  postalCode: '16000',
  country: 'Algérie',
  currency: 'DZD',
  dateFormat: 'dd/MM/yyyy',
  numberFormat: 'fr-DZ',
  timezone: 'Africa/Algiers',
  paymentDelayDays: 30,
  reminderDelayDays: 7,
  invoicePrefix: 'FAC',
  quotePrefix: 'DEV',
  nextInvoiceNumber: 42,
  fiscalYearStartMonth: 1,
  defaultVatRate: 19,
  invoiceFooter: 'Merci de votre confiance. Paiement par virement bancaire — RIP à communiquer sur demande.',
  acceptedPaymentMethods: 'virement,espèces,chèque,carte',
  brandPrimaryColor: '#2563eb',
  brandLogoUrl: null,
  brandSecondaryColor: '#0f766e',
  sessionTimeoutMinutes: 480,
  passwordMinLength: 8,
  forcePasswordChangeDays: 90,
  storageDriver: 'local',
  maxUploadSizeMb: 25,
  backupFrequency: 'daily',
  backupRetentionDays: 30,
  updatedAt: new Date().toISOString(),
});
