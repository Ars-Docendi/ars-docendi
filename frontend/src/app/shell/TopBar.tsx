import { useState, useSyncExternalStore } from "react";
import { Drawer } from "@ars-docendi/ui";

import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { menuIcon } from "./icons";
import { EnlaceAyuda, ListaNavegacion } from "./ListaNavegacion";
import { MarcaArsDocendi } from "./MarcaArsDocendi";
import { MenuUsuario } from "./MenuUsuario";

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

/**
 * Barra superior fina, sólo en pantallas angostas (ahí no hay barra lateral
 * fija): botón "Menú" que abre el panel de navegación y la marca.
 */
export function TopBar({ user }: { user: CurrentUser }) {
  // Si la ventana se agranda con el panel abierto, el panel se cierra solo:
  // ahí ya está la barra lateral.
  const angosta = usePantallaAngosta();
  const [navegacionAbierta, setNavegacionAbierta] = useState(false);
  const panelVisible = angosta && navegacionAbierta;
  const cerrar = () => setNavegacionAbierta(false);

  return (
    <header className="adoc-topbar">
      <button
        type="button"
        className="adoc-menu-btn"
        onClick={() => setNavegacionAbierta(true)}
        aria-expanded={panelVisible}
        aria-label="Abrir menú de navegación"
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
          <ListaNavegacion permissions={user.permissions} onNavegar={cerrar} />
          <div className="adoc-nav-drawer-pie">
            <EnlaceAyuda onNavegar={cerrar} />
            <MenuUsuario user={user} />
          </div>
        </nav>
      </Drawer>

      <div className="adoc-brand">
        <MarcaArsDocendi />
      </div>
    </header>
  );
}
