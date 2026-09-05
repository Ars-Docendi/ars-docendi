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
  /**
   * Reemplaza la etiqueta por defecto del estado. Lo usa la Tabla de revisión
   * para que un Devuelto diga de quién depende que avance ("Devuelto — corregís
   * vos") en vez del genérico "Devuelto", sin duplicar el mapeo de kinds.
   */
  etiqueta?: string;
}

/** Badge de estado del pedido (+ badge extra de prioridad). */
export function EstadoPedidoBadge({
  estado,
  prioritario = false,
  etiqueta,
}: EstadoPedidoBadgeProps) {
  return (
    <span className="adoc-estado-badges">
      <StatusBadge kind={KIND_POR_ESTADO[estado]} label={etiqueta ?? ETIQUETA_POR_ESTADO[estado]} />
      {prioritario && <StatusBadge kind="prioritario" label="Prioritario" />}
    </span>
  );
}
