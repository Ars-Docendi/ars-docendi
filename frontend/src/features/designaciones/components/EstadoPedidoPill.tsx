import type { ReactElement } from "react";
import type { EstadoPedido } from "../types";
import "./estado-acciones.css";
import {
  IconoBan,
  IconoCircleCheck,
  IconoCircleX,
  IconoClock,
  IconoCornerUpLeft,
  IconoFilePen,
} from "./lucide";

type TonoPill = "neutro" | "exito" | "peligro" | "alerta";

interface ConfigEstado {
  etiqueta: string;
  tono: TonoPill;
  icono: ReactElement;
}

// Mapeo estado → pill del diseño (etiqueta + icono Lucide + tono). en_lote → "Aprobado".
const CONFIG: Record<EstadoPedido, ConfigEstado> = {
  borrador: { etiqueta: "Borrador", tono: "neutro", icono: <IconoFilePen /> },
  en_revision_coordinador: {
    etiqueta: "En revisión · Coordinador",
    tono: "neutro",
    icono: <IconoClock />,
  },
  en_revision_secretaria: {
    etiqueta: "En revisión · Secretaría",
    tono: "neutro",
    icono: <IconoClock />,
  },
  en_revision_decanato: {
    etiqueta: "En revisión · Decanato",
    tono: "neutro",
    icono: <IconoClock />,
  },
  en_lote: { etiqueta: "Aprobado", tono: "exito", icono: <IconoCircleCheck /> },
  rechazado: { etiqueta: "Rechazado", tono: "peligro", icono: <IconoCircleX /> },
  devuelto: { etiqueta: "Devuelto", tono: "alerta", icono: <IconoCornerUpLeft /> },
  cancelado: { etiqueta: "Cancelado", tono: "neutro", icono: <IconoBan /> },
};

/** Pill de estado del pedido (estilo del screens.pen): icono Lucide + etiqueta. */
export function EstadoPedidoPill({ estado }: { estado: EstadoPedido }) {
  const { etiqueta, tono, icono } = CONFIG[estado];
  return (
    <span className={`adoc-estado-pill ${tono}`}>
      {icono}
      {etiqueta}
    </span>
  );
}
