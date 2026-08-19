import { useState } from "react";
import { Outlet } from "react-router-dom";

import { useCurrentUser } from "../shared/auth/useCurrentUser";
import { developmentAuthEnabled } from "../shared/auth/developmentAuth";
import { Sidebar } from "./shell/Sidebar";
import { TopBar } from "./shell/TopBar";
import "./shell/shell.css";

/**
 * App shell (design 04a): top bar + role-aware sidebar + scrolling main.
 * Wraps every authenticated route; the active page renders into <Outlet>.
 *
 * En desarrollo, cambiar el rol persistido vuelve a resolver la identidad y sus
 * ámbitos en el backend antes de actualizar los datos autorizados del shell.
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
      <TopBar
        collapsed={collapsed}
        onToggleCollapse={() => setCollapsed((c) => !c)}
        user={user}
        onSwitchRole={(rol) => {
          const opcion = user.roleOptions.find((item) => item.nombre === rol);
          if (opcion && developmentAuthEnabled) {
            void import("../shared/auth/dev/session").then(({ cambiarRolDesarrollo }) => {
              cambiarRolDesarrollo(opcion.codigo);
            });
          }
        }}
      />
      <Sidebar collapsed={collapsed} role={user.role} />
      <main className="adoc-main">
        <Outlet />
      </main>
    </div>
  );
}
