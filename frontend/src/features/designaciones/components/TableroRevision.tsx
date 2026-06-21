import type { CSSProperties } from "react";
import type { ActorContexto, PedidoDesignacion } from "../types";
import { ColumnaKanban } from "./ColumnaKanban";
import { construirColumnas } from "./tableroRevisionModelo";
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
 * Kanban de revisión (sin drag). Las columnas son RELATIVAS AL ROL (pipeline:
 * Pendientes → Etapa siguiente → Aceptados → Devueltos; + Rechazados en la vista
 * completa). Los filtros de tipo/prioridad acotan las cards.
 */
export function TableroRevision({ pedidos, actor, filtros, onSeleccionar }: TableroRevisionProps) {
  const filtrados = aplicarFiltros(pedidos, filtros);
  const columnas = construirColumnas(filtrados, actor, filtros.vista === "completa");
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
