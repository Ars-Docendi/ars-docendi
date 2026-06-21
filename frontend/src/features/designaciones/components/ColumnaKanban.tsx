import type { CSSProperties } from "react";
import type { PedidoDesignacion } from "../types";
import { PedidoCard } from "./PedidoCard";

interface ColumnaKanbanProps {
  titulo: string;
  pedidos: PedidoDesignacion[];
  vacioLabel: string;
  onSeleccionar: (pedido: PedidoDesignacion) => void;
}

const estiloColumna: CSSProperties = {
  display: "flex",
  flexDirection: "column",
  gap: "var(--space-2)",
  padding: "var(--space-2)",
  background: "var(--color-bg-sunken)",
  borderRadius: "var(--radius-sm)",
  minWidth: 0,
};

/** Columna del Kanban de revisión (sin drag): título + cuenta + lista de cards. */
export function ColumnaKanban({ titulo, pedidos, vacioLabel, onSeleccionar }: ColumnaKanbanProps) {
  return (
    <section style={estiloColumna} aria-label={titulo}>
      <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h2
          style={{
            margin: 0,
            fontSize: "var(--text-body-sm-size)",
            color: "var(--color-text-secondary)",
          }}
        >
          {titulo}
        </h2>
        <span
          style={{ color: "var(--color-text-tertiary)", fontSize: "var(--text-body-sm-size)" }}
          aria-label={`${pedidos.length} pedidos`}
        >
          {pedidos.length}
        </span>
      </header>
      {pedidos.length === 0 ? (
        <p
          style={{
            margin: 0,
            color: "var(--color-text-tertiary)",
            fontSize: "var(--text-body-sm-size)",
          }}
        >
          {vacioLabel}
        </p>
      ) : (
        pedidos.map((pedido) => (
          <PedidoCard key={pedido.id} pedido={pedido} onSeleccionar={onSeleccionar} />
        ))
      )}
    </section>
  );
}
