import type { CSSProperties } from "react";
import type { PedidoDesignacion } from "../types";
import { EstadoPedidoBadge } from "./EstadoPedidoBadge";

interface PedidoCardProps {
  pedido: PedidoDesignacion;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

const estiloCard: CSSProperties = {
  display: "flex",
  flexDirection: "column",
  gap: "var(--space-1)",
  width: "100%",
  textAlign: "left",
  padding: "var(--space-2)",
  border: "1px solid var(--color-border-subtle)",
  borderRadius: "var(--radius-sm)",
  background: "var(--color-bg-surface)",
  cursor: "pointer",
};

/** Card del Kanban de revisión: docente + cátedra + novedad + estado. Click → detalle. */
export function PedidoCard({ pedido, onSeleccionar }: PedidoCardProps) {
  return (
    <button
      type="button"
      style={estiloCard}
      onClick={() => onSeleccionar(pedido)}
      aria-label={`Ver el pedido de ${pedido.docente.nombre}`}
    >
      <strong>{pedido.docente.nombre}</strong>
      <span style={{ color: "var(--color-text-tertiary)", fontSize: "var(--text-body-sm-size)" }}>
        {pedido.catedra} · {pedido.carrera}
      </span>
      <span style={{ color: "var(--color-text-secondary)", fontSize: "var(--text-body-sm-size)" }}>
        {pedido.novedad}
      </span>
      <EstadoPedidoBadge estado={pedido.estado} prioritario={pedido.prioritario} />
    </button>
  );
}
