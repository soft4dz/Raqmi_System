import { demoSites, getUserSiteIds, pushAudit } from './demo-stores.js';

const MAIN = 'demo-site-001';
const ANNEXE = 'demo-site-002';

export type DailyRevenue = {
  id: string;
  siteId: string;
  businessDate: string;
  amount: number;
  category: string;
  status: string;
  notes?: string;
};

export type Invoice = {
  id: string;
  number: string;
  clientName: string;
  status: string;
  issueDate: string;
  dueDate?: string;
  totalAmount: number;
  siteId?: string;
  lines?: Array<{ description: string; quantity: number; unitPrice: number; taxRate: number }>;
};

export type TreasuryMovement = {
  id: string;
  siteId: string;
  type: 'in' | 'out';
  account: string;
  amount: number;
  movementDate: string;
  label: string;
};

export type Employee = {
  id: string;
  matricule: string;
  firstName: string;
  lastName: string;
  siteId: string;
  department: string;
  status: string;
  hireDate: string;
};

export type Contract = {
  id: string;
  employeeId: string;
  contractType: string;
  startDate: string;
  endDate?: string;
  baseSalary: number;
};

export type Attendance = {
  id: string;
  employeeId: string;
  siteId: string;
  workDate: string;
  checkIn?: string;
  checkOut?: string;
  notes?: string;
};

export type Product = {
  id: string;
  code: string;
  name: string;
  unit: string;
  minStockLevel: number;
  active: boolean;
};

export type StockMovement = {
  id: string;
  productId: string;
  siteId: string;
  type: 'in' | 'out' | 'adjust';
  quantity: number;
  movementDate: string;
  reference?: string;
};

export type InventorySession = {
  id: string;
  siteId: string;
  sessionDate: string;
  status: string;
};

export type Document = {
  id: string;
  originalName: string;
  mimeType?: string;
  sizeBytes: number;
  uploadedAt: string;
  moduleCode?: string;
  siteId?: string;
};

const today = new Date().toISOString().slice(0, 10);

export const dailyRevenues: DailyRevenue[] = [
  {
    id: 'rev-001',
    siteId: MAIN,
    businessDate: today,
    amount: 125000,
    category: 'restaurant',
    status: 'validated',
    notes: 'Recette déjeuner',
  },
  {
    id: 'rev-002',
    siteId: ANNEXE,
    businessDate: today,
    amount: 48000,
    category: 'bar',
    status: 'draft',
    notes: 'Annexe plage',
  },
];

export const invoices: Invoice[] = [
  {
    id: 'inv-001',
    number: 'FAC-2026-001',
    clientName: 'Client Demo',
    status: 'draft',
    issueDate: today,
    dueDate: new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10),
    totalAmount: 35700,
    siteId: MAIN,
    lines: [{ description: 'Hébergement', quantity: 2, unitPrice: 15000, taxRate: 19 }],
  },
];

export const treasuryMovements: TreasuryMovement[] = [
  {
    id: 'tr-001',
    siteId: MAIN,
    type: 'in',
    account: 'cash',
    amount: 50000,
    movementDate: today,
    label: 'Encaissement demo',
  },
];

export const employees: Employee[] = [
  {
    id: 'emp-001',
    matricule: 'EMP-001',
    firstName: 'Karim',
    lastName: 'Benali',
    siteId: MAIN,
    department: 'Réception',
    status: 'active',
    hireDate: new Date(Date.now() - 365 * 86400000).toISOString().slice(0, 10),
  },
  {
    id: 'emp-002',
    matricule: 'EMP-002',
    firstName: 'Samira',
    lastName: 'Khelifi',
    siteId: ANNEXE,
    department: 'Restauration',
    status: 'active',
    hireDate: today,
  },
];

export const contracts: Contract[] = [
  {
    id: 'ctr-001',
    employeeId: 'emp-001',
    contractType: 'cdi',
    startDate: employees[0].hireDate,
    baseSalary: 65000,
  },
];

export const attendance: Attendance[] = [
  {
    id: 'att-001',
    employeeId: 'emp-001',
    siteId: MAIN,
    workDate: today,
    checkIn: '08:00',
    checkOut: '17:00',
  },
];

export const products: Product[] = [
  {
    id: 'prod-001',
    code: 'PROD-001',
    name: 'Serviette de bain',
    unit: 'u',
    minStockLevel: 50,
    active: true,
  },
  {
    id: 'prod-002',
    code: 'PROD-002',
    name: 'Shampoing accueil',
    unit: 'u',
    minStockLevel: 100,
    active: true,
  },
];

export const stockMovements: StockMovement[] = [
  {
    id: 'sm-001',
    productId: 'prod-001',
    siteId: MAIN,
    type: 'in',
    quantity: 200,
    movementDate: today,
    reference: 'INIT',
  },
];

export const inventories: InventorySession[] = [
  {
    id: 'inv-s-001',
    siteId: MAIN,
    sessionDate: today,
    status: 'open',
  },
];

export const documents: Document[] = [
  {
    id: 'doc-001',
    originalName: 'contrat-demo.pdf',
    mimeType: 'application/pdf',
    sizeBytes: 245000,
    uploadedAt: new Date().toISOString(),
    moduleCode: 'ged',
    siteId: MAIN,
  },
];

function defaultSite(siteId?: string | null): string {
  return siteId && demoSites.some((s) => s.id === siteId) ? siteId : MAIN;
}

export function filterBySite<T extends { siteId?: string }>(
  items: T[],
  siteId: string | null | undefined,
  userId: string,
  roleCode: string,
): T[] {
  const allowed = roleCode === 'admin' ? null : getUserSiteIds(userId);
  let result = items;
  if (allowed) result = result.filter((i) => !i.siteId || allowed.includes(i.siteId));
  if (siteId) result = result.filter((i) => !i.siteId || i.siteId === siteId);
  return result;
}

export function filterEmployeesBySite(
  siteId: string | null | undefined,
  userId: string,
  roleCode: string,
) {
  return filterBySite(employees, siteId, userId, roleCode);
}

export function stockLevels() {
  const levels = new Map<string, number>();
  for (const m of stockMovements) {
    const delta = m.type === 'out' ? -m.quantity : m.quantity;
    levels.set(m.productId, (levels.get(m.productId) ?? 0) + delta);
  }
  return [...levels.entries()].map(([productId, quantity]) => ({ productId, quantity }));
}

export function treasuryBalance(filtered: TreasuryMovement[]) {
  return filtered.reduce((sum, m) => sum + (m.type === 'in' ? m.amount : -m.amount), 0);
}

export {
  defaultSite,
  MAIN as defaultSiteId,
  pushAudit,
};
