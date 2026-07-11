import type { ActorContexto, PedidoDesignacion } from "../types";
import { NovedadChip, PrioridadFlag, RechazadoChip } from "./NovedadChip";
import {
  avancePedido,
  detallePedido,
  esTuTurno,
  inicialesDocente,
  motivoRechazo,
  situacionPedido,
} from "./tableroRevisionModelo";
import { resumenMaterias } from "./detalleAdapters";

interface PedidoCardProps {
  pedido: PedidoDesignacion;
  actor: ActorContexto;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

/**
 * Card del Kanban de revisión (opción D): novedad + prioridad + "Tu turno" +
 * docente + detalle + avance `x/4` (en la cadena) + recencia. Click → detalle.
 */
export function PedidoCard({ pedido, actor, onSeleccionar }: PedidoCardProps) {
  const avance = avancePedido(pedido);
  const tuTurno = esTuTurno(pedido, actor);
  // El avance se muestra solo en la cadena de revisión (pasos 1..3); el cierre
  // "En lote · 4/4" lo comunica el subtítulo de la columna Aceptados.
  const mostrarAvance = avance !== null && avance.paso < avance.total;
  const marcaDerecha = pedido.prioritario || tuTurno;
  // Los rechazados llevan un distintivo de estado "Rechazado" (no el de novedad)
  // y muestran el motivo como cita destacada.
  const esRechazado = pedido.estado === "rechazado";
  const motivo = esRechazado ? motivoRechazo(pedido) : undefined;

  return (
    <button
      type="button"
      className={`adoc-pedido-card${tuTurno ? " tu-turno" : ""}`}
      onClick={() => onSeleccionar(pedido)}
      aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
    >
      <span className="adoc-pedido-top">
        {esRechazado ? <RechazadoChip /> : <NovedadChip novedad={pedido.novedad} />}
        {marcaDerecha && (
          <span className="adoc-pedido-top-right">
            {pedido.prioritario && <PrioridadFlag />}
            {tuTurno && <span className="adoc-pedido-turno">Tu turno</span>}
          </span>
        )}
      </span>

      <span className="adoc-pedido-nombre">Prof. {pedido.docente.nombre}</span>
      <span className="adoc-pedido-materia">{resumenMaterias(pedido.asignaciones)}</span>
      {esRechazado && motivo ? (
        <span className="adoc-pedido-motivo">{`“${motivo}”`}</span>
      ) : (
        <span className="adoc-pedido-detalle">{detallePedido(pedido)}</span>
      )}

      {mostrarAvance && avance && (
        <span className={`adoc-pedido-avance${tuTurno ? " tu-turno" : ""}`}>
          <span className="adoc-pedido-stepper" aria-hidden="true">
            {Array.from({ length: avance.total }, (_, indice) => (
              <span
                key={indice}
                className={`adoc-pedido-stepper-bar${indice < avance.paso ? " lleno" : ""}`}
              />
            ))}
          </span>
          <span className="adoc-pedido-avance-txt">
            {avance.etiqueta} · {avance.paso}/{avance.total}
          </span>
        </span>
      )}

      <span className="adoc-pedido-divisor" aria-hidden="true" />

      <span className="adoc-pedido-foot">
        <span className="adoc-pedido-situacion">{situacionPedido(pedido)}</span>
        <span className="adoc-pedido-avatar" aria-hidden="true">
          {inicialesDocente(pedido.docente.nombre)}
        </span>
      </span>
    </button>
  );
}
