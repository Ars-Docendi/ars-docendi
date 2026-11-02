import type { Notificacion, PaginaNotificaciones } from "./types";

/** Test-only builders (never imported by runtime code). */
let secuencia = 0;

export function notificacion(sobrescribir: Partial<Notificacion> = {}): Notificacion {
  secuencia += 1;
  return {
    id: `00000000-0000-4000-8000-${String(secuencia).padStart(12, "0")}`,
    rolDestino: null,
    tipoEvento: "TAREA_ASIGNADA",
    tipoEntidad: "TAREA",
    entidadId: `10000000-0000-4000-8000-${String(secuencia).padStart(12, "0")}`,
    titulo: `Tarea asignada ${secuencia}`,
    mensaje: `Se te asignó la tarea N° ${secuencia}: Informe.`,
    contexto: { numero: secuencia },
    leidaEn: null,
    creadaEn: new Date().toISOString(),
    ...sobrescribir,
  };
}

export function pagina(
  datos: Notificacion[],
  { pagina = 1, totalPaginas = 1, total = datos.length, tamanio = 20 } = {},
): PaginaNotificaciones {
  return { datos, total, pagina, tamanio, totalPaginas };
}
