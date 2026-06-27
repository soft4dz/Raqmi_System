# PostgreSQL embarqué (Windows)

Placez ici les binaires PostgreSQL 16 extraits de l'archive EDB :

- Téléchargement : https://www.enterprisedb.com/download-postgresql-binaries
- Fichier : `postgresql-16.x-windows-x64-binaries.zip`
- Extraire le contenu dans ce dossier pour obtenir `bin/pg_ctl.exe`, `bin/initdb.exe`, etc.

Structure attendue :

```text
installer/assets/postgresql/
  bin/
    initdb.exe
    pg_ctl.exe
    psql.exe
    postgres.exe
  lib/
  share/
```

L'installateur NSIS copie ce dossier vers `%ProgramFiles%\Raqmi System Server\postgresql`.
