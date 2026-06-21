import type { ActorContexto, PedidoDesignacion } from "../types";
import { NovedadChip, PrioridadFlag } from "./NovedadChip";
import { detallePedido, inicialesDocente, situacionPedido } from "./tableroRevisionModelo";

interface PedidoCardProps {
  pedido: PedidoDesignacion;
  actor: ActorContexto;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

/** Card del Kanban de revisión: novedad + prioridad + docente + detalle + situación. Click → detalle. */
export function PedidoCard({ pedido, actor, onSeleccionar }: PedidoCardProps) {
  return (
    <button
      type="button"
      className="adoc-pedido-card"
      onClick={() => onSeleccionar(pedido)}
      aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
    >
      <span className="adoc-pedido-top">
        <NovedadChip novedad={pedido.novedad} />
        {pedido.prioritario && <PrioridadFlag />}
      </span>

      <span className="adoc-pedido-nombre">Prof. {pedido.docente.nombre}</span>
      <span className="adoc-pedido-materia">{pedido.materiaAsociada}</span>
      <span className="adoc-pedido-detalle">{detallePedido(pedido)}</span>

      <span className="adoc-pedido-divisor" aria-hidden="true" />

      <span className="adoc-pedido-foot">
        <span className="adoc-pedido-situacion">{situacionPedido(pedido, actor)}</span>
        <span className="adoc-pedido-avatar" aria-hidden="true">
          {inicialesDocente(pedido.docente.nombre)}
        </span>
      </span>
    </button>
  );
}
