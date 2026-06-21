// ============================================================
// Modelo de presentación del tablero de revisión (SCRUM-8).
// Construye las columnas RELATIVAS AL ROL del actor (pipeline:
// Pendientes → Etapa siguiente → Aceptados → Devueltos) y deriva
// los textos de cada card (detalle, situación, iniciales). Lógica
// de vista pura (no es dominio): no decide autoridad, solo presenta.
// ============================================================
import type { ActorContexto, EstadoPedido, Novedad, PedidoDesignacion, Rol } from "../types";

export type TonoColumna = "acento" | "neutro" | "exito" | "alerta" | "peligro";

export interface ColumnaTablero {
  id: string;
  titulo: string;
  subtitulo: string;
  tono: TonoColumna;
  pedidos: PedidoDesignacion[];
}

const CADENA: EstadoPedido[] = [
  "en_revision_coordinador",
  "en_revision_secretaria",
  "en_revision_decanato",
];

/** Etapa de revisión que "posee" cada rol revisor. */
const ETAPA_DE_ROL: Partial<Record<Rol, EstadoPedido>> = {
  Coordinador: "en_revision_coordinador",
  Secretaría: "en_revision_secretaria",
  Decanato: "en_revision_decanato",
};

/** Nombre corto de cada etapa de revisión (para títulos y situación). */
const NOMBRE_ETAPA: Record<string, string> = {
  en_revision_coordinador: "Coordinación",
  en_revision_secretaria: "Secretaría",
  en_revision_decanato: "Decanato",
};

/** A quién vuelve un pedido devuelto desde cada etapa (subtítulo de "Devueltos"). */
const DESTINO_DEVOLUCION: Partial<Record<EstadoPedido, string>> = {
  en_revision_coordinador: "Al Jefe de Cátedra",
  en_revision_secretaria: "Al Coordinador",
  en_revision_decanato: "A la Secretaría",
};

function esEtapaDeRevision(estado: EstadoPedido): boolean {
  return CADENA.includes(estado);
}

/**
 * Columnas del tablero relativas al actor. Coordinador/Secretaría tienen las 4
 * (Pendientes, Etapa siguiente, Aceptados, Devueltos); Decanato/Administración
 * omiten "Etapa siguiente". Con `incluirRechazados`, agrega la columna Rechazados.
 */
export function construirColumnas(
  pedidos: PedidoDesignacion[],
  actor: ActorContexto,
  incluirRechazados = false,
): ColumnaTablero[] {
  const miEtapa = ETAPA_DE_ROL[actor.rol];
  const miIndice = miEtapa ? CADENA.indexOf(miEtapa) : -1;

  const enMiEtapa = (p: PedidoDesignacion) =>
    miEtapa ? p.estado === miEtapa : esEtapaDeRevision(p.estado);

  const adelante = (p: PedidoDesignacion) =>
    esEtapaDeRevision(p.estado) && CADENA.indexOf(p.estado) > miIndice && miIndice >= 0;

  const columnas: ColumnaTablero[] = [];

  columnas.push({
    id: "pendientes",
    titulo: "Pendientes",
    subtitulo: "En tu etapa",
    tono: "acento",
    pedidos: pedidos.filter(enMiEtapa),
  });

  const etapaSiguiente = miIndice >= 0 ? CADENA[miIndice + 1] : undefined;
  if (etapaSiguiente) {
    columnas.push({
      id: "siguiente",
      titulo: `En ${NOMBRE_ETAPA[etapaSiguiente]}`,
      subtitulo: "Etapa siguiente",
      tono: "neutro",
      pedidos: pedidos.filter(adelante),
    });
  }

  columnas.push({
    id: "aceptados",
    titulo: "Aceptados",
    subtitulo: "En lote",
    tono: "exito",
    pedidos: pedidos.filter((p) => p.estado === "en_lote"),
  });

  columnas.push({
    id: "devueltos",
    titulo: "Devueltos",
    subtitulo: (miEtapa && DESTINO_DEVOLUCION[miEtapa]) || "Al solicitante",
    tono: "alerta",
    pedidos: pedidos.filter((p) => p.estado === "devuelto"),
  });

  if (incluirRechazados) {
    columnas.push({
      id: "rechazados",
      titulo: "Rechazados",
      subtitulo: "Terminados",
      tono: "peligro",
      pedidos: pedidos.filter((p) => p.estado === "rechazado"),
    });
  }

  return columnas;
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

function comentarioDevolucion(pedido: PedidoDesignacion): string | undefined {
  return [...pedido.historial].reverse().find((h) => h.accion === "devolver")?.comentario;
}

/** Línea de detalle de la card (transición de cargo/dedicación, adjunto o motivo). */
export function detallePedido(pedido: PedidoDesignacion): string {
  if (pedido.estado === "devuelto") {
    const motivo = comentarioDevolucion(pedido);
    return motivo ? `Devuelto: ${motivo}` : "Devuelto";
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

/** Texto de situación (pie de la card), según el estado relativo al actor. */
export function situacionPedido(pedido: PedidoDesignacion, actor: ActorContexto): string {
  if (pedido.estado === "en_lote") return "En lote";
  if (pedido.estado === "rechazado") return "Rechazado";
  if (pedido.estado === "devuelto") return tiempoAtras(ultimaFecha(pedido));

  const miEtapa = ETAPA_DE_ROL[actor.rol];
  if (esEtapaDeRevision(pedido.estado) && pedido.estado !== miEtapa) {
    return `En ${NOMBRE_ETAPA[pedido.estado]}`;
  }

  const ultimaAccion = pedido.historial.at(-1)?.accion;
  const prefijo = ultimaAccion === "reenviar" ? "Reenviado · " : "";
  return prefijo + tiempoAtras(ultimaFecha(pedido));
}
