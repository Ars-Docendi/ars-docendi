// ============================================================
// Modelo de presentación del tablero de revisión (SCRUM-8, opción D).
// Agrupa por ESTADO DE AVANCE del pedido, no por rol: cuatro columnas
// fijas e iguales para todos — En revisión (toda la cadena) · Aceptados ·
// Devueltos · Rechazados. El "turno" del actor se marca por card
// (`esTuTurno`), NO cambia qué columna contiene al pedido. Cada card en
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
 * Columnas del tablero (opción D), iguales para todo actor. "En revisión"
 * reúne toda la cadena (las cards declaran su etapa + `x/4`); los pedidos del
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

/** Línea de detalle de la card (transición de cargo/dedicación, adjunto o motivo). */
export function detallePedido(pedido: PedidoDesignacion): string {
  if (pedido.estado === "devuelto") {
    const motivo = comentarioDe(pedido, "devolver");
    return motivo ? `Devuelto: ${motivo}` : "Devuelto";
  }
  if (pedido.estado === "rechazado") {
    const motivo = motivoRechazo(pedido);
    return motivo ? `Rechazado: ${motivo}` : "Rechazado";
  }
  switch (pedido.novedad) {
    case "Alta":
      return (
        [pedido.cargoSolicitado, pedido.dedicacionSolicitada].filter(Boolean).join(" · ") ||
        "Alta docente"
      );
    case "Baja":
      return pedido.justificacion
        ? `${pedido.cargoActual ?? "Docente"} · ${pedido.justificacion}`
        : `${pedido.cargoActual ?? "Baja"} · baja`;
    case "Cambio de cargo o dedicación":
      if (pedido.cargoSolicitado && pedido.cargoSolicitado !== pedido.cargoActual) {
        return `Cargo: ${pedido.cargoActual ?? "—"} → ${pedido.cargoSolicitado}`;
      }
      if (pedido.dedicacionSolicitada && pedido.dedicacionSolicitada !== pedido.dedicacionActual) {
        return `Dedicación: ${pedido.dedicacionActual ?? "—"} → ${pedido.dedicacionSolicitada}`;
      }
      return "Cambio de cargo o dedicación";
    case "Sin novedad":
      return (
        [pedido.cargoActual, pedido.dedicacionActual].filter(Boolean).join(" · ") || "Sin novedad"
      );
  }
}

/** Motivo crudo del último rechazo (sin el prefijo "Rechazado:"), para destacarlo como cita en la card. */
export function motivoRechazo(pedido: PedidoDesignacion): string | undefined {
  return comentarioDe(pedido, "rechazar");
}

function ultimaFecha(pedido: PedidoDesignacion): string | undefined {
  return pedido.historial.at(-1)?.fecha;
}

/** Tiempo relativo legible ("hace 3 d"). Usa la fecha actual (solo display). */
function tiempoAtras(iso: string | undefined): string {
  if (!iso) return "—";
  const ms = Date.now() - new Date(iso).getTime();
  const minutos = Math.max(0, Math.floor(ms / 60000));
  if (minutos < 60) return `hace ${minutos} min`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `hace ${horas} h`;
  const dias = Math.floor(horas / 24);
  if (dias < 60) return `hace ${dias} d`;
  const meses = Math.floor(dias / 30);
  return `hace ${meses} ${meses === 1 ? "mes" : "meses"}`;
}

/** Recencia del pedido (pie de la card): "hace X" o "Reenviado · hace X". */
export function situacionPedido(pedido: PedidoDesignacion): string {
  const ultimaAccion = pedido.historial.at(-1)?.accion;
  const prefijo = ultimaAccion === "reenviar" ? "Reenviado · " : "";
  return prefijo + tiempoAtras(ultimaFecha(pedido));
}
