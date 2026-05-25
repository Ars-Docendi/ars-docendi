import { Navigate, Outlet, useLocation } from "react-router-dom";

import { isAuthenticated } from "./auth";

/** Layout-route gate: renders the protected tree only when authenticated. */
export function RequireAuth() {
  const location = useLocation();
  if (!isAuthenticated()) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }
  return <Outlet />;
}
