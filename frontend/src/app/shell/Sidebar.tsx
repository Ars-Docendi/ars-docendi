import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { menuIcon } from "./icons";
import { EnlaceAyuda, ListaNavegacion } from "./ListaNavegacion";
import { MarcaArsDocendi } from "./MarcaArsDocendi";
import { MenuUsuario } from "./MenuUsuario";

interface SidebarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  user: CurrentUser;
}

/** Barra lateral de escritorio: menú y marca arriba, ayuda y usuario abajo. */
export function Sidebar({ collapsed, onToggleCollapse, user }: SidebarProps) {
  return (
    <aside className="adoc-sidebar">
      <div className="adoc-sidebar-head">
        <button
          type="button"
          className="adoc-menu-btn"
          onClick={onToggleCollapse}
          aria-expanded={!collapsed}
          aria-label={collapsed ? "Expandir menú lateral" : "Colapsar menú lateral"}
        >
          <span className="ico">{menuIcon}</span>
        </button>
        <div className="adoc-brand">
          <MarcaArsDocendi collapsed={collapsed} />
        </div>
      </div>

      <nav className="adoc-sidebar-nav" aria-label="Navegación principal">
        <ListaNavegacion permissions={user.permissions} collapsed={collapsed} />
      </nav>

      <div className="adoc-sidebar-foot">
        <EnlaceAyuda collapsed={collapsed} />
        <MenuUsuario user={user} collapsed={collapsed} />
      </div>
    </aside>
  );
}
