// ============================================================
// Adapters de PRESENTACIÓN para el detalle de la tarea. Traducen el
// dominio (en español) a los tipos de @ars-docendi/ui (en inglés):
// `AuditVerb`. Funciones puras de vista — NO lógica de dominio.
// Espejo de `designaciones/components/detalleAdapters.ts`.
// ============================================================
import type { AuditEntry, AuditVerb } from "@ars-docendi/ui";
import type { AccionHistorialTarea, EventoHistorialTarea } from "../types";

const VERBO_POR_ACCION: Record<AccionHistorialTarea, AuditVerb> = {
  crear: "create",
  cambiar_estado: "update",
  editar_avance: "update",
  editar: "update",
};

const ETIQUETA_ESTADO: Record<string, string> = {
  pendiente: "Pendiente",
  en_curso: "En curso",
  pausa: "Pausa",
  resuelta: "Resuelta",
  cancelada: "Cancelada",
};

const ETIQUETA_POR_ACCION: Record<AccionHistorialTarea, (evento: EventoHistorialTarea) => string> =
  {
    crear: () => "Creó la tarea",
    cambiar_estado: (e) => `Cambió el estado a "${ETIQUETA_ESTADO[e.estado] ?? e.estado}"`,
    editar_avance: (e) => `Actualizó el avance${e.detalle ? ` a ${e.detalle}` : ""}`,
    editar: () => "Editó los campos de la tarea",
  };

/** Iniciales para el avatar a partir de un nombre tipo "M. Díaz" → "MD". */
function iniciales(nombre: string): string {
  const letras = nombre
    .split(/\s+/)
    .map((parte) => parte.replace(/[^\p{L}]/gu, "").charAt(0))
    .filter(Boolean);
  return (letras[0] ?? "").concat(letras[letras.length - 1] ?? "").toUpperCase() || "?";
}

/** Formatea un ISO a dd/mm/yyyy de forma determinista (UTC), sin depender del locale. */
export function formatearFecha(iso: string): string {
  const fecha = new Date(iso);
  const dia = String(fecha.getUTCDate()).padStart(2, "0");
  const mes = String(fecha.getUTCMonth() + 1).padStart(2, "0");
  return `${dia}/${mes}/${fecha.getUTCFullYear()}`;
}

/** Convierte el historial de la tarea en entradas para `AuditLog`. */
export function historialAAuditEntries(historial: EventoHistorialTarea[]): AuditEntry[] {
  return historial.map((evento) => ({
    id: evento.id,
    actor: evento.porNombre,
    initials: iniciales(evento.porNombre),
    verb: VERBO_POR_ACCION[evento.accion],
    verbLabel: ETIQUETA_POR_ACCION[evento.accion](evento),
    detail: evento.porRol,
    when: formatearFecha(evento.fecha),
  }));
}
