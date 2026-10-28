import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button, Drawer, RoleBadge } from "@ars-docendi/ui";

import { useCerrarSesion } from "../../shared/auth/auth";
import { IsotipoArsDocendi } from "../../shared/marca/IsotipoArsDocendi";
import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { menuIcon } from "./icons";
import { EnlaceAyuda, ListaNavegacion } from "./ListaNavegacion";

/** Mismo corte que `shell.css`: debajo de esto la barra lateral fija no se muestra. */
const CONSULTA_ANGOSTA = "(max-width: 767px)";

function suscribirAncho(avisar: () => void) {
  const consulta = window.matchMedia?.(CONSULTA_ANGOSTA);
  consulta?.addEventListener("change", avisar);
  return () => consulta?.removeEventListener("change", avisar);
}

function usePantallaAngosta(): boolean {
  return useSyncExternalStore(
    suscribirAncho,
    () => window.matchMedia?.(CONSULTA_ANGOSTA).matches ?? false,
    () => false,
  );
}

interface TopBarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  user: CurrentUser;
}

export function TopBar({ collapsed, onToggleCollapse, user }: TopBarProps) {
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  // El botón "Menú" abre el panel en pantallas angostas y colapsa la barra
  // lateral a íconos en escritorio. Si la ventana se agranda con el panel
  // abierto, el panel se cierra solo: ahí ya está la barra lateral.
  const angosta = usePantallaAngosta();
  const [navegacionAbierta, setNavegacionAbierta] = useState(false);
  const panelVisible = angosta && navegacionAbierta;
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    function closeOnOutsideClick(event: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) setMenuOpen(false);
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") setMenuOpen(false);
    }
    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [menuOpen]);

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
      <button
        type="button"
        className="adoc-menu-btn"
        onClick={angosta ? () => setNavegacionAbierta(true) : onToggleCollapse}
        aria-expanded={angosta ? panelVisible : !collapsed}
        aria-label={
          angosta
            ? "Abrir menú de navegación"
            : collapsed
              ? "Expandir menú lateral"
              : "Colapsar menú lateral"
        }
      >
        <span className="ico">{menuIcon}</span>
      </button>
      <Drawer
        open={panelVisible}
        onOpenChange={setNavegacionAbierta}
        side="left"
        title="Menú"
        className="adoc-nav-drawer"
      >
        <nav aria-label="Navegación principal">
          <ListaNavegacion
            permissions={user.permissions}
            onNavegar={() => setNavegacionAbierta(false)}
          />
          <div className="adoc-nav-drawer-pie">
            <EnlaceAyuda onNavegar={() => setNavegacionAbierta(false)} />
          </div>
        </nav>
      </Drawer>

      <div className="adoc-brand">
        {/* La marca lleva a Mi portal, el inicio de la aplicación. */}
        <Link to="/portal" className="adoc-brand-link" aria-label="Ars Docendi, ir a Mi portal">
          <IsotipoArsDocendi className="mark" />
          <span>Ars Docendi</span>
        </Link>
      </div>

      <div className="adoc-topbar-right">
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
