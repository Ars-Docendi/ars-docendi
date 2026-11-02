import { Navigate, Outlet } from "react-router-dom";
import { useCurrentUser } from "./useCurrentUser";

/** Con uno de los permisos alcanza. */
export function RequirePermission({ permission }: { permission: string | string[] }) {
  const { user, isLoading } = useCurrentUser();
  if (isLoading) return null;
  const requeridos = Array.isArray(permission) ? permission : [permission];
  if (!user || !requeridos.some((p) => user.permissions.includes(p)))
    return <Navigate to="/" replace />;
  return <Outlet />;
}
