import { useAuth } from "@/lib/auth";
import { type Permission, rolePermissions } from "@/lib/permissions";

export { type Permission } from "@/lib/permissions";

export function usePermissions() {
  const { user } = useAuth();
  const permissions = rolePermissions[user?.role ?? ""] ?? new Set<Permission>();

  return {
    can: (permission: Permission): boolean => permissions.has(permission),
    canAny: (...perms: Permission[]): boolean => perms.some((p) => permissions.has(p)),
    role: user?.role ?? null,
  };
}
