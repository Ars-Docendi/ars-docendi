// ============================================================
// Adapters de PRESENTACIÓN para el detalle del pedido (SCRUM-8).
// Traducen el dominio (en español) a los tipos de @ars-docendi/ui
// (en inglés): `AuditVerb`. Y derivan la cadena de aprobación
// (5 etapas) para el stepper horizontal `CadenaRevision`. Son
// funciones puras de vista —NO lógica de dominio—, por eso viven
// junto a los componentes de detalle y no en `maquinaEstados.ts`.
// ============================================================
import type { AuditEntry, AuditVerb } from "@ars-docendi/ui";
import type {
  AccionHistorial,
  ActorContexto,
  AsignacionMateria,
  EstadoPedido,
  EventoHistorial,
  PedidoDesignacion,
  Rol,
} from "../types";

/** Resumen legible de las materias de un pedido: una sola → su nombre; varias → "Primera +N". */
export function resumenMaterias(asignaciones: AsignacionMateria[]): string {
  if (asignaciones.length === 0) return "—";
  const [primera, ...resto] = asignaciones;
  return resto.length > 0 ? `${primera.materia || "—"} +${resto.length}` : primera.materia || "—";
}

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
export function iniciales(nombre: string): string {
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

// ============================================================
// Cadena de aprobación (5 etapas) para el stepper `CadenaRevision`.
// Jefe de Cátedra → Coordinador → Secretaría → Decanato → En lote.
// ============================================================

export type EstadoEtapaCadena = "cumplida" | "actual" | "pendiente" | "devuelta" | "rechazada";

export interface EtapaCadena {
  /** Etiqueta del rol/etapa (en español). */
  rol: string;
  estado: EstadoEtapaCadena;
  /** Línea de detalle bajo el rol (estado legible + fecha si aplica). */
  detalle: string;
  /** El actor actual ocupa esta etapa (resalta "· vos"). */
  esVos: boolean;
}

interface DefEtapa {
  rol: string;
  /** Estado del pedido al que corresponde la etapa (las de revisión + en_lote). */
  estado?: EstadoPedido;
  /** Rol del actor que actúa en la etapa (para detectar "vos"). */
  rolActor?: Rol;
}

const CADENA: readonly DefEtapa[] = [
  { rol: "Jefe de Cátedra", rolActor: "Jefe de Cátedra" },
  { rol: "Coordinador", estado: "en_revision_coordinador", rolActor: "Coordinador" },
  { rol: "Secretaría", estado: "en_revision_secretaria", rolActor: "Secretaría" },
  { rol: "Decanato", estado: "en_revision_decanato", rolActor: "Decanato" },
  { rol: "En lote", estado: "en_lote" },
];

function indiceDeEstado(estado: EstadoPedido | undefined): number {
  return CADENA.findIndex((etapa) => etapa.estado === estado);
}

function indiceDeRolRevisor(rol: Rol): number {
  const indice = CADENA.findIndex((etapa) => etapa.rolActor === rol && etapa.estado);
  return indice < 0 ? 1 : indice;
}

/** ¿El estado es una etapa de revisión (Coordinador / Secretaría / Decanato)? */
function esEstadoRevision(estado: EstadoPedido): boolean {
  return (
    estado === "en_revision_coordinador" ||
    estado === "en_revision_secretaria" ||
    estado === "en_revision_decanato"
  );
}

/**
 * Posición de la etapa de revisión actual en la cadena ("2 de 4"), 1-based sobre
 * las 4 etapas posteriores al Jefe de Cátedra (Coordinador → Secretaría →
 * Decanato → En lote). Devuelve `null` fuera de las etapas de revisión
 * (borrador / devuelto / terminales), donde una posición sería ambigua.
 */
export function posicionEtapa(estado: EstadoPedido): { n: number; total: number } | null {
  if (!esEstadoRevision(estado)) return null;
  return { n: indiceDeEstado(estado) + 1, total: CADENA.length - 1 };
}

/** Texto de detalle bajo cada etapa, según su estado derivado. */
function detalleEtapa(
  pedido: PedidoDesignacion,
  etapa: DefEtapa,
  estado: EstadoEtapaCadena,
  esVos: boolean,
): string {
  switch (estado) {
    case "actual":
      if (etapa.rol === "Jefe de Cátedra" && pedido.estado === "borrador") {
        return "En borrador";
      }
      return esVos ? "En revisión · vos" : "En revisión";
    case "pendiente":
      return "Pendiente";
    case "devuelta":
      return "Devuelto para corrección";
    case "rechazada": {
      const evento = [...pedido.historial].reverse().find((h) => h.accion === "rechazar");
      return evento ? `Rechazó · ${formatearFecha(evento.fecha)}` : "Rechazó";
    }
    case "cumplida": {
      if (etapa.rol === "Jefe de Cátedra") {
        const evento = [...pedido.historial]
          .reverse()
          .find((h) => h.accion === "reenviar" || h.accion === "enviar");
        if (!evento) return "Originó el pedido";
        const verbo = evento.accion === "reenviar" ? "Reenvió" : "Envió";
        return `${verbo} · ${formatearFecha(evento.fecha)}`;
      }
      if (etapa.rol === "En lote") return "En lote";
      const evento = etapa.rolActor
        ? [...pedido.historial]
            .reverse()
            .find((h) => h.accion === "aceptar" && h.porRol === etapa.rolActor)
        : undefined;
      return evento ? `Aprobó · ${formatearFecha(evento.fecha)}` : "Aprobado";
    }
  }
}

/**
 * Deriva las 5 etapas de la cadena (Jefe de Cátedra → … → En lote) a partir del
 * estado y el historial del pedido. Cada etapa queda cumplida / actual /
 * pendiente / devuelta / rechazada; `esVos` marca la etapa que ocupa el actor.
 */
export function derivarCadena(pedido: PedidoDesignacion, actor: ActorContexto): EtapaCadena[] {
  let activo: number;
  let naturaleza: EstadoEtapaCadena = "actual";

  if (pedido.estado === "borrador") {
    activo = 0;
  } else if (pedido.estado === "rechazado") {
    // La etapa rechazada se deriva de la última etapa de revisión registrada en
    // el historial (robusto aunque rechace Administración, que puede actuar en
    // cualquier etapa); si el historial es mínimo, cae al rol del rechazo.
    const etapaRevision = [...pedido.historial].reverse().find((h) => esEstadoRevision(h.etapa));
    const rechazo = [...pedido.historial].reverse().find((h) => h.accion === "rechazar");
    activo = etapaRevision
      ? indiceDeEstado(etapaRevision.etapa)
      : rechazo
        ? indiceDeRolRevisor(rechazo.porRol)
        : 1;
    naturaleza = "rechazada";
  } else if (pedido.estado === "devuelto") {
    activo = pedido.etapaRetorno ? indiceDeEstado(pedido.etapaRetorno) : 1;
    naturaleza = "devuelta";
  } else if (pedido.estado === "en_lote") {
    activo = CADENA.length; // toda la cadena queda done
  } else {
    activo = indiceDeEstado(pedido.estado); // etapas de revisión
  }
  if (activo < 0) activo = 0;

  return CADENA.map((etapa, indice) => {
    const estado: EstadoEtapaCadena =
      indice < activo ? "cumplida" : indice === activo ? naturaleza : "pendiente";
    const esVos =
      etapa.rolActor !== undefined && actor.rol === etapa.rolActor && estado === "actual";
    return { rol: etapa.rol, estado, detalle: detalleEtapa(pedido, etapa, estado, esVos), esVos };
  });
}
