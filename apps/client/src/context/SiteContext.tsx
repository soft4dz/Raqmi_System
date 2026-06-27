import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, type SiteDto } from '../api';

const STORAGE_KEY = 'raqmi_active_site';

interface SiteContextValue {
  sites: SiteDto[];
  activeSiteId: string | null;
  activeSite: SiteDto | null;
  setActiveSiteId: (id: string | null) => void;
  reload: () => Promise<void>;
  loading: boolean;
}

const SiteContext = createContext<SiteContextValue | null>(null);

export function SiteProvider({ userRole, children }: { userRole: string; children: ReactNode }) {
  const [sites, setSites] = useState<SiteDto[]>([]);
  const [activeSiteId, setActiveSiteIdState] = useState<string | null>(() => localStorage.getItem(STORAGE_KEY));
  const [loading, setLoading] = useState(true);

  async function reload() {
    try {
      setLoading(true);
      const res = await api.getSites();
      setSites(res.items);
      setActiveSiteIdState((current) => {
        if (current && res.items.some((s) => s.id === current && s.active)) return current;
        const first = res.items.find((s) => s.active);
        const next = first?.id ?? null;
        if (next) localStorage.setItem(STORAGE_KEY, next);
        else localStorage.removeItem(STORAGE_KEY);
        return next;
      });
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void reload();
  }, [userRole]);

  function setActiveSiteId(id: string | null) {
    setActiveSiteIdState(id);
    if (id) localStorage.setItem(STORAGE_KEY, id);
    else localStorage.removeItem(STORAGE_KEY);
  }

  const activeSite = useMemo(
    () => sites.find((s) => s.id === activeSiteId) ?? null,
    [sites, activeSiteId],
  );

  return (
    <SiteContext.Provider value={{ sites, activeSiteId, activeSite, setActiveSiteId, reload, loading }}>
      {children}
    </SiteContext.Provider>
  );
}

export function useSiteContext() {
  const ctx = useContext(SiteContext);
  if (!ctx) throw new Error('useSiteContext requires SiteProvider');
  return ctx;
}

export function getActiveSiteId() {
  return localStorage.getItem(STORAGE_KEY);
}
