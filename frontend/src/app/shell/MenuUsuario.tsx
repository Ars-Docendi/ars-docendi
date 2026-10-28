import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@ars-docendi/ui";

import { useCerrarSesion } from "../../shared/auth/auth";
import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { chevronIcon } from "./icons";

/**
 * Nombre corto para la tarjeta: primer nombre y primer apellido a partir del
 * formato institucional "Apellidos, Nombres". Sin coma, queda como viene.
 */
function nombreCorto(nombreCompleto: string): string {
  const [apellidos, nombres] = nombreCompleto.split(",").map((parte) => parte.trim());
  if (!apellidos || !nombres) return nombreCompleto;
  return `${nombres.split(/\s+/)[0]} ${apellidos.split(/\s+/)[0]}`;
}

interface MenuUsuarioProps {
  user: CurrentUser;
  collapsed?: boolean;
}

/**
 * Tarjeta de usuario al pie de la barra lateral (y del panel móvil). Al
 * tocarla abre hacia arriba el menú con "Cerrar sesión".
 */
export function MenuUsuario({ user, collapsed = false }: MenuUsuarioProps) {
  const navigate = useNavigate();
  const cerrarSesion = useCerrarSesion();
  const [abierto, setAbierto] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!abierto) return;
    function cerrarAlTocarAfuera(event: MouseEvent) {
      if (ref.current && !ref.current.contains(event.target as Node)) setAbierto(false);
    }
    function cerrarConEscape(event: KeyboardEvent) {
      if (event.key === "Escape") setAbierto(false);
    }
    document.addEventListener("mousedown", cerrarAlTocarAfuera);
    document.addEventListener("keydown", cerrarConEscape);
    return () => {
      document.removeEventListener("mousedown", cerrarAlTocarAfuera);
      document.removeEventListener("keydown", cerrarConEscape);
    };
  }, [abierto]);

  async function salir() {
    try {
      await cerrarSesion();
    } finally {
      navigate("/login", { replace: true });
    }
  }

  return (
    <div className="adoc-usuario" ref={ref}>
      {abierto && (
        <div className="adoc-usuario-pop" role="menu">
          <Button variant="ghost" size="sm" role="menuitem" onClick={() => void salir()}>
            Cerrar sesión
          </Button>
        </div>
      )}
      <button
        type="button"
        className="adoc-usuario-tarjeta"
        aria-haspopup="menu"
        aria-expanded={abierto}
        aria-label={`Menú de usuario: ${user.name}, ${user.role}`}
        title={`${user.name} · ${user.role}`}
        onClick={() => setAbierto((previo) => !previo)}
      >
        <span className="avatar" aria-hidden>
          {user.initials}
        </span>
        {!collapsed && (
          <span className="datos" aria-hidden>
            <span className="nombre">{nombreCorto(user.name)}</span>
            <span className="rol">{user.role}</span>
          </span>
        )}
        {!collapsed && (
          <span className={`chev${abierto ? " abierto" : ""}`} aria-hidden>
            {chevronIcon}
          </span>
        )}
      </button>
    </div>
  );
}
