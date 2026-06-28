import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth.js';
import { requireModule } from '../middleware/require-module.js';
import {
  attendance,
  contracts,
  dailyRevenues,
  defaultSite,
  documents,
  employees,
  filterBySite,
  filterEmployeesBySite,
  inventories,
  invoices,
  products,
  pushAudit,
  stockLevels,
  stockMovements,
  treasuryBalance,
  treasuryMovements,
} from '../demo-business-stores.js';

export const businessRoutes = new Hono();

businessRoutes.use('*', authMiddleware);

function ctx(c: { get: (k: 'userId' | 'roleCode') => string; req: { query: (k: string) => string | undefined } }) {
  return {
    userId: c.get('userId'),
    roleCode: c.get('roleCode'),
    siteId: c.req.query('siteId') ?? null,
  };
}

// ── Finance ──────────────────────────────────────────────────────────

businessRoutes.get('/finance/daily-revenue', requireModule('daily_revenue'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const items = filterBySite(dailyRevenues, siteId, userId, roleCode);
  return c.json({ items });
});

businessRoutes.post('/finance/daily-revenue', requireModule('daily_revenue'), async (c) => {
  const body = await c.req.json<{ amount?: number; category?: string; notes?: string; siteId?: string }>();
  const entry = {
    id: crypto.randomUUID(),
    siteId: defaultSite(body.siteId ?? c.req.query('siteId')),
    businessDate: new Date().toISOString().slice(0, 10),
    amount: body.amount ?? 0,
    category: body.category ?? 'general',
    status: 'draft',
    notes: body.notes,
  };
  dailyRevenues.unshift(entry);
  pushAudit('create', 'daily_revenue', 'DailyRevenueEntry', entry.id, 'Recette créée', c.get('userId'));
  return c.json(entry);
});

businessRoutes.patch('/finance/daily-revenue/:id/validate', requireModule('daily_revenue'), (c) => {
  const entry = dailyRevenues.find((r) => r.id === c.req.param('id'));
  if (!entry) return c.json({ error: 'Introuvable' }, 404);
  entry.status = 'validated';
  return c.json(entry);
});

businessRoutes.get('/finance/invoices', requireModule('billing'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const items = filterBySite(invoices, siteId, userId, roleCode);
  return c.json({ items });
});

businessRoutes.post('/finance/invoices', requireModule('billing'), async (c) => {
  const body = await c.req.json<{ clientName?: string; number?: string; lines?: Array<{ description: string; quantity: number; unitPrice: number; taxRate: number }> }>();
  const lines = body.lines ?? [{ description: 'Prestation', quantity: 1, unitPrice: 10000, taxRate: 19 }];
  const totalAmount = lines.reduce((s, l) => s + l.quantity * l.unitPrice * (1 + l.taxRate / 100), 0);
  const invoice = {
    id: crypto.randomUUID(),
    number: body.number ?? `FAC-${Date.now()}`,
    clientName: body.clientName ?? 'Client',
    status: 'draft',
    issueDate: new Date().toISOString().slice(0, 10),
    dueDate: new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10),
    totalAmount,
    siteId: defaultSite(c.req.query('siteId')),
    lines,
  };
  invoices.unshift(invoice);
  pushAudit('create', 'billing', 'Invoice', invoice.id, `Facture ${invoice.number}`, c.get('userId'));
  return c.json(invoice);
});

businessRoutes.patch('/finance/invoices/:id', requireModule('billing'), async (c) => {
  const invoice = invoices.find((i) => i.id === c.req.param('id'));
  if (!invoice) return c.json({ error: 'Introuvable' }, 404);
  const body = await c.req.json<{ status?: string; clientName?: string }>();
  if (body.status) invoice.status = body.status;
  if (body.clientName) invoice.clientName = body.clientName;
  return c.json(invoice);
});

businessRoutes.get('/finance/treasury/movements', requireModule('treasury'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const items = filterBySite(treasuryMovements, siteId, userId, roleCode);
  return c.json({ items, balance: treasuryBalance(items) });
});

businessRoutes.post('/finance/treasury/movements', requireModule('treasury'), async (c) => {
  const body = await c.req.json<{ type?: 'in' | 'out'; account?: string; amount?: number; label?: string; siteId?: string }>();
  const movement = {
    id: crypto.randomUUID(),
    siteId: defaultSite(body.siteId ?? c.req.query('siteId')),
    type: body.type ?? 'in',
    account: body.account ?? 'cash',
    amount: body.amount ?? 0,
    movementDate: new Date().toISOString().slice(0, 10),
    label: body.label ?? 'Mouvement',
  };
  treasuryMovements.unshift(movement);
  return c.json(movement);
});

// ── HR ───────────────────────────────────────────────────────────────

businessRoutes.get('/hr/employees', requireModule('hr'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const search = c.req.query('search')?.toLowerCase();
  let items = filterEmployeesBySite(siteId, userId, roleCode);
  if (search) {
    items = items.filter(
      (e) =>
        e.firstName.toLowerCase().includes(search) ||
        e.lastName.toLowerCase().includes(search) ||
        e.matricule.toLowerCase().includes(search),
    );
  }
  return c.json({ items });
});

businessRoutes.post('/hr/employees', requireModule('hr'), async (c) => {
  const body = await c.req.json<{ firstName?: string; lastName?: string; department?: string; matricule?: string; siteId?: string }>();
  const employee = {
    id: crypto.randomUUID(),
    matricule: body.matricule ?? `EMP-${crypto.randomUUID().slice(0, 6)}`,
    firstName: body.firstName ?? '',
    lastName: body.lastName ?? '',
    siteId: defaultSite(body.siteId ?? c.req.query('siteId')),
    department: body.department ?? '',
    status: 'active',
    hireDate: new Date().toISOString().slice(0, 10),
  };
  employees.push(employee);
  pushAudit('create', 'hr', 'Employee', employee.id, `Employé ${employee.firstName} ${employee.lastName}`, c.get('userId'));
  return c.json(employee);
});

