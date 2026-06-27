import type { RaqmiModuleCode } from '@raqmi/shared';
import type { LicenseKind, LicenseMode, LicenseStatus } from '@raqmi/licensing/types';

export interface EditorTenant {
  id: string;
  code: string;
  name: string;
}

export interface EditorLicense {
  id: string;
  tenantId: string;
  kind: LicenseKind;
  mode: LicenseMode;
  status: LicenseStatus;
  startsAt: string;
  expiresAt: string;
  allowedModules: RaqmiModuleCode[];
  limits: {
    maxUsers: number;
    maxSites: number;
    maxStorageGb: number;
    offlineGraceDays: number;
  };
  serverFingerprint?: string;
}

export interface EditorData {
  tenants: EditorTenant[];
  licenses: EditorLicense[];
}

declare global {
  interface Window {
    raqmiEditor?: {
      loadData: () => Promise<EditorData>;
      saveData: (data: EditorData) => Promise<void>;
      ensureKeys: () => Promise<{ publicPath: string; privatePath: string }>;
      exportLicense: (payload: Omit<EditorLicense, 'id'> & { licenseId: string; tenantName: string }) => Promise<string>;
      saveLicenseFile: (content: string, suggestedName?: string) => Promise<string | null>;
    };
  }
}

export async function loadEditorData(): Promise<EditorData> {
  if (!window.raqmiEditor) {
    return { tenants: [], licenses: [] };
  }
  return window.raqmiEditor.loadData();
}

export async function saveEditorData(data: EditorData): Promise<void> {
  if (!window.raqmiEditor) return;
  await window.raqmiEditor.saveData(data);
}

export async function exportSignedLicense(
  license: EditorLicense,
  tenantName: string,
): Promise<string | null> {
  if (!window.raqmiEditor) throw new Error('Disponible uniquement en application desktop');
  await window.raqmiEditor.ensureKeys();
  const content = await window.raqmiEditor.exportLicense({
    licenseId: license.id,
    tenantId: license.tenantId,
    tenantName,
    kind: license.kind,
    mode: license.mode,
    status: license.status,
    startsAt: license.startsAt,
    expiresAt: license.expiresAt,
    allowedModules: license.allowedModules,
    limits: license.limits,
    serverFingerprint: license.serverFingerprint,
  });
  return window.raqmiEditor.saveLicenseFile(content, `${tenantName.replace(/\s+/g, '_')}.license`);
}
