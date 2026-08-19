// ============================================================
// Modelo de presentación de la vista Tabla de revisión: secciones por
// ETAPA DEL CIRCUITO — En Coordinación / En Secretaría / En Decanato /
// Finalizados — en vez de por estado de avance puro. Un pedido Devuelto no
// tiene sección propia: vive en la sección de la etapa a la que volvió
// (`etapaRetorno`) — es ahí donde queda trabado hasta que se corrija y
// reenvíe —, con su fila anotada "Devuelto por {revisor}" en vez del
// stepper (`quienDevolvio`). Este agrupamiento le permite a Secretaría
// Académica, Administrativo y Decanato (que ven TODO el departamento, a
// diferencia del Coordinador que ve solo su carrera) triangular grandes
// volúmenes de pedidos por dónde están trabados en la cadena. Lógica de
// vista pura (no es dominio): no decide autoridad, solo presenta — el
// gating por ámbito [BR-009] ya se aplicó a los `pedidos` que llegan acá.
// ============================================================
import type { ActorContexto, EstadoPedido, Novedad, PedidoDesignacion, Rol } from "../types";
import { formatearFecha } from "./detalleAdapters";

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

type EtapaRevision = "en_revision_coordinador" | "en_revision_secretaria" | "en_revision_decanato";

const ETAPAS_REVISION: EtapaRevision[] = [
  "en_revision_coordinador",
  "en_revision_secretaria",
  "en_revision_decanato",
];

/** Sección de la Tabla por cada etapa del circuito (id/título/subtítulo fijos). */
const SECCION_DE_ETAPA: Record<EtapaRevision, { id: string; titulo: string; subtitulo: string }> = {
  en_revision_coordinador: {
    id: "en-coordinacion",
    titulo: "En Coordinación",
    subtitulo: "Revisión de carrera",
  },
  en_revision_secretaria: {
    id: "en-secretaria",
    titulo: "En Secretaría",
    subtitulo: "Revisión departamental",
  },
  en_revision_decanato: {
    id: "en-decanato",
    titulo: "En Decanato",
    subtitulo: "Aprobación final",
  },
};

/** Rol revisor "dueño" de cada sección de etapa (para el default de expansión, ver `seccionInicialDelActor`). */
const ROL_DE_SECCION: Record<string, ActorContexto["rol"]> = {
  "en-coordinacion": "Coordinador",
  "en-secretaria": "Secretaría",
  "en-decanato": "Decanato",
};

/**
 * Id de la sección que debe arrancar expandida para este actor — la suya
 * (Coordinador → "en-coordinacion", etc.); las demás arrancan colapsadas. Sin
 * match (Administración, que ve todo por igual, o cualquier otro rol) →
 * `null`, y las 4 arrancan colapsadas.
 */
export function seccionInicialDelActor(actor: ActorContexto): string | null {
  return Object.entries(ROL_DE_SECCION).find(([, rol]) => rol === actor.rol)?.[0] ?? null;
}

/** ¿Es el turno del actor sobre este pedido? (revisor de la etapa en su ámbito, o Administración). */
export function esTuTurno(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  void actor;
  return (pedido.accionesPermitidas ?? []).some((accion) =>
    ["aceptar", "rechazar", "devolver"].includes(accion),
  );
}

/** Avance del pedido en la cadena (etapa + paso `x/4`); `null` para estados sin avance. */
export function avancePedido(pedido: PedidoDesignacion): AvanceEtapa | null {
  const paso = PASO_DE_ETAPA[pedido.estado];
  const etiqueta = ETIQUETA_ETAPA[pedido.estado];
  if (paso === undefined || etiqueta === undefined) return null;
  return { etiqueta, paso, total: TOTAL_PASOS };
}

/** ¿Este pedido vive en la sección de esta etapa? Activo en ella, o devuelto esperando corrección para volver. */
function perteneceASeccion(pedido: PedidoDesignacion, etapa: EtapaRevision): boolean {
  return pedido.estado === etapa || (pedido.estado === "devuelto" && pedido.etapaRetorno === etapa);
}

/** Fecha (epoch ms) del evento más reciente del historial; 0 si no hay historial. */
function fechaUltimaActualizacionMs(pedido: PedidoDesignacion): number {
  const ultimo = pedido.historial.at(-1);
  return ultimo ? new Date(ultimo.fecha).getTime() : 0;
}

/**
 * Orden dentro de una sección de etapa: prioritarios primero, después
 * devueltos, después el resto — dentro de cada grupo, por fecha de última
 * actualización ascendente (el que espera hace más tiempo, arriba).
 */
