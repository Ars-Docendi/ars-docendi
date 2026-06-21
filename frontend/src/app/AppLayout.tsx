import { useState } from "react";
import { Outlet } from "react-router-dom";

import { useCurrentUser } from "../shared/auth/useCurrentUser";
import { setRolActivo } from "../shared/auth/dev/mockSession";
import { Sidebar } from "./shell/Sidebar";
import { TopBar } from "./shell/TopBar";
import "./shell/shell.css";

/**
 * App shell (design 04a): top bar + role-aware sidebar + scrolling main.
 * Wraps every authenticated route; the active page renders into <Outlet>.
 *
 * El rol activo de un usuario multi-rol vive en la sesión mock (reactiva vía
 * useCurrentUser): al cambiar de rol re-renderiza el shell Y el ActorContexto
 * que consume la capa api/, sin recargar.
 */
export default function AppLayout() {
  const user = useCurrentUser();
  const [collapsed, setCollapsed] = useState(false);

  return (
    <div className={`adoc-ui adoc-app${collapsed ? " collapsed" : ""}`}>
      <TopBar
        collapsed={collapsed}
        onToggleCollapse={() => setCollapsed((c) => !c)}
        user={user}
        onSwitchRole={setRolActivo}
      />
      <Sidebar collapsed={collapsed} role={user.role} />
      <main className="adoc-main">
        <Outlet />
      </main>
    </div>
  );
}
