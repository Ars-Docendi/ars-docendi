import { Fragment } from "react";
import { NavLink } from "react-router-dom";

import type { Role } from "../../shared/auth/useCurrentUser";
import { navIcons } from "./icons";
import { NAV_BY_ROLE } from "./nav";

interface SidebarProps {
  collapsed: boolean;
  role: Role;
}

export function Sidebar({ collapsed, role }: SidebarProps) {
  const groups = NAV_BY_ROLE[role];

  return (
    <nav className="adoc-sidebar" aria-label="Navegación principal">
      <ul className="adoc-nav">
        {groups.map((group) => (
          <Fragment key={group.label}>
            {!collapsed && <li className="adoc-nav-section">{group.label}</li>}
            {group.items.map((item) => (
              <li key={`${group.label}:${item.to}:${item.label}`}>
                <NavLink
                  to={item.to}
                  className={({ isActive }) => (isActive ? "current" : undefined)}
                  title={collapsed ? item.label : undefined}
                >
                  <span className="ico">{navIcons[item.icon]}</span>
                  <span className="label">{item.label}</span>
                </NavLink>
              </li>
            ))}
          </Fragment>
        ))}
      </ul>

      <div className="adoc-sidebar-foot">
        {collapsed ? (
          "1C"
        ) : (
          <>
            Cuatrimestre activo · <b style={{ color: "var(--color-text-primary)" }}>2026 · 1C</b>
            <br />
            Versión 0.1 · Borrador
          </>
        )}
      </div>
    </nav>
  );
}
