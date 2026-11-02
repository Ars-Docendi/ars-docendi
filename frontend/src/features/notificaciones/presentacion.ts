import type { Notificacion, TipoEntidadNotificacion } from "./types";

export type EtiquetaGrupo = "Hoy" | "Esta semana" | "Anteriores";
export const ORDEN_GRUPOS: EtiquetaGrupo[] = ["Hoy", "Esta semana", "Anteriores"];

function inicioDeDia(fecha: Date): Date {
  return new Date(fecha.getFullYear(), fecha.getMonth(), fecha.getDate());
}

/** Today, this week (it starts on Monday) or earlier. An invalid date falls in "Anteriores". */
export function grupoDeFecha(creadaEn: string, ahora: Date): EtiquetaGrupo {
  const fecha = new Date(creadaEn);
  if (Number.isNaN(fecha.getTime())) return "Anteriores";
  const hoy = inicioDeDia(ahora);
  const manana = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() + 1);
  if (fecha >= hoy && fecha < manana) return "Hoy";
  const diasDesdeLunes = (hoy.getDay() + 6) % 7;
  const inicioSemana = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() - diasDesdeLunes);
  return fecha >= inicioSemana && fecha <= ahora ? "Esta semana" : "Anteriores";
}

export interface GrupoNotificaciones {
  etiqueta: EtiquetaGrupo;
  notificaciones: Notificacion[];
}

/** Keeps the incoming order inside each group and drops the empty ones. */
export function agruparPorFecha(
  notificaciones: Notificacion[],
  ahora: Date,
): GrupoNotificaciones[] {
  const porGrupo = new Map<EtiquetaGrupo, Notificacion[]>(ORDEN_GRUPOS.map((g) => [g, []]));
  for (const n of notificaciones) porGrupo.get(grupoDeFecha(n.creadaEn, ahora))!.push(n);
  return ORDEN_GRUPOS.map((etiqueta) => ({
    etiqueta,
    notificaciones: porGrupo.get(etiqueta)!,
  })).filter((g) => g.notificaciones.length > 0);
}

export function textoInsignia(cantidad: number): string {
  return cantidad > 99 ? "99+" : String(cantidad);
}

export function etiquetaCampana(cantidad: number): string {
  if (cantidad === 0) return "Notificaciones, ninguna sin leer";
  return `Notificaciones, ${cantidad} ${cantidad === 1 ? "notificación sin leer" : "notificaciones sin leer"}`;
}

export function rutaDeEntidad(tipo: TipoEntidadNotificacion, entidadId: string): string {
  switch (tipo) {
    case "SOLICITUD_AULA":
      return `/aulas/solicitudes/${entidadId}`;
    case "PEDIDO_DESIGNACION":
      return `/designaciones/pedidos/${entidadId}`;
    case "TAREA":
      return `/tareas/${entidadId}`;
  }
}

export const ENLACE_POR_ENTIDAD: Record<TipoEntidadNotificacion, string> = {
  SOLICITUD_AULA: "Ir a la solicitud →",
  PEDIDO_DESIGNACION: "Ir al pedido →",
  TAREA: "Ir a la tarea →",
};

/** Singular noun used in the rollback toast. */
export const NOMBRE_ENTIDAD: Record<TipoEntidadNotificacion, string> = {
  SOLICITUD_AULA: "solicitud",
  PEDIDO_DESIGNACION: "pedido",
  TAREA: "tarea",
};

export const ETIQUETA_ENTIDAD_OCULTA: Record<TipoEntidadNotificacion, string> = {
  SOLICITUD_AULA: "Solicitud de aula",
  PEDIDO_DESIGNACION: "Pedido",
  TAREA: "Tarea",
};

/** Accessible description of a notification for screen readers. */
export function descripcionAccesible(n: Notificacion): string {
  const numero =
    typeof n.contexto?.numero === "string" || typeof n.contexto?.numero === "number"
      ? ` ${n.contexto.numero}`
      : "";
  return `${n.leidaEn === null ? "Notificación no leída" : "Notificación leída"}. ${ETIQUETA_ENTIDAD_OCULTA[n.tipoEntidad]}${numero}.`;
}
