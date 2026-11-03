import { Outlet } from "react-router-dom";

import { PaginaSinAcceso } from "../navegacion/PaginasDeEstado";
import { useCurrentUser } from "./useCurrentUser";

/**
 * `permission` acepta un único permiso o una lista: con uno de los permisos alcanza (any-of).
 * Sin ninguno la URL no cambia: se explica en la página que el perfil activo no puede abrirla
 * (un redirect silencioso no dice por qué no se llegó).
 */
export function RequirePermission({ permission }: { permission: string | readonly string[] }) {
  const { user, isLoading } = useCurrentUser();
  if (isLoading) return null;
  const requeridos = Array.isArray(permission) ? permission : [permission];
  if (!user || !requeridos.some((p) => user.permissions.includes(p)))
    return <PaginaSinAcceso rol={user?.role} />;
  return <Outlet />;
}
