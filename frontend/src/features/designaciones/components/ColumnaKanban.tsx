import type { ActorContexto, PedidoDesignacion } from "../types";
import { PedidoCard } from "./PedidoCard";
import type { ColumnaTablero } from "./tableroRevisionModelo";

const CLASE_DOT: Record<ColumnaTablero["tono"], string> = {
  acento: "acento",
  neutro: "neutro",
  exito: "exito",
  alerta: "alerta",
  peligro: "peligro",
};

interface ColumnaKanbanProps {
  columna: ColumnaTablero;
  actor: ActorContexto;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

/** Columna del Kanban (sin drag): dot + título/hint + count + lista de cards. */
export function ColumnaKanban({ columna, actor, onSeleccionar }: ColumnaKanbanProps) {
  return (
    <section className="adoc-kanban-col" aria-label={columna.titulo}>
      <header className="adoc-kanban-col-head">
        <span className="adoc-kanban-col-headl">
          <span className={`adoc-kanban-dot ${CLASE_DOT[columna.tono]}`} aria-hidden="true" />
          <span className="adoc-kanban-col-tw">
            <span className="adoc-kanban-col-title">{columna.titulo}</span>
            <span className="adoc-kanban-col-sub">{columna.subtitulo}</span>
          </span>
        </span>
        <span className="adoc-kanban-col-count" aria-label={`${columna.pedidos.length} pedidos`}>
          {columna.pedidos.length}
        </span>
      </header>
      {columna.pedidos.length === 0 ? (
        <p className="adoc-kanban-col-empty">Sin pedidos</p>
      ) : (
        columna.pedidos.map((pedido) => (
          <PedidoCard key={pedido.id} pedido={pedido} actor={actor} onSeleccionar={onSeleccionar} />
        ))
      )}
    </section>
  );
}
