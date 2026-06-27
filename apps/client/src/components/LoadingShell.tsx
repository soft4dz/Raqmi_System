export function LoadingShell() {
  return (
    <div className="ux-loading-shell" role="status" aria-live="polite" aria-label="Chargement">
      <div className="ux-loading-top">
        <div className="ux-skeleton ux-skeleton-brand" />
        <div className="ux-skeleton-row">
          <div className="ux-skeleton ux-skeleton-chip" />
          <div className="ux-skeleton ux-skeleton-chip" />
          <div className="ux-skeleton ux-skeleton-avatar" />
        </div>
      </div>
      <div className="ux-loading-body">
        <aside className="ux-loading-sidebar">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="ux-skeleton ux-skeleton-nav" style={{ animationDelay: `${i * 80}ms` }} />
          ))}
        </aside>
        <main className="ux-loading-main">
          <div className="ux-skeleton ux-skeleton-hero" />
          <div className="ux-skeleton-grid">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="ux-skeleton ux-skeleton-kpi" style={{ animationDelay: `${i * 100}ms` }} />
            ))}
          </div>
        </main>
      </div>
    </div>
  );
}

export function TableSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="ux-table-skeleton" aria-hidden="true">
      <div className="ux-skeleton ux-skeleton-table-head" />
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="ux-skeleton ux-skeleton-table-row" style={{ animationDelay: `${i * 60}ms` }} />
      ))}
    </div>
  );
}
