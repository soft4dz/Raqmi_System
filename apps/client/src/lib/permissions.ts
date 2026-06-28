export function hasPermission(
  permissions: string[] | undefined,
  module: string,
  action: string,
): boolean {
  if (!permissions?.length) return false;
  if (permissions.includes('*')) return true;
  return (
    permissions.includes(`${module}:${action}`) ||
    permissions.includes(`${module}:*`)
  );
}

export function isAdminRole(roleCode: string): boolean {
  return roleCode === 'admin';
}

export function canReadAdmin(permissions: string[] | undefined, roleCode: string): boolean {
  return isAdminRole(roleCode) || hasPermission(permissions, 'administration', 'read');
}

export function canWriteAdmin(permissions: string[] | undefined, roleCode: string): boolean {
  return isAdminRole(roleCode) || hasPermission(permissions, 'administration', 'write');
}

export function canWriteSites(permissions: string[] | undefined, roleCode: string): boolean {
  return isAdminRole(roleCode) || hasPermission(permissions, 'sites', 'write');
}

export function canWriteSettings(permissions: string[] | undefined, roleCode: string): boolean {
  return isAdminRole(roleCode) || hasPermission(permissions, 'settings', 'write');
}
