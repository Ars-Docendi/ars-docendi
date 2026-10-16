import { useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button, RoleBadge } from "@ars-docendi/ui";

import { useCerrarSesion } from "../../shared/auth/auth";
import type { CurrentUser } from "../../shared/auth/useCurrentUser";
// import { collapseIcon } from "./icons";
import { LanzadorAsistente } from "../../features/asistente";
import { useDescartarAlClicAfuera } from "../../shared/hooks/useDescartarAlClicAfuera";

interface TopBarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  user: CurrentUser;
}

export function TopBar({ /*collapsed, onToggleCollapse,*/ user }: TopBarProps) {
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useDescartarAlClicAfuera(menuOpen, menuRef, () => setMenuOpen(false));

  const cerrarSesion = useCerrarSesion();

  async function handleLogout() {
    try {
      await cerrarSesion();
    } finally {
      navigate("/login", { replace: true });
    }
  }

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

      <div className="adoc-topbar-right">
        {/* Acá vivía un botón «Ayuda» disabled con title="Próximamente", que es
            exactamente el fake UI que el invariante #7 prohíbe. El lanzador del
            asistente lo reemplaza: activarlo ELIMINA superficie falsa en vez de
            agregar superficie nueva. Quien no tiene el permiso no ve nada. */}
        <LanzadorAsistente />

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
              <div className="adoc-user-menu-foot">
                <Button variant="ghost" size="sm" onClick={() => void handleLogout()}>
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
