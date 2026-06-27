# Raqmi System — stack C# / .NET 8

Migration du monorepo TypeScript vers **C# / .NET 8** pour Windows natif.

## Projets

| Projet | Type | Rôle |
|--------|------|------|
| `Raqmi.Shared` | Bibliothèque | Catalogue des 23 modules ERP |
| `Raqmi.Licensing` | Bibliothèque | Licences signées Ed25519, packs, politique |
| `Raqmi.Server` | ASP.NET Core | API REST (auth, modules, licence) |
| `Raqmi.Client` | WPF | Application desktop utilisateur |
| `Raqmi.LicenseManager` | WPF | Outil éditeur (CRUD + export `.license`) |

## Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Lancer en développement

```powershell
cd dotnet

# 1. API serveur (port 3000, mode demo)
dotnet run --project src/Raqmi.Server

# 2. Client WPF (autre terminal)
dotnet run --project src/Raqmi.Client

# 3. License Manager (autre terminal)
dotnet run --project src/Raqmi.LicenseManager
```

**Compte demo :** `admin@demo.raqmi.local` / `demo1234`

## Build

```powershell
dotnet build RaqmiSystem.sln
```

## Tests

```powershell
pnpm dotnet:test
# ou
dotnet test RaqmiSystem.sln
```

18 tests (5 licensing + 13 intégration API) : auth, licence, modules, pack Professional 14/23.

## Coexistence avec l'ancien stack TypeScript

Le dossier `dotnet/` cohabite avec `apps/` (Electron/Node) le temps de la migration.
Les fichiers `.license` générés restent compatibles entre les deux stacks (format Ed25519 identique).

## Prochaines étapes migration

- EF Core + PostgreSQL (remplace Prisma)
- Installateur MSI/WiX (remplace NSIS + Electron)
- Services Windows natifs pour le serveur
- Tests xUnit pour `@raqmi/licensing` C#
