import { type ReactNode, useState } from 'react';

interface Tab {
  key: string;
  label: string;
  icon?: ReactNode;
}

interface TabsProps {
  tabs: Tab[];
  children: (activeKey: string) => ReactNode;
  defaultTab?: string;
}

export function Tabs({ tabs, children, defaultTab }: TabsProps) {
  const [active, setActive] = useState(defaultTab ?? tabs[0]?.key ?? '');

  return (
    <div className="ux-tabs">
      <div className="ux-tabs-bar" role="tablist">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={active === tab.key}
            className={`ux-tab-btn ${active === tab.key ? 'ux-tab-btn--active' : ''}`}
            onClick={() => setActive(tab.key)}
          >
            {tab.icon && <span className="ux-tab-icon">{tab.icon}</span>}
            {tab.label}
          </button>
        ))}
      </div>
      <div className="ux-tab-content" role="tabpanel">
        {children(active)}
      </div>
    </div>
  );
}
