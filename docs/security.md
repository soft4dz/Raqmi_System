# Security baseline

## Rules from day one

- No default admin password committed to the repository.
- No API key, database password or license secret in source code.
- Production configuration must be injected by environment variables or a secure secret store.
- Passwords must be hashed using a modern password hashing algorithm.
- JWT or cookie authentication must include expiry and refresh strategy.
- Every sensitive action must be logged in the audit trail.
- Permissions must be checked server-side, not only in the UI.

## First security tasks

1. Define roles.
2. Define permission matrix.
3. Add authentication.
4. Add audit trail.
5. Add secure configuration loading.
6. Add license verification model.
