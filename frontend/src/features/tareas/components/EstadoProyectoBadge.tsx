import type { ReactElement } from "react";
import type { EstadoProyecto } from "../types";
import { IconoBan, IconoCircleCheck, IconoCircleDot } from "./lucide";
import "./estadoTarea.css";

type TonoPill = "neutro" | "info" | "alerta" | "exito" | "peligro";

interface ConfigVisual {
  tono: TonoPill;
  icono: ReactElement;
}

// Solo el aspecto visual de los estados conocidos: el nombre que se muestra y el
// comportamiento (si admite tareas) vienen del catálogo del servidor. Un estado nuevo del
// catálogo, sin entrada acá, se muestra con estilo neutro.
const VISUAL: Record<string, ConfigVisual> = {
  abierto: { tono: "info", icono: <IconoCircleDot /> },
  finalizado: { tono: "exito", icono: <IconoCircleCheck /> },
  cancelado: { tono: "peligro", icono: <IconoBan /> },
};

/** Badge de estado del proyecto: ícono + nombre del catálogo. Mismo estilo que `EstadoTareaBadge`. */
export function EstadoProyectoBadge({
  estado,
  nombre,
}: {
  estado: EstadoProyecto;
  nombre: string;
}) {
  const { tono, icono } = VISUAL[estado] ?? { tono: "neutro" as const, icono: <IconoCircleDot /> };
  return (
    <span className={`adoc-tarea-pill ${tono}`}>
      {icono}
      {nombre}
    </span>
  );
}
