import { isPlausibleServerUrl, normalizeServerUrl } from './lib/serverUrl';

const TOKEN_KEY = 'raqmi_token';
const CONFIG_KEY = 'raqmi_server_url';
const DEFAULT_SERVER_URL = 'http://localhost:3000';

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  roleCode: string;
  tenant: { id: string; code: string; name: string };
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
  email?: string;
  phone?: string;
  address?: string;
  currency: string;
  dateFormat: string;
  numberFormat: string;
  timezone: string;
  paymentDelayDays: number;
  reminderDelayDays: number;
  brandPrimaryColor?: string;
  brandLogoUrl?: string;
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
    return request<{ user: Omit<AuthUser, 'tenant'> & { tenantId: string } }>('/api/v1/auth/me');
  },

  async fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
    return request<T>(path, init);
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

  async getAuditLogs() {
    return request<{ items: AuditLogDto[] }>('/api/v1/admin/audit-logs');
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
};
