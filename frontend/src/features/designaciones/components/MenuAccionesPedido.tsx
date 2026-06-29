import { useEffect, useRef, useState } from "react";
import type { PedidoDesignacion } from "../types";
import "./estado-acciones.css";
import {
  IconoBan,
  IconoCornerUpRight,
  IconoEllipsisVertical,
  IconoEye,
  IconoPencil,
  IconoSend,
} from "./lucide";

interface MenuAccionesPedidoProps {
  pedido: PedidoDesignacion;
  onVerDetalle: (pedido: PedidoDesignacion) => void;
  onEditar: (pedido: PedidoDesignacion) => void;
  onEnviar: (pedido: PedidoDesignacion) => void;
  onCancelar: (pedido: PedidoDesignacion) => void;
  onReenviar: (pedido: PedidoDesignacion) => void;
}

// Mismos guards de la UI que ya usaba la tabla (alineados a la máquina de estados).
function puedeEditar(p: PedidoDesignacion): boolean {
  return (
    p.estado === "borrador" ||
    (p.estado === "devuelto" && p.propietarioActual === "Jefe de Cátedra")
  );
}
function puedeEnviar(p: PedidoDesignacion): boolean {
  return p.estado === "borrador";
}
function puedeCancelar(p: PedidoDesignacion): boolean {
  return p.estado === "borrador";
}
function puedeReenviar(p: PedidoDesignacion): boolean {
  return p.estado === "devuelto" && p.propietarioActual === "Jefe de Cátedra";
}

/** Menú kebab (⋮) con las acciones contextuales del pedido según su estado. */
export function MenuAccionesPedido({
  pedido,
  onVerDetalle,
  onEditar,
  onEnviar,
  onCancelar,
  onReenviar,
}: MenuAccionesPedidoProps) {
  const [abierto, setAbierto] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!abierto) return;
    function onPointer(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setAbierto(false);
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") setAbierto(false);
    }
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [abierto]);

  function ejecutar(accion: (pedido: PedidoDesignacion) => void) {
    setAbierto(false);
    accion(pedido);
  }

  return (
    <div className="adoc-menu-acc" ref={ref}>
      <button
        type="button"
        className="adoc-menu-acc-trigger"
        aria-label={`Acciones del pedido de ${pedido.docente.nombre}`}
        aria-haspopup="menu"
        aria-expanded={abierto}
        onClick={() => setAbierto((o) => !o)}
      >
        <IconoEllipsisVertical />
      </button>
      {abierto && (
        <div className="adoc-menu-acc-pop" role="menu">
          <button type="button" role="menuitem" onClick={() => ejecutar(onVerDetalle)}>
            <IconoEye /> Ver detalle
          </button>
          {puedeEditar(pedido) && (
            <button type="button" role="menuitem" onClick={() => ejecutar(onEditar)}>
              <IconoPencil /> Editar
            </button>
          )}
          {puedeEnviar(pedido) && (
            <button type="button" role="menuitem" onClick={() => ejecutar(onEnviar)}>
              <IconoSend /> Enviar a revisión
            </button>
          )}
          {puedeReenviar(pedido) && (
            <button type="button" role="menuitem" onClick={() => ejecutar(onReenviar)}>
              <IconoCornerUpRight /> Reenviar a revisión
            </button>
          )}
          {puedeCancelar(pedido) && (
            <button
              type="button"
              role="menuitem"
              className="peligro"
              onClick={() => ejecutar(onCancelar)}
            >
              <IconoBan /> Cancelar
            </button>
          )}
        </div>
      )}
    </div>
  );
}