function compararEnSeccion(a: PedidoDesignacion, b: PedidoDesignacion): number {
  const rango = (p: PedidoDesignacion): number => {
    if (p.prioritario) return 0;
    if (p.estado === "devuelto") return 1;
    return 2;
  };
  const diff = rango(a) - rango(b);
  if (diff !== 0) return diff;
  return fechaUltimaActualizacionMs(a) - fechaUltimaActualizacionMs(b);
}

/**
 * Orden dentro de Finalizados: Aceptados antes que Rechazados; dentro de
 * cada bloque, por fecha de última actualización descendente (el cierre
 * más reciente arriba).
 */
function compararFinalizados(a: PedidoDesignacion, b: PedidoDesignacion): number {
  const rango = (p: PedidoDesignacion) => (p.estado === "en_lote" ? 0 : 1);
  const diff = rango(a) - rango(b);
  if (diff !== 0) return diff;
  return fechaUltimaActualizacionMs(b) - fechaUltimaActualizacionMs(a);
}

/**
 * Secciones de la Tabla de revisión, iguales para todo actor: una por etapa
 * del circuito (En Coordinación / En Secretaría / En Decanato) más
 * Finalizados (Aceptados + Rechazados). El gating por ámbito [BR-009] ya se
 * aplicó al traer `pedidos`; estas secciones no filtran por rol, solo
 * organizan lo que el actor ya puede ver.
 */
export function construirColumnas(pedidos: PedidoDesignacion[]): ColumnaTablero[] {
  const seccionesEtapa = ETAPAS_REVISION.map((etapa) => {
    const { id, titulo, subtitulo } = SECCION_DE_ETAPA[etapa];
    const filas = pedidos
      .filter((pedido) => perteneceASeccion(pedido, etapa))
      .sort(compararEnSeccion);
    return { id, titulo, subtitulo, tono: "acento" as TonoColumna, pedidos: filas };
  });

  const finalizados = pedidos
    .filter((pedido) => pedido.estado === "en_lote" || pedido.estado === "rechazado")
    .sort(compararFinalizados);

  return [
    ...seccionesEtapa,
    {
      id: "finalizados",
      titulo: "Finalizados",
      subtitulo: "Aceptados y rechazados",
      tono: "neutro",
      pedidos: finalizados,
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

/** Fecha del evento más reciente del historial (cualquier acción), formato dd/mm/aaaa. */
export function fechaUltimaActualizacion(pedido: PedidoDesignacion): string {
  const ultimo = pedido.historial.at(-1);
  return ultimo ? formatearFecha(ultimo.fecha) : "—";
}

/** Etiqueta corta de la novedad para el chip. */
export function etiquetaNovedad(novedad: Novedad): string {
  return novedad === "Cambio de cargo o dedicación" ? "Cambio" : novedad;
}

/** Último evento del historial del tipo dado (devolución / rechazo). */
function eventoDe(
  pedido: PedidoDesignacion,
  accion: "devolver" | "rechazar",
): PedidoDesignacion["historial"][number] | undefined {
  return [...pedido.historial].reverse().find((evento) => evento.accion === accion);
}

/** Motivo crudo del último rechazo (sin el prefijo "Rechazado:"), para destacarlo como cita en el detalle. */
export function motivoRechazo(pedido: PedidoDesignacion): string | undefined {
  return eventoDe(pedido, "rechazar")?.comentario;
}

/** Nombre de quien devolvió el pedido por última vez, para "Devuelto por {nombre}". */
export function quienDevolvio(pedido: PedidoDesignacion): string | undefined {
  return eventoDe(pedido, "devolver")?.porNombre;
}

/** Rol de quien devolvió el pedido por última vez, para "Devuelto por {nombre} ({rol})". */
export function rolDeQuienDevolvio(pedido: PedidoDesignacion): Rol | undefined {
  return eventoDe(pedido, "devolver")?.porRol;
}

/**
 * Avance (etapa + paso `x/4`) de un pedido Devuelto, calculado sobre `etapaRetorno` en vez de
 * `estado` — la misma etapa que decide en qué sección vive (`perteneceASeccion`). Mismo shape que
 * `avancePedido`, para que la celda Estado de un Devuelto use el mismo stepper + "En {etapa} ·
 * x/4" que ya usan los estados en revisión, en vez de perder esa referencia.
 */
export function avanceEtapaRetorno(pedido: PedidoDesignacion): AvanceEtapa | null {
  const etapa = pedido.etapaRetorno;
  const paso = etapa ? PASO_DE_ETAPA[etapa] : undefined;
  const etiqueta = etapa ? ETIQUETA_ETAPA[etapa] : undefined;
  if (paso === undefined || etiqueta === undefined) return null;
  return { etiqueta, paso, total: TOTAL_PASOS };
}
