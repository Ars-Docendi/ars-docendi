import { Navigate, Outlet } from "react-router-dom";

import type { Role } from "./useCurrentUser";
import { useCurrentUser } from "./useCurrentUser";

interface RequireRoleProps {
  allowedRoles: Role[];
}

export function RequireRole({ allowedRoles }: RequireRoleProps) {
  const { user, isLoading } = useCurrentUser();
  if (isLoading) return null;
  if (!user || !allowedRoles.includes(user.role)) {
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
