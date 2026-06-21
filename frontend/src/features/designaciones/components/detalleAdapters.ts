// ============================================================
// Adapters de PRESENTACIÓN para el detalle del pedido (SCRUM-8).
// Traducen el dominio (en español) a los tipos de @ars-docendi/ui
// (en inglés): `AuditVerb` y `TimelineStep`. Son funciones puras de
// vista —NO lógica de dominio—, por eso viven junto a los componentes
// de detalle y no en `maquinaEstados.ts` (no rompen la regla del seam).
// ============================================================
import type { AuditEntry, AuditVerb, TimelineStatus, TimelineStep } from "@ars-docendi/ui";
import type {
  AccionHistorial,
  EstadoPedido,
  EventoHistorial,
  PedidoDesignacion,
  Rol,
} from "../types";

/** Mapa español → `AuditVerb` (símbolo de la lib, en inglés). Exhaustivo por tipo. */
const VERBO_POR_ACCION: Record<AccionHistorial, AuditVerb> = {
  crear: "create",
  enviar: "update",
  editar: "update",
  aceptar: "approve",
  rechazar: "reject",
  devolver: "return",
  reenviar: "update",
  cancelar: "reject",
  priorizar: "update",
};

/** Etiqueta legible (en español) que se muestra junto al verbo. */
const ETIQUETA_POR_ACCION: Record<AccionHistorial, string> = {
  crear: "Creó el pedido",
  enviar: "Envió a revisión",
  editar: "Editó el pedido",
  aceptar: "Aceptó",
  rechazar: "Rechazó",
  devolver: "Devolvió",
  reenviar: "Reenvió",
  cancelar: "Canceló",
  priorizar: "Marcó prioritario",
};

/** Traduce una acción del historial a su `AuditVerb` de la lib. */
export function accionAAuditVerb(accion: AccionHistorial): AuditVerb {
  return VERBO_POR_ACCION[accion];
}

/** Iniciales para el avatar a partir de un nombre tipo "M. Díaz" → "MD". */
function iniciales(nombre: string): string {
  const letras = nombre
    .split(/\s+/)
    .map((parte) => parte.replace(/[^\p{L}]/gu, "").charAt(0))
    .filter(Boolean);
  return (letras[0] ?? "").concat(letras[letras.length - 1] ?? "").toUpperCase() || "?";
}

/** Formatea un ISO a dd/mm/yyyy de forma determinista (UTC), sin depender del locale. */
function formatearFecha(iso: string): string {
  const fecha = new Date(iso);
  const dia = String(fecha.getUTCDate()).padStart(2, "0");
  const mes = String(fecha.getUTCMonth() + 1).padStart(2, "0");
  return `${dia}/${mes}/${fecha.getUTCFullYear()}`;
}

/** Convierte el historial del pedido en entradas para `AuditLog`. */
export function historialAAuditEntries(historial: EventoHistorial[]): AuditEntry[] {
  return historial.map((evento) => ({
    id: evento.id,
    actor: evento.porNombre,
    initials: iniciales(evento.porNombre),
    verb: accionAAuditVerb(evento.accion),
    verbLabel: ETIQUETA_POR_ACCION[evento.accion],
    detail: evento.porRol,
    when: formatearFecha(evento.fecha),
    comment: evento.comentario,
  }));
}

// --- Cadena de aprobación para `ApprovalTimeline` ---

interface Etapa {
  estado: Extract<
    EstadoPedido,
    "en_revision_coordinador" | "en_revision_secretaria" | "en_revision_decanato"
  >;
  rol: Rol;
  nombre: string;
}

const ETAPAS: readonly Etapa[] = [
  { estado: "en_revision_coordinador", rol: "Coordinador", nombre: "Coordinador de Carrera" },
  { estado: "en_revision_secretaria", rol: "Secretaría", nombre: "Secretaría Académica" },
  { estado: "en_revision_decanato", rol: "Decanato", nombre: "Decanato" },
];

/** Nombre del revisor que actuó en una etapa (de existir en el historial). */
function actorDeEtapa(pedido: PedidoDesignacion, rol: Rol): string | undefined {
  const evento = pedido.historial.find(
    (h) =>
      h.porRol === rol &&
      (h.accion === "aceptar" || h.accion === "rechazar" || h.accion === "devolver"),
  );
  return evento?.porNombre;
}

function ultimoComentario(pedido: PedidoDesignacion, accion: AccionHistorial): string | undefined {
  return [...pedido.historial].reverse().find((h) => h.accion === accion)?.comentario;
}

/**
 * Deriva la cadena de aprobación (Coordinador → Secretaría → Decanato) a
 * `TimelineStep[]`, marcando cada etapa como done/current/pending/returned/rejected
 * según el estado y el historial del pedido.
 */
export function derivarTimeline(pedido: PedidoDesignacion): TimelineStep[] {
  const construir = (indiceCorte: number, estadoCorte: TimelineStatus, comentario?: string) =>
    ETAPAS.map((etapa, i) => {
      const status: TimelineStatus =
        i < indiceCorte ? "done" : i === indiceCorte ? estadoCorte : "pending";
      return {
        role: etapa.nombre,
        name: actorDeEtapa(pedido, etapa.rol) ?? "Pendiente",
        status,
        comment: i === indiceCorte ? comentario : undefined,
      };
    });

  if (pedido.estado === "rechazado") {
    const evento = [...pedido.historial].reverse().find((h) => h.accion === "rechazar");
    const indice = evento ? ETAPAS.findIndex((e) => e.rol === evento.porRol) : 0;
    return construir(Math.max(indice, 0), "rejected", evento?.comentario);
  }

  if (pedido.estado === "devuelto") {
    const indice = pedido.etapaRetorno
      ? ETAPAS.findIndex((e) => e.estado === pedido.etapaRetorno)
      : 0;
    return construir(Math.max(indice, 0), "returned", ultimoComentario(pedido, "devolver"));
  }

  if (pedido.estado === "en_lote") {
    return ETAPAS.map((etapa) => ({
      role: etapa.nombre,
      name: actorDeEtapa(pedido, etapa.rol) ?? "Aprobado",
      status: "done" as const,
    }));
  }

  const indiceActual = ETAPAS.findIndex((e) => e.estado === pedido.estado);
  // borrador / cancelado: aún no entró a la cadena (todas pendientes).
  if (indiceActual === -1) {
    return ETAPAS.map((etapa) => ({ role: etapa.nombre, name: "Pendiente", status: "pending" }));
  }
  return construir(indiceActual, "current");
}
