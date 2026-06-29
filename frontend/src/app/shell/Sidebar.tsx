import { Fragment, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";

import type { Role } from "../../shared/auth/useCurrentUser";
import { chevronIcon, navIcons } from "./icons";
import { NAV_BY_ROLE, type NavItem } from "./nav";

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
            {group.items.map((item) =>
              item.children && item.children.length > 0 ? (
                <GrupoColapsable
                  key={`${group.label}:${item.to}`}
                  item={item}
                  collapsed={collapsed}
                />
              ) : (
                <li key={`${group.label}:${item.to}:${item.label}`}>
                  <EnlaceNav item={item} collapsed={collapsed} />
                </li>
              ),
            )}
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

/** Enlace de navegación hoja (ítem sin hijos, o un hijo dentro de un grupo). */
function EnlaceNav({ item, collapsed }: { item: NavItem; collapsed: boolean }) {
  return (
    <NavLink
      to={item.to}
      end
      className={({ isActive }) => (isActive ? "current" : undefined)}
      title={collapsed ? item.label : undefined}
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
function GrupoColapsable({ item, collapsed }: { item: NavItem; collapsed: boolean }) {
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
          <EnlaceNav item={item} collapsed />
        </li>
        {item.children?.map((hijo) => (
          <li key={hijo.to}>
            <EnlaceNav item={hijo} collapsed />
          </li>
        ))}
      </>
    );
  }

  const idHijos = `nav-grupo-${item.to}`;

  return (
    <li className="adoc-nav-group">
      <div className="adoc-nav-grouprow">
        <EnlaceNav item={item} collapsed={false} />
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
              <EnlaceNav item={hijo} collapsed={false} />
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}
