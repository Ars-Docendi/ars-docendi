import type { ReactElement } from "react";
import type { EstadoSolicitudAula } from "../types";
import "./estado-solicitud.css";
import { IconoBan, IconoCircleCheck, IconoCircleX, IconoClock } from "./lucide";

type TonoPill = "neutro" | "exito" | "peligro";

interface ConfigEstado {
  etiqueta: string;
  tono: TonoPill;
  icono: ReactElement;
}

const CONFIG: Record<EstadoSolicitudAula, ConfigEstado> = {
  pendiente: { etiqueta: "Pendiente", tono: "neutro", icono: <IconoClock /> },
  aprobada: { etiqueta: "Aprobada", tono: "exito", icono: <IconoCircleCheck /> },
  rechazada: { etiqueta: "Rechazada", tono: "peligro", icono: <IconoCircleX /> },
  cancelada: { etiqueta: "Cancelada", tono: "peligro", icono: <IconoBan /> },
};

/** Pill de estado de la solicitud de reserva de aula: ícono Lucide + etiqueta. */
export function EstadoSolicitudPill({ estado }: { estado: EstadoSolicitudAula }) {
  const { etiqueta, tono, icono } = CONFIG[estado];
  return (
    <span className={`adoc-estado-pill ${tono}`}>
      {icono}
      {etiqueta}
    </span>
  );
}
