import { Navigate, Outlet } from "react-router-dom";
import { useCurrentUser } from "./useCurrentUser";

/** `permission` acepta uno o varios permisos; con varios, alcanza con tener alguno (OR). */
export function RequirePermission({ permission }: { permission: string | string[] }) {
  const { user, isLoading } = useCurrentUser();
  if (isLoading) return null;
  const permisosRequeridos = Array.isArray(permission) ? permission : [permission];
  if (!user || !permisosRequeridos.some((p) => user.permissions.includes(p))) {
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
