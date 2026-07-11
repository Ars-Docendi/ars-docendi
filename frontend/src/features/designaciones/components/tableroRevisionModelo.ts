// ============================================================
// Modelo de presentación de la vista Tabla de revisión (opción D).
// Agrupa por ESTADO DE AVANCE del pedido, no por rol: cuatro grupos
// fijos e iguales para todos — En revisión (toda la cadena) · Aceptados ·
// Devueltos · Rechazados. El "turno" del actor se marca por fila
// (`esTuTurno`), NO cambia qué grupo contiene al pedido. Cada fila en
// revisión declara su etapa + avance `x/4` (`avancePedido`). Lógica de
// vista pura (no es dominio): no decide autoridad, solo presenta.
// ============================================================
import type { ActorContexto, EstadoPedido, Novedad, PedidoDesignacion } from "../types";
import { puedeRevisar } from "../api/maquinaEstados";

export type TonoColumna = "acento" | "neutro" | "exito" | "alerta" | "peligro";

export interface ColumnaTablero {
  id: string;
  titulo: string;
  subtitulo: string;
  tono: TonoColumna;
  pedidos: PedidoDesignacion[];
}

/** Pasos de la cadena de aprobación (el Jefe de Cátedra es el origen, no cuenta). */
export const TOTAL_PASOS = 4;

export interface AvanceEtapa {
  /** Etiqueta legible de la etapa actual, p. ej. "En Coordinación". */
  etiqueta: string;
  /** Paso actual dentro de la cadena (1..4). */
  paso: number;
  /** Total de pasos de la cadena (4). */
  total: number;
}

/** Paso `x/4` de cada estado con avance (revisión + cierre). */
const PASO_DE_ETAPA: Partial<Record<EstadoPedido, number>> = {
  en_revision_coordinador: 1,
  en_revision_secretaria: 2,
  en_revision_decanato: 3,
  en_lote: 4,
};

/** Etiqueta de la etapa para la card (con prefijo "En "). */
const ETIQUETA_ETAPA: Partial<Record<EstadoPedido, string>> = {
  en_revision_coordinador: "En Coordinación",
  en_revision_secretaria: "En Secretaría",
  en_revision_decanato: "En Decanato",
  en_lote: "En lote",
};

const ETAPAS_REVISION: EstadoPedido[] = [
  "en_revision_coordinador",
  "en_revision_secretaria",
  "en_revision_decanato",
];

function esEtapaDeRevision(estado: EstadoPedido): boolean {
  return ETAPAS_REVISION.includes(estado);
}

/** ¿Es el turno del actor sobre este pedido? (revisor de la etapa en su ámbito, o Administración). */
export function esTuTurno(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  return puedeRevisar(pedido, actor);
}

/** Avance del pedido en la cadena (etapa + paso `x/4`); `null` para estados sin avance. */
export function avancePedido(pedido: PedidoDesignacion): AvanceEtapa | null {
  const paso = PASO_DE_ETAPA[pedido.estado];
  const etiqueta = ETIQUETA_ETAPA[pedido.estado];
  if (paso === undefined || etiqueta === undefined) return null;
  return { etiqueta, paso, total: TOTAL_PASOS };
}

/**
 * Grupos de la Tabla de revisión (opción D), iguales para todo actor. "En revisión"
 * reúne toda la cadena (cada fila declara su etapa + `x/4`); los pedidos del
 * actor (su turno) se ordenan primero. Aceptados / Devueltos / Rechazados son
 * terminales. El gating por ámbito ya se aplicó al traer `pedidos`.
 */
export function construirColumnas(
  pedidos: PedidoDesignacion[],
  actor: ActorContexto,
): ColumnaTablero[] {
  const enRevision = pedidos
    .filter((pedido) => esEtapaDeRevision(pedido.estado))
    .sort((a, b) => {
      const turno = Number(esTuTurno(b, actor)) - Number(esTuTurno(a, actor));
      if (turno !== 0) return turno;
      return (PASO_DE_ETAPA[a.estado] ?? 0) - (PASO_DE_ETAPA[b.estado] ?? 0);
    });

  return [
    {
      id: "en-revision",
      titulo: "En revisión",
      subtitulo: "Cadena de aprobación",
      tono: "acento",
      pedidos: enRevision,
    },
    {
      id: "aceptados",
      titulo: "Aceptados",
      subtitulo: "En lote · 4/4",
      tono: "exito",
      pedidos: pedidos.filter((pedido) => pedido.estado === "en_lote"),
    },
    {
      id: "devueltos",
      titulo: "Devueltos",
      subtitulo: "Para corrección",
      tono: "alerta",
      pedidos: pedidos.filter((pedido) => pedido.estado === "devuelto"),
    },
    {
      id: "rechazados",
      titulo: "Rechazados",
      subtitulo: "Terminados · período",
      tono: "peligro",
      pedidos: pedidos.filter((pedido) => pedido.estado === "rechazado"),
    },
  ];
}

/** Iniciales del docente (p. ej. "Ana Pérez" → "AP"). */
export function inicialesDocente(nombre: string): string {
  const partes = nombre
    .split(/\s+/)
    .map((parte) => parte.replace(/[^\p{L}]/gu, "").charAt(0))
    .filter(Boolean);
  return (partes[0] ?? "").concat(partes[partes.length - 1] ?? "").toUpperCase() || "?";
}

/** Etiqueta corta de la novedad para el chip. */
export function etiquetaNovedad(novedad: Novedad): string {
  return novedad === "Cambio de cargo o dedicación" ? "Cambio" : novedad;
}

/** Comentario del último evento del tipo dado (devolución / rechazo). */
function comentarioDe(
  pedido: PedidoDesignacion,
  accion: "devolver" | "rechazar",
): string | undefined {
  return [...pedido.historial].reverse().find((evento) => evento.accion === accion)?.comentario;
}

/** Motivo crudo del último rechazo (sin el prefijo "Rechazado:"), para destacarlo como cita en el detalle. */
export function motivoRechazo(pedido: PedidoDesignacion): string | undefined {
  return comentarioDe(pedido, "rechazar");
}
