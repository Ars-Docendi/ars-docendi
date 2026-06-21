import type { CSSProperties } from "react";
import { puedeRevisar } from "../api/maquinaEstados";
import type { ActorContexto, PedidoDesignacion } from "../types";
import { ColumnaKanban } from "./ColumnaKanban";

interface TableroRevisionProps {
  pedidos: PedidoDesignacion[];
  actor: ActorContexto;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

const estiloTablero: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "repeat(4, minmax(0, 1fr))",
  gap: "var(--space-3)",
  alignItems: "start",
};

/**
 * Kanban de revisión (sin drag). Las 4 columnas se derivan del estado de cada
 * pedido y del actor: "Pendiente (mi etapa)" usa el predicado de dominio
 * `puedeRevisar` (revisor de la etapa en su ámbito, o Administración).
 */
export function TableroRevision({ pedidos, actor, onSeleccionar }: TableroRevisionProps) {
  const pendientes = pedidos.filter((pedido) => puedeRevisar(pedido, actor));
  const aprobados = pedidos.filter((pedido) => pedido.estado === "en_lote");
  const rechazados = pedidos.filter((pedido) => pedido.estado === "rechazado");
  const devueltos = pedidos.filter((pedido) => pedido.estado === "devuelto");

  return (
    <div style={estiloTablero}>
      <ColumnaKanban
        titulo="Pendiente (mi etapa)"
        pedidos={pendientes}
        vacioLabel="Sin pedidos en tu etapa"
        onSeleccionar={onSeleccionar}
      />
      <ColumnaKanban
        titulo="Aprobado"
        pedidos={aprobados}
        vacioLabel="Sin pedidos aprobados"
        onSeleccionar={onSeleccionar}
      />
      <ColumnaKanban
        titulo="Rechazado"
        pedidos={rechazados}
        vacioLabel="Sin pedidos rechazados"
        onSeleccionar={onSeleccionar}
      />
      <ColumnaKanban
        titulo="Devuelto"
        pedidos={devueltos}
        vacioLabel="Sin pedidos devueltos"
        onSeleccionar={onSeleccionar}
      />
    </div>
  );
}
