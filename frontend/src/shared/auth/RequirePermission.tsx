import { Navigate, Outlet } from "react-router-dom";
import { useCurrentUser } from "./useCurrentUser";

/**
 * `permission` acepta un único permiso o una lista: con una lista, alcanza con
 * mantener CUALQUIERA de los permisos indicados (any-of). Ampliado para
 * `/sistema` (design D9, sistema-seccion-unificada), que se guarda con las
 * tres — `sistema.estado.ver`, `asistente.administrar`, `auditoria.ver` —; los
 * call sites de un solo string existentes no cambian.
 */
export function RequirePermission({ permission }: { permission: string | readonly string[] }) {
  const { user, isLoading } = useCurrentUser();
  if (isLoading) return null;
  const requeridos = Array.isArray(permission) ? permission : [permission];
  const tieneAlguno = requeridos.some((p) => user?.permissions.includes(p));
  if (!user || !tieneAlguno) return <Navigate to="/" replace />;
  return <Outlet />;
}
