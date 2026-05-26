import { useState } from "react";
import { Outlet } from "react-router-dom";

import { useCurrentUser, type Role } from "../shared/auth/useCurrentUser";
import { Sidebar } from "./shell/Sidebar";
import { TopBar } from "./shell/TopBar";
import "./shell/shell.css";

/**
 * App shell (design 04a): top bar + role-aware sidebar + scrolling main.
 * Wraps every authenticated route; the active page renders into <Outlet>.
 */
export default function AppLayout() {
  const currentUser = useCurrentUser();
  const [collapsed, setCollapsed] = useState(false);
  // Local role override so a multi-role user can switch without a reload.
  const [role, setRole] = useState<Role>(currentUser.role);

  const user = { ...currentUser, role };

  return (
    <div className={`adoc-ui adoc-app${collapsed ? " collapsed" : ""}`}>
      <TopBar
        collapsed={collapsed}
        onToggleCollapse={() => setCollapsed((c) => !c)}
        user={user}
        onSwitchRole={setRole}
      />
      <Sidebar collapsed={collapsed} role={role} />
      <main className="adoc-main">
        <Outlet />
      </main>
    </div>
  );
}
