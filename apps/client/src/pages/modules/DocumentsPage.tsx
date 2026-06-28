import { ChangeEvent, useEffect, useRef, useState } from 'react';
import { api, type DocumentDto } from '../../api';
import { TableSkeleton } from '../../components/LoadingShell';
import { useSiteContext } from '../../context/SiteContext';
import { useI18n } from '../../i18n/I18nProvider';

export function DocumentsPage() {
  const { activeSite, activeSiteId } = useSiteContext();
  const { dateLocale } = useI18n();
  const [items, setItems] = useState<DocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  async function load() {
    try {
      setError(null);
      setItems((await api.getDocuments()).items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    setLoading(true);
    void load();
  }, [activeSiteId]);

  async function onDelete(id: string) {
    try {
      await api.deleteDocument(id);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    }
  }

  async function onFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      setUploading(true);
      setError(null);
      await api.uploadDocument(file);
      if (fileInputRef.current) fileInputRef.current.value = '';
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur');
    } finally {
      setUploading(false);
    }
  }

  if (loading) return <TableSkeleton rows={5} />;

  return (
    <div className="module-panel ux-users-panel">
      {error && <div className="module-error ux-alert"><p>{error}</p></div>}
      {activeSite && <p className="ux-card-desc">Site actif : <strong>{activeSite.name}</strong></p>}

      <section className="ux-card ux-form-card">
        <h3 className="ux-card-title">Importer un document</h3>
        <div className="ux-form-grid">
          <label className="ux-field">
            <span className="ux-field-label">Fichier</span>
            <input
              ref={fileInputRef}
              type="file"
              className="ux-field-input"
              disabled={uploading}
              onChange={(e) => void onFileChange(e)}
            />
          </label>
          {uploading && <p className="ux-card-desc">Envoi en cours…</p>}
        </div>
      </section>

      <section className="ux-card">
        <h3 className="ux-card-title">Documents ({items.length})</h3>
        {items.length === 0 ? (
          <div className="ux-empty-state"><p>Aucun document pour ce site.</p></div>
        ) : (
          <div className="module-table-wrap ux-table-wrap">
            <table className="module-table ux-table">
              <thead><tr><th>Nom</th><th>Type</th><th>Taille</th><th>Date</th><th /></tr></thead>
              <tbody>
                {items.map((d) => (
                  <tr key={d.id}>
                    <td>{d.originalName}</td>
                    <td>{d.mimeType ?? '—'}</td>
                    <td>{Math.round(d.sizeBytes / 1024)} Ko</td>
                    <td>{new Date(d.uploadedAt).toLocaleString(dateLocale)}</td>
                    <td><button type="button" className="db-ghost-btn ux-btn-sm" onClick={() => void onDelete(d.id)}>Supprimer</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
