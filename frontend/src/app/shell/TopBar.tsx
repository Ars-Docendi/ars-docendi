import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button, RoleBadge, RoleMenu } from "@ars-docendi/ui";

import { clearToken } from "../../shared/auth/auth";
import type { CurrentUser, Role } from "../../shared/auth/useCurrentUser";
import { bellIcon, /*collapseIcon,*/ helpIcon, searchIcon } from "./icons";

interface TopBarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  user: CurrentUser;
  /** Called when a multi-role user picks a different role. */
  onSwitchRole: (role: Role) => void;
}

export function TopBar({ /*collapsed, onToggleCollapse,*/ user, onSwitchRole }: TopBarProps) {
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Close the user menu on outside click or Escape.
  useEffect(() => {
    if (!menuOpen) return;
    function onPointer(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") setMenuOpen(false);
    }
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [menuOpen]);

  function handleLogout() {
    clearToken();
    navigate("/login", { replace: true });
  }

  const canSwitchRole = user.roles.length > 1;

  return (
    <header className="adoc-topbar">
      <div className="adoc-brand">
        <span className="mark">AD</span>
        <span>Ars Docendi</span>
        <span className="sub">Ingeniería · UNLaM</span>

        {/* Collapse button, comentado hasta que se me ocurra un buen diseño.
        <button
          type="button"
          className="adoc-collapse-btn"
          onClick={onToggleCollapse}
          aria-pressed={collapsed}
          aria-label={collapsed ? "Expandir menú lateral" : "Colapsar menú lateral"}
        >
          <span className="ico">{collapseIcon}</span>
        </button>
        */}
      </div>

      <div className="adoc-topbar-search">
        <span className="ico">{searchIcon}</span>
        {/* No global-search backend yet — disabled so it doesn't pretend to work. */}
        <input
          type="search"
          placeholder="Búsqueda global · próximamente"
          disabled
          aria-label="Búsqueda global (próximamente)"
        />
      </div>

      <div className="adoc-topbar-right">
        <button
          type="button"
          className="adoc-icon-btn"
          aria-label="Notificaciones"
          title="Próximamente"
          disabled
        >
          <span className="ico">{bellIcon}</span>
        </button>
        <button
          type="button"
          className="adoc-icon-btn"
          aria-label="Ayuda"
          title="Próximamente"
          disabled
        >
          <span className="ico">{helpIcon}</span>
        </button>

        <div className="adoc-user-menu" ref={menuRef}>
          <RoleBadge
            name={user.name}
            initials={user.initials}
            role={user.role}
            multi
            onSwitchClick={() => setMenuOpen((open) => !open)}
            switchLabel="Abrir menú de usuario"
          />

          {menuOpen && (
            <div className="adoc-user-menu-pop" role="menu">
              {canSwitchRole && (
                <RoleMenu
                  heading="Cambiar de rol"
                  options={user.roles.map((r) => ({
                    id: r,
                    name: r,
                    current: r === user.role,
                  }))}
                  onSelect={(id) => {
                    onSwitchRole(id as Role);
                    setMenuOpen(false);
                  }}
                />
              )}
              <div className="adoc-user-menu-foot">
                <Button variant="ghost" size="sm" onClick={handleLogout}>
                  Cerrar sesión
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
