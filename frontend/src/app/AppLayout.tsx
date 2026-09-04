import { useState } from "react";
import { Outlet } from "react-router-dom";

import { useCurrentUser } from "../shared/auth/useCurrentUser";
import { Sidebar } from "./shell/Sidebar";
import { TopBar } from "./shell/TopBar";
import "./shell/shell.css";

/**
 * App shell (design 04a): top bar + role-aware sidebar + scrolling main.
 * Wraps every authenticated route; the active page renders into <Outlet>.
 *
 */
export default function AppLayout() {
  const usuarioActual = useCurrentUser();
  const [collapsed, setCollapsed] = useState(false);

  if (usuarioActual.isLoading) return <main role="status">Cargando sesión…</main>;
  if (usuarioActual.error || !usuarioActual.user) {
    return (
      <main role="alert">
        No se pudo validar la sesión.{" "}
        <button type="button" onClick={usuarioActual.retry}>
          Reintentar
        </button>
      </main>
    );
  }
  const user = usuarioActual.user;

  return (
    <div className={`adoc-ui adoc-app${collapsed ? " collapsed" : ""}`}>
      <TopBar collapsed={collapsed} onToggleCollapse={() => setCollapsed((c) => !c)} user={user} />
      <Sidebar collapsed={collapsed} role={user.role} />
      <main className="adoc-main">
        <Outlet />
      </main>
    </div>
  );
}
