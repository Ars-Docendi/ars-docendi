import type { ReactElement } from "react";
import type { EstadoProyecto } from "../types";
import { IconoBan, IconoCircleCheck, IconoCircleDot } from "./lucide";
import "./estadoTarea.css";

type TonoPill = "neutro" | "info" | "alerta" | "exito" | "peligro";

interface ConfigEstado {
  etiqueta: string;
  tono: TonoPill;
  icono: ReactElement;
}

const CONFIG: Record<EstadoProyecto, ConfigEstado> = {
  abierto: { etiqueta: "Abierto", tono: "info", icono: <IconoCircleDot /> },
  finalizado: { etiqueta: "Finalizado", tono: "exito", icono: <IconoCircleCheck /> },
  cancelado: { etiqueta: "Cancelado", tono: "peligro", icono: <IconoBan /> },
};

/** Badge de estado del proyecto: ícono + etiqueta. Mismo estilo que `EstadoTareaBadge`. */
export function EstadoProyectoBadge({ estado }: { estado: EstadoProyecto }) {
  const { etiqueta, tono, icono } = CONFIG[estado];
  return (
    <span className={`adoc-tarea-pill ${tono}`}>
      {icono}
      {etiqueta}
    </span>
  );
}
