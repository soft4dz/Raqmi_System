INSERT INTO security.permissions (key, name, category, description)
VALUES
    ('users.read', 'Lire les utilisateurs', 'security', 'Consulter les utilisateurs et profils.'),
    ('users.write', 'Gerer les utilisateurs', 'security', 'Creer, modifier, activer ou desactiver les utilisateurs.'),
    ('roles.read', 'Lire les roles', 'security', 'Consulter les roles et permissions.'),
    ('roles.write', 'Gerer les roles', 'security', 'Modifier les roles et leurs permissions.'),
    ('units.read', 'Lire les unites', 'organization', 'Consulter les hotels et unites.'),
    ('units.write', 'Gerer les unites', 'organization', 'Creer et modifier les hotels et unites.'),
    ('revenue.read', 'Lire les recettes', 'exploitation', 'Consulter les recettes journalieres.'),
    ('revenue.write', 'Saisir les recettes', 'exploitation', 'Saisir ou corriger les recettes journalieres.'),
    ('revenue.validate', 'Valider les recettes', 'exploitation', 'Valider les recettes apres controle.'),
    ('dashboard.read', 'Lire les tableaux de bord', 'reporting', 'Consulter les indicateurs de direction.'),
    ('treasury.read', 'Lire la tresorerie', 'finance', 'Consulter caisse, encaissements et mouvements.'),
    ('treasury.write', 'Gerer la tresorerie', 'finance', 'Creer ou modifier les mouvements de tresorerie.'),
    ('audit.read', 'Lire l audit', 'security', 'Consulter le journal des actions sensibles.'),
    ('reports.export', 'Exporter les rapports', 'reporting', 'Exporter ou imprimer les etats.'),
    ('security.seed', 'Initialiser la securite', 'security', 'Executer les operations de socle securite.')
ON CONFLICT (key) DO NOTHING;

INSERT INTO security.roles (name, display_name, description, is_system)
VALUES
    ('system.administrator', 'Administrateur systeme', 'Acces complet au socle Raqmi System.', true),
    ('direction', 'Direction', 'Consultation direction, tableaux de bord et reporting.', true),
    ('exploitation.control', 'Exploitation et controle', 'Controle des recettes, validation et audit.', true),
    ('unit.manager', 'Responsable unite', 'Gestion operationnelle d une unite hoteliere.', true),
    ('cashier', 'Caissier', 'Saisie caisse, recettes et mouvements de tresorerie.', true),
    ('reader', 'Lecture seule', 'Consultation limitee des donnees autorisees.', true)
ON CONFLICT (name) DO NOTHING;

INSERT INTO security.role_permissions (role_id, permission_id)
SELECT roles.id, permissions.id
FROM security.roles roles
CROSS JOIN security.permissions permissions
WHERE roles.name = 'system.administrator'
ON CONFLICT DO NOTHING;

INSERT INTO security.role_permissions (role_id, permission_id)
SELECT roles.id, permissions.id
FROM security.roles roles
JOIN security.permissions permissions ON permissions.key IN (
    'units.read',
    'revenue.read',
    'dashboard.read',
    'treasury.read',
    'audit.read',
    'reports.export'
)
WHERE roles.name = 'direction'
ON CONFLICT DO NOTHING;

INSERT INTO security.role_permissions (role_id, permission_id)
SELECT roles.id, permissions.id
FROM security.roles roles
JOIN security.permissions permissions ON permissions.key IN (
    'units.read',
    'revenue.read',
    'revenue.write',
    'revenue.validate',
    'dashboard.read',
    'audit.read',
    'reports.export'
)
WHERE roles.name = 'exploitation.control'
ON CONFLICT DO NOTHING;

INSERT INTO security.role_permissions (role_id, permission_id)
SELECT roles.id, permissions.id
FROM security.roles roles
JOIN security.permissions permissions ON permissions.key IN (
    'units.read',
    'revenue.read',
    'revenue.write',
    'dashboard.read',
    'reports.export'
)
WHERE roles.name = 'unit.manager'
ON CONFLICT DO NOTHING;

INSERT INTO security.role_permissions (role_id, permission_id)
SELECT roles.id, permissions.id
FROM security.roles roles
JOIN security.permissions permissions ON permissions.key IN (
    'revenue.read',
    'revenue.write',
    'treasury.read',
    'treasury.write'
)
WHERE roles.name = 'cashier'
ON CONFLICT DO NOTHING;

INSERT INTO security.role_permissions (role_id, permission_id)
SELECT roles.id, permissions.id
FROM security.roles roles
JOIN security.permissions permissions ON permissions.key IN (
    'units.read',
    'revenue.read',
    'dashboard.read'
)
WHERE roles.name = 'reader'
ON CONFLICT DO NOTHING;
