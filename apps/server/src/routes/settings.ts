import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth.js';
import { requireModule } from '../middleware/require-module.js';

let settings = {
  legalName: 'Hotel Demo Raqmi SARL',
  email: 'contact@demo.raqmi.local',
  phone: '+213 555 000 000',
  address: '12 Rue Didouche Mourad, Alger',
  currency: 'DZD',
  dateFormat: 'dd/MM/yyyy',
  numberFormat: 'fr-DZ',
  timezone: 'Africa/Algiers',
  paymentDelayDays: 30,
  reminderDelayDays: 7,
  brandPrimaryColor: '#2563eb',
  brandLogoUrl: null as string | null,
};

export const settingsRoutes = new Hono();

settingsRoutes.use('*', authMiddleware);
settingsRoutes.use('*', requireModule('settings'));

settingsRoutes.get('/', (c) => c.json(settings));

settingsRoutes.patch('/', async (c) => {
  if (c.get('roleCode') !== 'admin') return c.json({ error: 'Accès réservé aux administrateurs' }, 403);
  const body = await c.req.json<Partial<typeof settings>>();
  settings = { ...settings, ...body };
  return c.json(settings);
});
