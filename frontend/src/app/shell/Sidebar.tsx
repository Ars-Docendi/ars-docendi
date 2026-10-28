import { EnlaceAyuda, ListaNavegacion } from "./ListaNavegacion";

interface SidebarProps {
  collapsed: boolean;
  permissions: readonly string[];
}

export function Sidebar({ collapsed, permissions }: SidebarProps) {
  return (
    <nav className="adoc-sidebar" aria-label="Navegación principal">
      <ListaNavegacion permissions={permissions} collapsed={collapsed} />

      <div className="adoc-sidebar-foot">
        <EnlaceAyuda collapsed={collapsed} />
        {collapsed ? (
          "AD"
        ) : (
          <>
            Ars Docendi
            <br />
            <i>El arte de enseñar</i>
          </>
        )}
      </div>
    </nav>
  );
}
