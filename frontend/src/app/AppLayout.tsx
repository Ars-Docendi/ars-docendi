import { useState } from "react";
import { Outlet, useLocation } from "react-router-dom";

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
  const { pathname } = useLocation();
  // Bajo /ayuda la sidebar se contrae a íconos para dar lugar a la de documentación.
  // Se deriva de la ruta (sin estado ni efectos): al salir, vuelve sola al estado previo.
  const enAyuda = pathname === "/ayuda" || pathname.startsWith("/ayuda/");
  const colapsado = collapsed || enAyuda;

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
    <div className={`adoc-ui adoc-app${colapsado ? " collapsed" : ""}`}>
      <TopBar collapsed={colapsado} onToggleCollapse={() => setCollapsed((c) => !c)} user={user} />
      <Sidebar collapsed={colapsado} permissions={user.permissions} />
      <main className="adoc-main">
        <Outlet />
      </main>
    </div>
  );
}
