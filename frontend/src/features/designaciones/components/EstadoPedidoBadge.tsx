import { StatusBadge } from "@ars-docendi/ui";
import type { StatusKind } from "@ars-docendi/ui";
import type { EstadoPedido } from "../types";

// Mapeo EstadoPedido → StatusKind de la librería (§6.6 del plan).
const KIND_POR_ESTADO: Record<EstadoPedido, StatusKind> = {
  borrador: "pendiente",
  en_revision_coordinador: "revision",
  en_revision_secretaria: "revision",
  en_revision_decanato: "revision",
  devuelto: "devuelto",
  en_lote: "aprobado",
  rechazado: "rechazado",
  cancelado: "cancelado",
};

const ETIQUETA_POR_ESTADO: Record<EstadoPedido, string> = {
  borrador: "Borrador",
  en_revision_coordinador: "En revisión · Coordinador",
  en_revision_secretaria: "En revisión · Secretaría",
  en_revision_decanato: "En revisión · Decanato",
  devuelto: "Devuelto",
  en_lote: "En lote",
  rechazado: "Rechazado",
  cancelado: "Cancelado",
};

interface EstadoPedidoBadgeProps {
  estado: EstadoPedido;
  prioritario?: boolean;
}

/** Badge de estado del pedido (+ badge extra de prioridad). */
export function EstadoPedidoBadge({ estado, prioritario = false }: EstadoPedidoBadgeProps) {
  return (
    <span style={{ display: "inline-flex", gap: "var(--space-1)", alignItems: "center" }}>
      <StatusBadge kind={KIND_POR_ESTADO[estado]} label={ETIQUETA_POR_ESTADO[estado]} />
      {prioritario && <StatusBadge kind="prioritario" label="Prioritario" />}
    </span>
  );
}
