import { Navigate, Outlet } from "react-router-dom";
import { useCurrentUser } from "./useCurrentUser";

export function RequirePermission({ permission }: { permission: string }) {
  const { user, isLoading } = useCurrentUser();
  if (isLoading) return null;
  if (!user || !user.permissions.includes(permission)) return <Navigate to="/" replace />;
  return <Outlet />;
}
