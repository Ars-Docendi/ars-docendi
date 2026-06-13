import { Navigate, Outlet } from "react-router-dom";

import type { Role } from "./useCurrentUser";
import { useCurrentUser } from "./useCurrentUser";

interface RequireRoleProps {
  allowedRoles: Role[];
}

export function RequireRole({ allowedRoles }: RequireRoleProps) {
  const usuario = useCurrentUser();
  if (!allowedRoles.includes(usuario.role)) {
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
