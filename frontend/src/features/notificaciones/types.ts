/** Read state tabs of the panel; the same values the API accepts in `estado`. */
export type EstadoLectura = "NO_LEIDAS" | "LEIDAS";

export type TipoEntidadNotificacion = "SOLICITUD_AULA" | "PEDIDO_DESIGNACION" | "TAREA";

export type TipoEventoNotificacion =
  | "AULA_ASIGNADA"
  | "AULA_ACTUALIZADA"
  | "SOLICITUD_RECHAZADA"
  | "RESERVA_CANCELADA"
  | "SOLICITUD_CANCELADA_DOCENTE"
  | "PEDIDO_PARA_REVISAR"
  | "PEDIDO_RECHAZADO"
  | "PEDIDO_DEVUELTO"
  | "TAREA_ASIGNADA"
  | "TAREA_ESTADO_CAMBIADO"
  | "TAREA_COMENTADA";

export interface Notificacion {
  id: string;
  /** Profile under which it is visible; `null` means any profile. */
  rolDestino: string | null;
  tipoEvento: TipoEventoNotificacion;
  tipoEntidad: TipoEntidadNotificacion;
  entidadId: string;
  titulo: string;
  mensaje: string;
  contexto: Record<string, unknown>;
  leidaEn: string | null;
  creadaEn: string;
}

export interface PaginaNotificaciones {
  datos: Notificacion[];
  total: number;
  pagina: number;
  tamanio: number;
  totalPaginas: number;
}

/** What "mark as read" sends: explicit ids, or everything up to an instant. */
export type SolicitudMarcarLeidas = { ids: string[] } | { todas: true; hasta: string };

export const TAMANIO_PAGINA = 20;
export const MAXIMO_IDS_POR_PEDIDO = 100;
