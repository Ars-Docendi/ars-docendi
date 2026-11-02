import type { ReactNode } from "react";

import type { TipoEventoNotificacion } from "../types";
import {
  IconoBandeja,
  IconoCirculoX,
  IconoDevolver,
  IconoMensaje,
  IconoProhibido,
  IconoPuerta,
  IconoRefrescar,
  IconoUsuarioCheck,
} from "./iconos";

export const ICONO_POR_EVENTO: Record<TipoEventoNotificacion, () => ReactNode> = {
  AULA_ASIGNADA: IconoPuerta,
  AULA_ACTUALIZADA: IconoRefrescar,
  SOLICITUD_RECHAZADA: IconoCirculoX,
  RESERVA_CANCELADA: IconoProhibido,
  SOLICITUD_CANCELADA_DOCENTE: IconoProhibido,
  PEDIDO_PARA_REVISAR: IconoBandeja,
  PEDIDO_RECHAZADO: IconoCirculoX,
  PEDIDO_DEVUELTO: IconoDevolver,
  TAREA_ASIGNADA: IconoUsuarioCheck,
  TAREA_ESTADO_CAMBIADO: IconoRefrescar,
  TAREA_COMENTADA: IconoMensaje,
};