businessRoutes.get('/hr/employees/:id/contracts', requireModule('hr'), (c) => {
  const items = contracts.filter((x) => x.employeeId === c.req.param('id'));
  return c.json({ items });
});

businessRoutes.post('/hr/employees/:id/contracts', requireModule('hr'), async (c) => {
  const body = await c.req.json<{ contractType?: string; baseSalary?: number; startDate?: string }>();
  const contract = {
    id: crypto.randomUUID(),
    employeeId: c.req.param('id'),
    contractType: body.contractType ?? 'cdi',
    startDate: body.startDate ?? new Date().toISOString().slice(0, 10),
    baseSalary: body.baseSalary ?? 0,
  };
  contracts.push(contract);
  return c.json(contract);
});

businessRoutes.get('/hr/attendance', requireModule('hr'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const date = c.req.query('date') ?? new Date().toISOString().slice(0, 10);
  const items = filterBySite(attendance, siteId, userId, roleCode).filter((a) => a.workDate === date);
  return c.json({ items });
});

businessRoutes.post('/hr/attendance', requireModule('hr'), async (c) => {
  const body = await c.req.json<{ employeeId?: string; workDate?: string; checkIn?: string; checkOut?: string; notes?: string; siteId?: string }>();
  const record = {
    id: crypto.randomUUID(),
    employeeId: body.employeeId ?? employees[0]?.id ?? '',
    siteId: defaultSite(body.siteId ?? c.req.query('siteId')),
    workDate: body.workDate ?? new Date().toISOString().slice(0, 10),
    checkIn: body.checkIn,
    checkOut: body.checkOut,
    notes: body.notes,
  };
  attendance.unshift(record);
  return c.json(record);
});

// ── Stocks ─────────────────────────────────────────────────────────

businessRoutes.get('/stocks/products', requireModule('stocks'), (c) => {
  return c.json({ items: products.filter((p) => p.active) });
});

businessRoutes.post('/stocks/products', requireModule('stocks'), async (c) => {
  const body = await c.req.json<{ code?: string; name?: string; unit?: string; minStockLevel?: number }>();
  const product = {
    id: crypto.randomUUID(),
    code: body.code ?? `P-${crypto.randomUUID().slice(0, 6)}`,
    name: body.name ?? '',
    unit: body.unit ?? 'u',
    minStockLevel: body.minStockLevel ?? 0,
    active: true,
  };
  products.push(product);
  return c.json(product);
});

businessRoutes.get('/stocks/movements', requireModule('stocks'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const productId = c.req.query('productId');
  let items = filterBySite(stockMovements, siteId, userId, roleCode);
  if (productId) items = items.filter((m) => m.productId === productId);
  return c.json({ items, stockByProduct: stockLevels() });
});

businessRoutes.post('/stocks/movements', requireModule('stocks'), async (c) => {
  const body = await c.req.json<{ productId?: string; type?: 'in' | 'out'; quantity?: number; reference?: string; siteId?: string }>();
  const movement = {
    id: crypto.randomUUID(),
    productId: body.productId ?? products[0]?.id ?? '',
    siteId: defaultSite(body.siteId ?? c.req.query('siteId')),
    type: body.type ?? 'in',
    quantity: body.quantity ?? 0,
    movementDate: new Date().toISOString().slice(0, 10),
    reference: body.reference,
  };
  stockMovements.unshift(movement);
  return c.json(movement);
});

businessRoutes.get('/stocks/inventories', requireModule('stocks'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const items = filterBySite(inventories, siteId, userId, roleCode);
  return c.json({ items });
});

businessRoutes.post('/stocks/inventories', requireModule('stocks'), async (c) => {
  const body = await c.req.json<{ siteId?: string }>();
  const session = {
    id: crypto.randomUUID(),
    siteId: defaultSite(body.siteId ?? c.req.query('siteId')),
    sessionDate: new Date().toISOString().slice(0, 10),
    status: 'open',
  };
  inventories.unshift(session);
  return c.json(session);
});

// ── GED ──────────────────────────────────────────────────────────────

businessRoutes.get('/ged/documents', requireModule('ged'), (c) => {
  const { userId, roleCode, siteId } = ctx(c);
  const items = filterBySite(documents, siteId, userId, roleCode);
  return c.json({ items });
});

businessRoutes.post('/ged/documents', requireModule('ged'), async (c) => {
  const body = await c.req.parseBody();
  const file = body.file;
  if (!file || typeof file === 'string') return c.json({ error: 'Fichier requis' }, 400);
  const siteId = defaultSite(typeof body.siteId === 'string' ? body.siteId : c.req.query('siteId'));
  const doc = {
    id: crypto.randomUUID(),
    originalName: file.name,
    mimeType: file.type || undefined,
    sizeBytes: file.size,
    uploadedAt: new Date().toISOString(),
    moduleCode: 'ged',
    siteId,
  };
  documents.unshift(doc);
  pushAudit('create', 'ged', 'Document', doc.id, `Document uploadé : ${file.name}`, c.get('userId'));
  return c.json(doc);
});

businessRoutes.delete('/ged/documents/:id', requireModule('ged'), (c) => {
  const index = documents.findIndex((d) => d.id === c.req.param('id'));
  if (index < 0) return c.json({ error: 'Introuvable' }, 404);
  documents.splice(index, 1);
  return c.body(null, 204);
});
