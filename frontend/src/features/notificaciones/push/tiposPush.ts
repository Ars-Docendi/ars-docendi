import { rutaArticulo } from "../../ayuda/utils/rutas";

/** Help article that explains how to turn push on, on each phone. */
export const RUTA_AYUDA_PUSH = rutaArticulo(
  "primeros-pasos",
  "activar-las-notificaciones-en-el-celular",
);

/** Event types a user can switch on or off for push, grouped by the module that emits them. */

export interface TipoPush {
  id: string;
  etiqueta: string;
  descripcion: string;
}

export interface GrupoPush {
  modulo: string;
  titulo: string;
  /** Any of these permissions of the active role means the user can receive the group. */
  permisos: readonly string[];
  tipos: readonly TipoPush[];
}

export const GRUPOS_PUSH: readonly GrupoPush[] = [
  {
    modulo: "aulas",
    titulo: "Reserva de aulas",
    permisos: ["aulas.solicitar", "aulas.aprobar"],
    tipos: [
      {
        id: "AULA_ASIGNADA",
        etiqueta: "Aula asignada",
        descripcion: "Te asignaron un aula para tu solicitud.",
      },
      {
        id: "AULA_ACTUALIZADA",
        etiqueta: "Aula actualizada",
        descripcion: "Cambió el aula de tu solicitud.",
      },
      {
        id: "SOLICITUD_RECHAZADA",
        etiqueta: "Solicitud rechazada",
        descripcion: "Rechazaron tu solicitud de aula.",
      },
      {
        id: "RESERVA_CANCELADA",
        etiqueta: "Reserva cancelada",
        descripcion: "Cancelaron una reserva tuya.",
      },
      {
        id: "SOLICITUD_CANCELADA_DOCENTE",
        etiqueta: "Solicitud cancelada por el docente",
        descripcion: "Un docente canceló su solicitud y hay que liberar el aula.",
      },
    ],
  },
  {
    modulo: "designaciones",
    titulo: "Designaciones",
    permisos: ["designaciones.gestionar", "designaciones.revisar"],
    tipos: [
      {
        id: "PEDIDO_PARA_REVISAR",
        etiqueta: "Pedido para revisar",
        descripcion: "Un pedido pasó a tu etapa y espera tu revisión.",
      },
      {
        id: "PEDIDO_RECHAZADO",
        etiqueta: "Pedido rechazado",
        descripcion: "Rechazaron un pedido de tu materia.",
      },
      {
        id: "PEDIDO_DEVUELTO",
        etiqueta: "Pedido devuelto",
        descripcion: "Te devolvieron un pedido para que lo corrijas.",
      },
    ],
  },
  {
    modulo: "tareas",
    titulo: "Tareas",
    permisos: ["tareas.ver"],
    tipos: [
      { id: "TAREA_ASIGNADA", etiqueta: "Tarea asignada", descripcion: "Te asignaron una tarea." },
      {
        id: "TAREA_ESTADO_CAMBIADO",
        etiqueta: "Cambio de estado",
        descripcion: "Cambió el estado de una tarea tuya.",
      },
      {
        id: "TAREA_COMENTADA",
        etiqueta: "Comentario nuevo",
        descripcion: "Comentaron una tarea tuya.",
      },
    ],
  },
];

/** Groups the active role can receive; with no permission list (unknown) every group is shown. */
export function gruposVisibles(permisos: readonly string[] | undefined): readonly GrupoPush[] {
  if (permisos === undefined) return GRUPOS_PUSH;
  const efectivos = new Set(permisos);
  return GRUPOS_PUSH.filter((g) => g.permisos.some((p) => efectivos.has(p)));
}
