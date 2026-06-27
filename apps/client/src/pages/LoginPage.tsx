import { FormEvent, useState } from 'react';

interface LoginPageProps {
  onLogin: (email: string, password: string) => Promise<void>;
  error: string | null;
}

export function LoginPage({ onLogin, error }: LoginPageProps) {
  const [email, setEmail] = useState('admin@demo.raqmi.local');
  const [password, setPassword] = useState('demo1234');
  const [submitting, setSubmitting] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setLocalError(null);
    try {
      await onLogin(email, password);
    } catch (err) {
      setLocalError(err instanceof Error ? err.message : 'Connexion impossible');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="shell login-layout">
      <section className="login-panel">
        <div className="brand">
          <div className="brand-mark">R</div>
          <div>
            <h1>Raqmi System</h1>
            <p>ERP modulaire multi-client</p>
          </div>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="username"
              required
            />
          </label>
          <label>
            Mot de passe
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
          </label>
          {(error || localError) && <p className="error">{localError ?? error}</p>}
          <button type="submit" disabled={submitting}>
            {submitting ? 'Connexion…' : 'Se connecter'}
          </button>
        </form>

        <p className="hint">
          Compte demo : <code>admin@demo.raqmi.local</code> / <code>demo1234</code>
        </p>
      </section>

      <aside className="login-aside">
        <h2>Modules activés par licence</h2>
        <p>
          Le client affiche uniquement les modules autorisés par le serveur, selon le pack
          Starter, Professional ou Enterprise.
        </p>
        <ul>
          <li>23 modules ERP</li>
          <li>Contrôle licence côté serveur</li>
          <li>Mode demo sans base de données</li>
        </ul>
      </aside>
    </div>
  );
}
