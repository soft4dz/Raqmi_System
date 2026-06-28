const STORAGE_KEY = 'raqmi_active_site';

export function getActiveSiteId(): string | null {
  return localStorage.getItem(STORAGE_KEY);
}

export function setActiveSiteIdStorage(id: string | null) {
  if (id) localStorage.setItem(STORAGE_KEY, id);
  else localStorage.removeItem(STORAGE_KEY);
}

export { STORAGE_KEY as ACTIVE_SITE_STORAGE_KEY };
