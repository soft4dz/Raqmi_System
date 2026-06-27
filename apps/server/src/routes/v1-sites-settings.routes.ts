import { Router } from 'express';
import { settings, sites } from '../data/in-memory-store';

export const v1SitesRouter = Router();

v1SitesRouter.get('/', (_request, response) => {
  response.json({ items: sites });
});

export const v1SettingsRouter = Router();

v1SettingsRouter.get('/', (_request, response) => {
  const map = Object.fromEntries(settings.map((item) => [item.key, item.value]));
  response.json({
    legalName: map['company.name'] ?? 'Client Demo',
    email: map['company.email'] ?? '',
    phone: map['company.phone'] ?? '',
    address: map['company.address'] ?? '',
    currency: map['company.currency'] ?? 'DZD',
    dateFormat: map['system.dateFormat'] ?? 'dd/MM/yyyy',
    numberFormat: map['system.numberFormat'] ?? 'fr-DZ',
    timezone: map['system.timezone'] ?? 'Africa/Algiers',
    paymentDelayDays: Number(map['finance.paymentDelayDays'] ?? 15),
    reminderDelayDays: Number(map['finance.reminderDelayDays'] ?? 7),
    brandPrimaryColor: map['appearance.primaryColor'] ?? '#1d4ed8',
    brandLogoUrl: map['appearance.logoUrl'] ?? '',
  });
});
