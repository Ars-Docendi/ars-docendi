import { Fragment, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";

import { chevronIcon, navIcons } from "./icons";
import { filtrarNavegacion, type NavItem } from "./nav";

interface ListaNavegacionProps {
  permissions: readonly string[];
  collapsed?: boolean;
  /** Se llama al elegir un destino; el panel móvil lo usa para cerrarse. */
  onNavegar?: () => void;
}

/**
 * Lista de navegación filtrada por permisos. La montan la barra lateral fija
 * y el panel de navegación de pantallas angostas, así el filtrado es uno solo.
 */
export function ListaNavegacion({
  permissions,
  collapsed = false,
  onNavegar,
}: ListaNavegacionProps) {
  const groups = filtrarNavegacion(permissions);

  return (
    <ul className="adoc-nav">
      {groups.map((group) => (
        <Fragment key={group.label}>
          {!collapsed && <li className="adoc-nav-section">{group.label}</li>}
          {group.items.map((item) =>
            item.children && item.children.length > 0 ? (
              <GrupoColapsable
                key={`${group.label}:${item.to}`}
                item={item}
                collapsed={collapsed}
                onNavegar={onNavegar}
              />
            ) : (
              <li key={`${group.label}:${item.to}:${item.label}`}>
                <EnlaceNav item={item} collapsed={collapsed} onNavegar={onNavegar} />
              </li>
            ),
          )}
        </Fragment>
      ))}
    </ul>
  );
}

interface EnlaceNavProps {
  item: NavItem;
  collapsed: boolean;
  onNavegar?: () => void;
}

/** Enlace de navegación hoja (ítem sin hijos, o un hijo dentro de un grupo). */
function EnlaceNav({ item, collapsed, onNavegar }: EnlaceNavProps) {
  return (
    <NavLink
      to={item.to}
      end
      className={({ isActive }) => (isActive ? "current" : undefined)}
      title={collapsed ? item.label : undefined}
      onClick={onNavegar}
    >
      <span className="ico">{navIcons[item.icon]}</span>
      <span className="label">{item.label}</span>
    </NavLink>
  );
}

/** Indica si la ruta actual cae dentro de alguno de los hijos del grupo. */
function usarHijoActivo(item: NavItem, pathname: string): boolean {
  return (item.children ?? []).some(
    (hijo) => pathname === hijo.to || pathname.startsWith(`${hijo.to}/`),
  );
}

/**
 * Ítem padre colapsable: el label navega a su ruta (NavLink) y un botón
 * chevron aparte expande/colapsa los hijos. Arranca abierto y se reabre solo
 * cuando una sub-ruta queda activa. En modo sidebar colapsado (solo iconos)
 * cae a una lista plana de iconos para mantener todas las rutas alcanzables.
 */
function GrupoColapsable({ item, collapsed, onNavegar }: EnlaceNavProps) {
  const { pathname } = useLocation();
  const hijoActivo = usarHijoActivo(item, pathname);

  // Auto-expand al entrar a una sub-ruta activa, sin useEffect: ajuste de
  // estado en render comparando contra el valor previo (patrón recomendado por
  // React, "You Might Not Need an Effect"). Permite colapsar manualmente y
  // reabre solo cuando la ruta activa cambia hacia un hijo.
  const [abierto, setAbierto] = useState(true);
  const [hijoActivoPrevio, setHijoActivoPrevio] = useState(hijoActivo);
  if (hijoActivo !== hijoActivoPrevio) {
    setHijoActivoPrevio(hijoActivo);
    if (hijoActivo) setAbierto(true);
  }

  if (collapsed) {
    return (
      <>
        <li>
          <EnlaceNav item={item} collapsed onNavegar={onNavegar} />
        </li>
        {item.children?.map((hijo) => (
          <li key={hijo.to}>
            <EnlaceNav item={hijo} collapsed onNavegar={onNavegar} />
          </li>
        ))}
      </>
    );
  }

  const idHijos = `nav-grupo-${item.to}`;

  return (
    <li className="adoc-nav-group">
      <div className="adoc-nav-grouprow">
        <EnlaceNav item={item} collapsed={false} onNavegar={onNavegar} />
        <button
          type="button"
          className="adoc-nav-chevron"
          aria-expanded={abierto}
          aria-controls={idHijos}
          aria-label={`${abierto ? "Colapsar" : "Expandir"} ${item.label}`}
          onClick={() => setAbierto((previo) => !previo)}
        >
          <span className={`chev${abierto ? "" : " collapsed"}`}>{chevronIcon}</span>
        </button>
      </div>
      {abierto && (
        <ul className="adoc-nav-children" id={idHijos}>
          {item.children?.map((hijo) => (
            <li key={hijo.to}>
              <EnlaceNav item={hijo} collapsed={false} onNavegar={onNavegar} />
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}
