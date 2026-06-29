import type { CSSProperties } from "react";
import type { ActorContexto, PedidoDesignacion } from "../types";
import { ColumnaKanban } from "./ColumnaKanban";
import { construirColumnas, esTuTurno } from "./tableroRevisionModelo";
import type { FiltrosTablero } from "./filtrosTablero";
import { aplicarFiltros } from "./filtrosTablero";
import "./revision.css";

interface TableroRevisionProps {
  pedidos: PedidoDesignacion[];
  actor: ActorContexto;
  filtros: FiltrosTablero;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

/**
 * Kanban de revisión (sin drag, opción D). Columnas por ESTADO DE AVANCE,
 * iguales para todos: En revisión (toda la cadena, con `x/4` y "Tu turno") ·
 * Aceptados · Devueltos · Rechazados. La vista "mis-pendientes" acota a los
 * pedidos en turno del actor; "completa" muestra todo el ámbito. Los filtros de
 * tipo/prioridad acotan las cards.
 */
export function TableroRevision({ pedidos, actor, filtros, onSeleccionar }: TableroRevisionProps) {
  const filtrados = aplicarFiltros(pedidos, filtros);
  const visibles =
    filtros.vista === "mis-pendientes"
      ? filtrados.filter((pedido) => esTuTurno(pedido, actor))
      : filtrados;
  const columnas = construirColumnas(visibles, actor);
  const estilo: CSSProperties = {
    gridTemplateColumns: `repeat(${columnas.length}, minmax(0, 1fr))`,
  };

  return (
    <div className="adoc-tablero" style={estilo}>
      {columnas.map((columna) => (
        <ColumnaKanban
          key={columna.id}
          columna={columna}
          actor={actor}
          onSeleccionar={onSeleccionar}
        />
      ))}
    </div>
  );
}
