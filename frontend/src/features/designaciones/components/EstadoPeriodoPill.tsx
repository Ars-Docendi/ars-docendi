import type { ReactElement } from "react";
import type { EstadoPeriodo } from "../types";
import "./estado-acciones.css";
import { IconoCircleCheck, IconoTimer, IconoTriangleAlert } from "./lucide";

type TonoPill = "neutro" | "exito" | "peligro" | "alerta";

interface ConfigEstado {
  etiqueta: string;
  tono: TonoPill;
  icono: ReactElement;
}

// Mapeo estado del período → pill del diseño (etiqueta + icono Lucide + tono).
const CONFIG: Record<EstadoPeriodo, ConfigEstado> = {
  abierto: { etiqueta: "Abierto", tono: "exito", icono: <IconoCircleCheck /> },
  proximo: { etiqueta: "Próximo", tono: "alerta", icono: <IconoTriangleAlert /> },
  cerrado: { etiqueta: "Cerrado", tono: "neutro", icono: <IconoTimer /> },
};

/** Pill de estado del período (estilo del screens.pen): icono Lucide + etiqueta. */
export function EstadoPeriodoPill({ estado }: { estado: EstadoPeriodo }) {
  const { etiqueta, tono, icono } = CONFIG[estado];
  return (
    <span className={`adoc-estado-pill ${tono}`}>
      {icono}
      {etiqueta}
    </span>
  );
}
