declare global {
  interface Window {
    raqmi?: {
      getConfig: () => Promise<{ serverUrl: string; locale?: 'fr' | 'en' | 'ar' }>;
      setConfig: (config: { serverUrl: string; locale?: 'fr' | 'en' | 'ar' }) => Promise<void>;
      testServer: (serverUrl: string) => Promise<boolean>;
      isDesktop: boolean;
    };
  }
}

export {};
