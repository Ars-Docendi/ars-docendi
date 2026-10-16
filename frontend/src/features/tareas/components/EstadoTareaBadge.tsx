import type { ReactElement } from "react";
import type { EstadoTarea } from "../types";
import {
  IconoBan,
  IconoCircleCheck,
  IconoCircleDot,
  IconoClock,
  IconoTriangleAlert,
} from "./lucide";
import "./estadoTarea.css";

type TonoPill = "neutro" | "info" | "alerta" | "exito" | "peligro";

interface ConfigEstado {
  etiqueta: string;
  tono: TonoPill;
  icono: ReactElement;
}

// Pausa usa el tono "alerta" (mismo naranja de advertencia que Designaciones)
// para que se distinga a simple vista en la columna Estado del listado —
// es la tarea que necesita atención de la autoridad creadora.
const CONFIG: Record<EstadoTarea, ConfigEstado> = {
  pendiente: { etiqueta: "Pendiente", tono: "neutro", icono: <IconoClock /> },
  en_curso: { etiqueta: "En curso", tono: "info", icono: <IconoCircleDot /> },
  pausa: { etiqueta: "Pausa", tono: "alerta", icono: <IconoTriangleAlert /> },
  resuelta: { etiqueta: "Resuelta", tono: "exito", icono: <IconoCircleCheck /> },
  cancelada: { etiqueta: "Cancelada", tono: "peligro", icono: <IconoBan /> },
};

/** Badge de estado de la tarea: ícono + etiqueta, con resaltado distintivo para Pausa. */
export function EstadoTareaBadge({ estado }: { estado: EstadoTarea }) {
  const { etiqueta, tono, icono } = CONFIG[estado];
  return (
    <span className={`adoc-tarea-pill ${tono}`}>
      {icono}
      {etiqueta}
    </span>
  );
}
