# Modèle de gestion des licences

## Objectif

Vendre Raqmi System par packs ou licences personnalisées, avec activation offline via fichier signé.

## Flux offline (implémenté)

```text
License Manager (éditeur)
  → clé privée Ed25519 (%APPDATA%\Raqmi License Manager\keys\)
  → signLicenseFile() via @raqmi/licensing
  → export client.license

Serveur client
  → clé publique embarquée (public.jwk.json)
  → POST /api/v1/license/import
  → verifyLicenseFile() + evaluateLicense()
  → modules activés localement
```

## Format fichier `.license`

```json
{
  "version": 1,
  "payload": {
    "licenseId": "lic-...",
    "tenantId": "tenant-...",
    "tenantName": "Hotel Demo",
    "kind": "professional",
    "mode": "offline",
    "status": "active",
    "issuedAt": "2026-06-26T00:00:00.000Z",
    "startsAt": "2026-01-01T00:00:00Z",
    "expiresAt": "2027-01-01T00:00:00Z",
    "allowedModules": ["billing", "reports"],
    "limits": {
      "maxUsers": 50,
      "maxSites": 5,
      "maxStorageGb": 100,
      "offlineGraceDays": 30
    },
    "serverFingerprint": "optional-32-char-hash"
  },
  "signature": "<JWT EdDSA contenant le payload>"
}
```

## Crypto (`@raqmi/licensing`)

| Module | Rôle |
|--------|------|
| `license-crypto.ts` | Paire Ed25519, sign/verify JWT |
| `license-file.ts` | Format `.license`, parse/serialize |
| `server-fingerprint.ts` | SHA256(hostname + platform + arch + salt) |

Génération des clés éditeur :

```powershell
pnpm keys:generate
# keys/public.jwk.json  → serveur / installateur
# keys/private.jwk.json → License Manager uniquement (ne pas committer)
```

## Packs commerciaux

Définis dans `packages/licensing/src/license-packs.ts` :

- **trial** — découverte limitée
- **starter** — petite structure
- **professional** — hôtel / PME
- **enterprise** — multi-sites
- **custom** — sur mesure

## Évaluation côté serveur

`evaluateLicense()` dans `@raqmi/licensing` vérifie :

- statut (active, suspended, expired, revoked)
- dates de validité
- limites utilisateurs / sites / stockage
- module demandé (`requireModule` middleware)
- mode offline + `lastOnlineCheckAt` + `offlineGraceDays`

## Règles de blocage

| Situation | Comportement |
|-----------|--------------|
| Licence expirée | `readonlyMode`, création bloquée |
| Module non inclus | HTTP 403 via `requireModule` |
| Signature invalide | Import refusé |
| Empreinte incorrecte | Import refusé si `serverFingerprint` défini |

## Règle importante

Le contrôle de licence est appliqué **côté serveur** (`requireModule`, `evaluateActiveLicense`), pas seulement dans l'interface client.

## Rotation de clés

1. Générer une nouvelle paire (`pnpm keys:generate`)
2. Redistribuer la clé publique avec le prochain installateur serveur
3. Re-signer et ré-exporter les licences clients actives
4. Conserver l'ancienne clé privée pour l'historique si nécessaire
