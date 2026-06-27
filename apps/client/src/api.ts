const TOKEN_KEY = 'raqmi_token';

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

function authHeaders() {
  const token = localStorage.getItem(TOKEN_KEY);
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
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
};
