import { isPlausibleServerUrl, normalizeServerUrl } from './lib/serverUrl';
import { getActiveSiteId } from './lib/activeSite';

const TOKEN_KEY = 'raqmi_token';
const CONFIG_KEY = 'raqmi_server_url';
const DEFAULT_SERVER_URL = 'http://localhost:3000';

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  roleCode: string;
  tenant: { id: string; code: string; name: string };
  permissions?: string[];
  siteIds?: string[];
}

export interface ModuleItem {
  code: string;
  label: string;
  family: string;
  commercial: boolean;
  description: string;
  enabled: boolean;
}

export interface LicenseStatusResponse {
  tenant: { id: string; code: string; name: string };
  license: { kind: string; expiresAt: string; allowedModules: string[] };
  evaluation: { valid: boolean; readonlyMode: boolean; reason?: string };
  pack?: { label: string; description: string };
}

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  roleCode: string;
  active: boolean;
  siteIds: string[];
}

export interface SiteDto {
  id: string;
  code: string;
  name: string;
  type?: string;
  city?: string;
  active: boolean;
}

export interface TenantSettingsDto {
  legalName?: string;
  tradeName?: string;
  legalForm?: string;
  registrationNumber?: string;
  taxId?: string;
  statisticalId?: string;
  vatArticle?: string;
  activitySector?: string;
  email?: string;
  phone?: string;
  website?: string;
  address?: string;
  city?: string;
  wilaya?: string;
  postalCode?: string;
  country?: string;
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
  invoiceFooter?: string;
  acceptedPaymentMethods?: string;
  brandPrimaryColor?: string;
  brandSecondaryColor?: string;
  brandLogoUrl?: string;
  sessionTimeoutMinutes: number;
  passwordMinLength: number;
  forcePasswordChangeDays: number;
  storageDriver: string;
  maxUploadSizeMb: number;
  backupFrequency: string;
  backupRetentionDays: number;
  updatedAt?: string;
}

export interface RoleDto {
  code: string;
  label: string;
  isSystem?: boolean;
  permissions: string[];
}

export interface AuditLogDto {
  id: string;
  userId?: string;
  action: string;
  moduleCode?: string;
  entityType?: string;
  entityId?: string;
  description: string;
  createdAt: string;
}

export interface DailyRevenueDto {
  id: string;
  siteId: string;
  businessDate: string;
  amount: number;
  category: string;
  status: string;
  notes?: string;
}

export interface InvoiceLineDto {
  description: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
}

export interface InvoiceDto {
  id: string;
  number: string;
  clientName: string;
  status: string;
  issueDate: string;
  totalAmount: number;
}

export interface TreasuryMovementDto {
  id: string;
  type: string;
  account: string;
  amount: number;
  movementDate: string;
  label: string;
}

export interface EmployeeDto {
  id: string;
  matricule: string;
  firstName: string;
  lastName: string;
  department: string;
  status: string;
}

export interface ContractDto {
  id: string;
  employeeId: string;
  contractType: string;
  startDate: string;
  baseSalary: number;
}

export interface AttendanceDto {
  id: string;
  employeeId: string;
  workDate: string;
  checkIn?: string;
  checkOut?: string;
  notes?: string;
}

export interface ProductDto {
  id: string;
  code: string;
  name: string;
  unit: string;
  minStockLevel: number;
}

export interface StockMovementDto {
  id: string;
  productId: string;
  type: string;
  quantity: number;
  movementDate: string;
}

export interface StockLevelDto {
  productId: string;
  quantity: number;
}

export interface InventoryDto {
  id: string;
  siteId: string;
  sessionDate: string;
  status: string;
}

export interface DocumentDto {
  id: string;
  originalName: string;
  mimeType?: string;
  sizeBytes: number;
  uploadedAt: string;
  moduleCode?: string;
}

let cachedServerUrl: string | null = null;

export async function getServerUrl(): Promise<string> {
  if (cachedServerUrl) return cachedServerUrl;

  if (window.raqmi?.getConfig) {
    const config = await window.raqmi.getConfig();
    cachedServerUrl = config.serverUrl || DEFAULT_SERVER_URL;
    return cachedServerUrl;
  }

  cachedServerUrl = localStorage.getItem(CONFIG_KEY) || DEFAULT_SERVER_URL;
  return cachedServerUrl;
}

export async function setServerUrl(serverUrl: string): Promise<void> {
  const normalized = normalizeServerUrl(serverUrl);
  if (!isPlausibleServerUrl(normalized)) {
    throw new Error('Adresse serveur invalide');
  }
  cachedServerUrl = normalized;
  localStorage.setItem(CONFIG_KEY, cachedServerUrl);
  if (window.raqmi?.setConfig) {
    await window.raqmi.setConfig({ serverUrl: cachedServerUrl });
  }
}

export async function testServerUrl(serverUrl: string): Promise<boolean> {
  const normalized = normalizeServerUrl(serverUrl);
  if (!isPlausibleServerUrl(normalized)) return false;
  if (window.raqmi?.testServer) {
    return window.raqmi.testServer(normalized);
  }
  const response = await fetch(`${normalized}/health`);
  return response.ok;
}

