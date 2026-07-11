import type { ActorContexto, PedidoDesignacion } from "../types";
import { construirColumnas, esTuTurno, inicialesDocente } from "./tableroRevisionModelo";
import type { FiltrosTablero } from "./filtrosTablero";
import { aplicarFiltros } from "./filtrosTablero";
import { NovedadChip, PrioridadFlagIcono } from "./NovedadChip";
import { EstadoAvance } from "./EstadoAvance";
import { resumenMaterias } from "./detalleAdapters";
import "./revision.css";

interface TablaRevisionProps {
  pedidos: PedidoDesignacion[];
  actor: ActorContexto;
  filtros: FiltrosTablero;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

/**
 * Vista Tabla del tablero de revisión (opción D): los MISMOS pedidos del Kanban
 * aplanados en una tabla plana ordenada por estado (En revisión → Aceptados →
 * Devueltos → Rechazados). Reusa `construirColumnas` (mismo orden/filtrado que el
 * Tablero) y la celda `EstadoAvance` (estado + avance combinados). La columna
 * Prioritario es solo-ícono. Cada fila es un botón que navega al detalle.
 */
export function TablaRevision({ pedidos, actor, filtros, onSeleccionar }: TablaRevisionProps) {
  const filtrados = aplicarFiltros(pedidos, filtros);
  const visibles =
    filtros.vista === "mis-pendientes"
      ? filtrados.filter((pedido) => esTuTurno(pedido, actor))
      : filtrados;
  const filas = construirColumnas(visibles, actor).flatMap((columna) => columna.pedidos);

  return (
    <div className="adoc-tabla">
      <div className="adoc-tabla-head" aria-hidden="true">
        <span className="adoc-tabla-h">Docente</span>
        <span className="adoc-tabla-h">Asignatura</span>
        <span className="adoc-tabla-h">Novedad</span>
        <span className="adoc-tabla-h">Estado</span>
        <span className="adoc-tabla-h" />
      </div>

      {filas.length === 0 ? (
        <p className="adoc-kanban-col-empty">Sin pedidos</p>
      ) : (
        filas.map((pedido) => (
          <button
            key={pedido.id}
            type="button"
            className="adoc-tabla-row"
            onClick={() => onSeleccionar(pedido)}
            aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
          >
            <span className="adoc-tabla-c adoc-tabla-docente">
              <span className="adoc-pedido-avatar" aria-hidden="true">
                {inicialesDocente(pedido.docente.nombre)}
              </span>
              <span className="adoc-tabla-nombre">Prof. {pedido.docente.nombre}</span>
            </span>
            <span className="adoc-tabla-c adoc-tabla-asig">
              {resumenMaterias(pedido.asignaciones)}
            </span>
            <span className="adoc-tabla-c">
              <NovedadChip novedad={pedido.novedad} />
            </span>
            <span className="adoc-tabla-c">
              <EstadoAvance pedido={pedido} />
            </span>
            <span className="adoc-tabla-c adoc-tabla-prio">
              {pedido.prioritario && <PrioridadFlagIcono />}
            </span>
          </button>
        ))
      )}
    </div>
  );
}
