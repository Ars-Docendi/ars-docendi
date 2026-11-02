import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button, InlineAlert, Modal, RoleMenu } from "@ars-docendi/ui";

import { mensajeProblema } from "../../shared/api/problemDetails";
import { useCerrarSesion } from "../../shared/auth/auth";
import { etiquetaAmbito, useElegirRol } from "../../shared/auth/rolActivo";
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
 * Tarjeta de usuario al pie de la barra lateral (y del panel móvil), con el rol y
 * el ámbito con que opera. Al tocarla abre hacia arriba el menú con "Cambiar rol"
 * (sólo con más de una asignación) y "Cerrar sesión".
 */
export function MenuUsuario({ user, collapsed = false }: MenuUsuarioProps) {
  const navigate = useNavigate();
  const cerrarSesion = useCerrarSesion();
  const [abierto, setAbierto] = useState(false);
  const [cambiandoRol, setCambiandoRol] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const ambito = user.asignacion ? etiquetaAmbito(user.asignacion) : "";
  const rolYAmbito = [user.role, ambito].filter(Boolean).join(", ");

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
          {user.asignaciones.length > 1 && (
            <Button
              variant="ghost"
              size="sm"
              role="menuitem"
              onClick={() => {
                setAbierto(false);
                setCambiandoRol(true);
              }}
            >
              Cambiar rol
            </Button>
          )}
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
        aria-label={`Menú de usuario: ${user.name}, ${rolYAmbito}`}
        title={`${user.name} · ${rolYAmbito}`}
        onClick={() => setAbierto((previo) => !previo)}
      >
        <span className="avatar" aria-hidden>
          {user.initials}
        </span>
        {!collapsed && (
          <span className="datos" aria-hidden>
            <span className="nombre">{nombreCorto(user.name)}</span>
            <span className="rol">{user.role}</span>
            <span className="ambito">{ambito}</span>
          </span>
        )}
        {!collapsed && (
          <span className={`chev${abierto ? " abierto" : ""}`} aria-hidden>
            {chevronIcon}
          </span>
        )}
      </button>
      {cambiandoRol && <ModalCambiarRol user={user} onCerrar={() => setCambiandoRol(false)} />}
    </div>
  );
}

/**
 * Elige con qué rol trabajar. Se monta sólo al abrirlo: desde "Cambiar rol" o, al
 * ingresar con varias asignaciones, sobre la app con la de por defecto marcada.
 * Al ingresar, cerrarlo confirma la de por defecto.
 */
export function ModalCambiarRol({
  user,
  alIngresar = false,
  onCerrar,
}: {
  user: CurrentUser;
  alIngresar?: boolean;
  onCerrar: () => void;
}) {
  const navigate = useNavigate();
  const elegir = useElegirRol();
  const actual = user.asignacion?.id;
  function elegirAsignacion(id: string) {
    if (id === actual && !alIngresar) return onCerrar();
    elegir.mutate(id, {
      onSuccess: () => {
        onCerrar();
        if (id !== actual) navigate("/", { replace: true });
      },
    });
  }
  return (
    <Modal
      open
      onOpenChange={(abrir) =>
        !abrir && (alIngresar && actual ? elegirAsignacion(actual) : onCerrar())
      }
      title={alIngresar ? "¿Con qué rol querés trabajar?" : "Cambiar rol"}
    >
      {alIngresar && (
        <p className="adoc-elegir-rol-ayuda">
          Tenés más de un rol. Podés cambiarlo después desde tu usuario, al pie de la barra lateral.
        </p>
      )}
      {elegir.isError && (
        <InlineAlert
          severity="danger"
          title={mensajeProblema(elegir.error, "No se pudo cambiar el rol. Intentá de nuevo.")}
        />
      )}
      <RoleMenu
        className="adoc-elegir-rol"
        heading={null}
        selectedId={actual}
        options={user.asignaciones.map((a) => ({
          id: a.id,
          name: a.rolNombre,
          scope: etiquetaAmbito(a),
          current: a.id === actual,
          disabled: elegir.isPending,
        }))}
        onSelect={elegirAsignacion}
      />
    </Modal>
  );
}
