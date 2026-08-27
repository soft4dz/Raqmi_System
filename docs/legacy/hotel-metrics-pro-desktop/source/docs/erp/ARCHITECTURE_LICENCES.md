# Architecture licences — ERP multi-sociétés

## Principe

Trois composants distincts :

```
┌─────────────────────┐     HTTPS      ┌──────────────────────────┐
│  ERP Desktop        │ ◄────────────► │  Serveur licences Raqmi   │
│  (client / société) │   activate     │  (PostgreSQL, multi-org)  │
│                     │   validate     │                           │
└─────────────────────┘                └──────────────────────────┘
         │                                         ▲
         │ modules, données, terminologie            │ émission / révocation
         ▼                                         │ (+ secteur métier)
┌─────────────────────┐                ┌──────────────────────────┐
│  SQLite locale      │                │  Portail éditeur (futur)  │
└─────────────────────┘                │  ou CLI generate-license  │
                                       └──────────────────────────┘
```

| Composant | Rôle | Déployé chez |
|-----------|------|--------------|
| **ERP Desktop** | Métier, activation licence, cache local | Chaque client |
| **Serveur licences** | Organisations, clés, activations poste, révocation | Raqmi (cloud/VPS) |
| **Profil métier** | Secteur, terminologie, pack modules | **Embarqué dans la clé — géré par Raqmi uniquement** |

Le **profil métier** n'est plus configurable chez le client : il est choisi par l'éditeur à l'émission de la licence et appliqué automatiquement à l'activation.

## Format de clé (offline et serveur)

```
RS-{EDITION}-{YYYYMMDD}-{SECTEUR}-{SIG8}
```

| Segment | Exemple | Description |
|---------|---------|-------------|
| EDITION | PRO | STANDARD, PRO, ENTERPRISE |
| YYYYMMDD | 20271231 | Date d'expiration |
| SECTEUR | COMM | HOTL, REST, COMM, SERV, INDU, PORT, GENR |
| SIG8 | A1B2C3D4 | Signature HMAC (secret partagé) |

Clés legacy `RS-{EDITION}-{YYYYMMDD}-{SIG8}` restent valides → secteur **hôtel** par défaut.

Génération côté éditeur :

```bash
node scripts/generate-license-key.mjs PRO 2027-12-31 commerce
# → RS-PRO-20271231-COMM-XXXXXXXX
```

## Modes client

| Mode | `license_mode` | Comportement |
|------|----------------|--------------|
| **offline** | `offline` | Clé RS-* signée HMAC, sans serveur |
| **remote** | `remote` | Activation + validation via `license_server_url` |
| **hybrid** | `remote` + cache | Validation périodique ; grâce si serveur injoignable (à configurer) |

## Endpoints serveur (`/api/v1/licenses`)

| Méthode | Route | Auth | Description |
|---------|-------|------|-------------|
| POST | `/activate` | Public | Active une clé sur un poste (`machineId`) — retourne `businessSector` |
| GET | `/validate` | Public | Vérifie qu'une activation est toujours valide |
| POST | `/admin/issue` | JWT admin | Émet une clé pour une organisation (+ `businessSector`) |
| POST | `/admin/revoke` | JWT admin | Révoque une clé ou une activation |

## Configuration ERP (admin)

- `/settings/licence` — activation, URL serveur, code organisation, synchronisation, **profil métier (lecture seule)**

Variables d'environnement :

- `LICENSE_SERVER_URL` — URL par défaut (ex. `https://licences.raqmi.dz/api/v1`)
- `HMP_LICENSE_SECRET` — secret partagé éditeur (offline + signature serveur)

## Déploiement recommandé

1. **Phase actuelle** : script CLI + mode offline par client
2. **Multi-clients** : déployer le module `licenses` du dossier `server/` sur un VPS dédié
3. **Scale** : portail web éditeur (React) branché sur la même API — pas une 2ᵉ app desktop

Le serveur NestJS existant peut héberger le module licences **ou** être scindé en micro-service `raqmi-license-server` plus tard (même API).
