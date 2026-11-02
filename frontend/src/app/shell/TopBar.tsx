import { useState, type ReactNode } from "react";
import { Drawer } from "@ars-docendi/ui";

import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { menuIcon } from "./icons";
import { EnlaceAyuda, ListaNavegacion } from "./ListaNavegacion";
import { MarcaArsDocendi } from "./MarcaArsDocendi";
import { MenuUsuario } from "./MenuUsuario";
import { usePantallaAngosta } from "./usePantallaAngosta";

/**
 * Barra superior fina, sólo en pantallas angostas (ahí no hay barra lateral
 * fija): botón "Menú" que abre el panel de navegación, la marca y, a la derecha,
 * la campana de notificaciones (`campana`).
 */
export function TopBar({ user, campana }: { user: CurrentUser; campana?: ReactNode }) {
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
      {campana}
    </header>
  );
}