export { normalizeServerUrl };

function withSiteQuery(path: string): string {
  const siteId = getActiveSiteId();
  if (!siteId) return path;
  const sep = path.includes('?') ? '&' : '?';
  return `${path}${sep}siteId=${encodeURIComponent(siteId)}`;
}

function withSiteBody<T extends object>(body: T): T & { siteId?: string } {
  const siteId = getActiveSiteId();
  return siteId ? { ...body, siteId } : body;
}

function authHeaders(): Record<string, string> {
  const token = localStorage.getItem(TOKEN_KEY);
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const serverUrl = await getServerUrl();
  const response = await fetch(`${serverUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...authHeaders(),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error ?? `Erreur ${response.status}`);
  }

  if (response.status === 204) return undefined as T;

  return response.json() as Promise<T>;
}

export const api = {
  getToken() {
    return localStorage.getItem(TOKEN_KEY);
  },

  setToken(token: string | null) {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  },

  async login(email: string, password: string) {
    return request<{ token: string; user: AuthUser }>('/api/v1/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
  },

  async getModules() {
    return request<{ modules: ModuleItem[] }>('/api/v1/modules');
  },

  async getLicenseStatus() {
    return request<LicenseStatusResponse>('/api/v1/license/status');
  },

  async getMe() {
    return request<{ user: Omit<AuthUser, 'tenant'> & { tenantId: string; permissions?: string[]; siteIds?: string[] } }>('/api/v1/auth/me');
  },

  async fetchJson<T>(path: string, init?: RequestInit, useSite = true): Promise<T> {
    const finalPath = useSite && (!init?.method || init.method === 'GET') ? withSiteQuery(path) : path;
    return request<T>(finalPath, init);
  },

  async getUsers() {
    return request<{ items: UserDto[] }>('/api/v1/admin/users');
  },

  async getAdminSites() {
    return request<{ items: SiteDto[] }>('/api/v1/admin/sites');
  },

  async getRoles() {
    return request<{ items: RoleDto[] }>('/api/v1/admin/roles');
  },

  async createUser(body: { email: string; fullName: string; roleCode: string; password?: string; siteIds?: string[] }) {
    return request<UserDto>('/api/v1/admin/users', {
      method: 'POST',
      body: JSON.stringify(body),
    });
  },

  async updateUser(id: string, body: { fullName?: string; roleCode?: string; active?: boolean; siteIds?: string[]; password?: string }) {
    return request<UserDto>(`/api/v1/admin/users/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    });
  },

  async getSites() {
    return request<{ items: SiteDto[] }>('/api/v1/sites');
  },

  async createSite(body: { code: string; name: string; type?: string; city?: string }) {
    return request<SiteDto>('/api/v1/sites', { method: 'POST', body: JSON.stringify(body) });
  },

  async updateSite(id: string, body: { name?: string; type?: string; city?: string; active?: boolean }) {
    return request<SiteDto>(`/api/v1/sites/${id}`, { method: 'PATCH', body: JSON.stringify(body) });
  },

  async getTenantSettings() {
    return request<TenantSettingsDto>('/api/v1/settings');
  },

  async updateTenantSettings(body: Partial<TenantSettingsDto>) {
    return request<TenantSettingsDto>('/api/v1/settings', { method: 'PATCH', body: JSON.stringify(body) });
  },

  async getAuditLogs(filters?: { action?: string; moduleCode?: string; q?: string }) {
    const params = new URLSearchParams();
    if (filters?.action) params.set('action', filters.action);
    if (filters?.moduleCode) params.set('moduleCode', filters.moduleCode);
    if (filters?.q) params.set('q', filters.q);
    const qs = params.toString();
    return request<{ items: AuditLogDto[] }>(`/api/v1/admin/audit-logs${qs ? `?${qs}` : ''}`);
  },

  async getPermissions() {
    return request<{ items: { key: string; label: string; module: string }[] }>('/api/v1/admin/permissions');
  },

  async createRole(body: { code: string; label: string; permissions: string[] }) {
    return request<RoleDto>('/api/v1/admin/roles', { method: 'POST', body: JSON.stringify(body) });
  },

  async updateRole(code: string, body: { label?: string; permissions?: string[] }) {
    return request<RoleDto>(`/api/v1/admin/roles/${code}`, { method: 'PATCH', body: JSON.stringify(body) });
  },

  async deleteRole(code: string) {
    return request<void>(`/api/v1/admin/roles/${code}`, { method: 'DELETE' });
  },

  async getDailyRevenue() {
    return request<{ items: DailyRevenueDto[] }>(withSiteQuery('/api/v1/finance/daily-revenue'));
  },

  async createDailyRevenue(body: { amount: number; category?: string; notes?: string }) {
    return request<DailyRevenueDto>(withSiteQuery('/api/v1/finance/daily-revenue'), {
      method: 'POST',
      body: JSON.stringify(withSiteBody(body)),
    });
  },

  async validateDailyRevenue(id: string) {
    return request<DailyRevenueDto>(`/api/v1/finance/daily-revenue/${id}/validate`, { method: 'PATCH', body: '{}' });
  },

  async getInvoices() {
    return request<{ items: InvoiceDto[] }>(withSiteQuery('/api/v1/finance/invoices'));
  },

  async createInvoice(body: { clientName: string; lines?: InvoiceLineDto[] }) {
    return request<InvoiceDto>(withSiteQuery('/api/v1/finance/invoices'), {
      method: 'POST',
      body: JSON.stringify(withSiteBody(body)),
    });
  },

  async updateInvoice(id: string, body: { status?: string; clientName?: string }) {
    return request<InvoiceDto>(`/api/v1/finance/invoices/${id}`, { method: 'PATCH', body: JSON.stringify(body) });
  },

  async getTreasury() {
    return request<{ items: TreasuryMovementDto[]; balance: number }>(withSiteQuery('/api/v1/finance/treasury/movements'));
  },

  async createTreasuryMovement(body: { type: 'in' | 'out'; amount: number; label?: string; account?: string }) {
    return request<TreasuryMovementDto>(withSiteQuery('/api/v1/finance/treasury/movements'), {
      method: 'POST',
      body: JSON.stringify(withSiteBody(body)),
    });
  },

  async getEmployees(search?: string) {
    const base = withSiteQuery('/api/v1/hr/employees');
    const path = search ? `${base}${base.includes('?') ? '&' : '?'}search=${encodeURIComponent(search)}` : base;
    return request<{ items: EmployeeDto[] }>(path);
  },

  async createEmployee(body: { firstName: string; lastName: string; department?: string; matricule?: string }) {
    return request<EmployeeDto>(withSiteQuery('/api/v1/hr/employees'), {
      method: 'POST',
      body: JSON.stringify(withSiteBody(body)),
    });
  },

  async getContracts(employeeId: string) {
    return request<{ items: ContractDto[] }>(`/api/v1/hr/employees/${employeeId}/contracts`);
  },

  async createContract(employeeId: string, body: { contractType?: string; baseSalary?: number }) {
    return request<ContractDto>(`/api/v1/hr/employees/${employeeId}/contracts`, {
      method: 'POST',
      body: JSON.stringify(body),
    });
  },

  async getAttendance(date?: string) {
    const base = withSiteQuery('/api/v1/hr/attendance');
    const path = date ? `${base}${base.includes('?') ? '&' : '?'}date=${encodeURIComponent(date)}` : base;
    return request<{ items: AttendanceDto[] }>(path);
  },

  async createAttendance(body: { employeeId: string; checkIn?: string; checkOut?: string; notes?: string }) {
    return request<AttendanceDto>(withSiteQuery('/api/v1/hr/attendance'), {
      method: 'POST',
      body: JSON.stringify(withSiteBody(body)),
    });
  },

  async getProducts() {
    return request<{ items: ProductDto[] }>('/api/v1/stocks/products');
  },

  async createProduct(body: { code?: string; name: string; unit?: string; minStockLevel?: number }) {
    return request<ProductDto>('/api/v1/stocks/products', { method: 'POST', body: JSON.stringify(body) });
  },

  async getStockMovements(productId?: string) {
    const base = withSiteQuery('/api/v1/stocks/movements');
    const path = productId ? `${base}${base.includes('?') ? '&' : '?'}productId=${encodeURIComponent(productId)}` : base;
    return request<{ items: StockMovementDto[]; stockByProduct: StockLevelDto[] }>(path);
  },

  async createStockMovement(body: { productId: string; type: 'in' | 'out'; quantity: number; reference?: string }) {
    return request<StockMovementDto>(withSiteQuery('/api/v1/stocks/movements'), {
      method: 'POST',
      body: JSON.stringify(withSiteBody(body)),
    });
  },

  async getInventories() {
    return request<{ items: InventoryDto[] }>(withSiteQuery('/api/v1/stocks/inventories'));
  },

  async createInventory() {
    return request<InventoryDto>(withSiteQuery('/api/v1/stocks/inventories'), {
      method: 'POST',
      body: JSON.stringify(withSiteBody({})),
    });
  },

  async getDocuments() {
    return request<{ items: DocumentDto[] }>(withSiteQuery('/api/v1/ged/documents'));
  },

  async uploadDocument(file: File) {
    const form = new FormData();
    form.append('file', file);
    const siteId = getActiveSiteId();
    if (siteId) form.append('siteId', siteId);
    const serverUrl = await getServerUrl();
    const response = await fetch(`${serverUrl}/api/v1/ged/documents`, {
      method: 'POST',
      headers: authHeaders(),
      body: form,
    });
    if (!response.ok) {
      const body = await response.json().catch(() => ({}));
      throw new Error(body.error ?? `Erreur ${response.status}`);
    }
    return response.json() as Promise<DocumentDto>;
  },

  async deleteDocument(id: string) {
    return request<void>(`/api/v1/ged/documents/${id}`, { method: 'DELETE' });
  },
};
