// ============================================================
// Validación del form de pedido — LÓGICA PURA (sin React).
// Las reglas mapean a las BR del módulo (docs/business-rules/designaciones.md).
// Devuelve un mapa campo → mensaje; vacío ⇒ el pedido es válido.
// ============================================================
import type { Adjunto, DatosEditablesPedido, PedidoDesignacion, TipoAdjunto } from "./types";

export type CampoPedido =
  | "docente"
  | "materiaAsociada"
  | "cargoSolicitado"
  | "dedicacionSolicitada"
  | "justificacion"
  | "adjuntos";

export type ErroresValidacion = Partial<Record<CampoPedido, string>>;

export interface ContextoValidacion {
  /** Pedidos ya cargados en el período (para BR-001). */
  pedidosExistentes: PedidoDesignacion[];
  /** Id del pedido en edición, para no compararlo consigo mismo. */
  pedidoActualId?: string;
}

const ETIQUETA_ADJUNTO: Record<TipoAdjunto, string> = {
  cv: "CV",
  dni_frente: "DNI (frente)",
  dni_dorso: "DNI (dorso)",
  justificativo: "justificativo",
};

function tieneAdjunto(adjuntos: Adjunto[], tipo: TipoAdjunto): boolean {
  return adjuntos.some((adjunto) => adjunto.tipo === tipo);
}

/** Valida los datos del form y devuelve los errores por campo (vacío ⇒ válido). */
export function validarPedido(
  datos: DatosEditablesPedido,
  contexto: ContextoValidacion,
): ErroresValidacion {
  const errores: ErroresValidacion = {};

  // Campos comunes obligatorios.
  if (!datos.docente.dni.trim()) {
    errores.docente = "El DNI del docente es obligatorio.";
  } else if (!datos.docente.nombre.trim()) {
    errores.docente = "El nombre del docente es obligatorio.";
  }
  if (!datos.materiaAsociada.trim()) {
    errores.materiaAsociada = "La materia asociada es obligatoria.";
  }

  // BR-001: un pedido por docente por período.
  const duplicado = contexto.pedidosExistentes.some(
    (pedido) =>
      pedido.id !== contexto.pedidoActualId &&
      pedido.docente.dni.trim() === datos.docente.dni.trim() &&
      datos.docente.dni.trim() !== "",
  );
  if (duplicado) {
    errores.docente = "Ya existe un pedido para este docente en el período.";
  }

  // Reglas por novedad.
  if (datos.novedad === "Alta" || datos.novedad === "Cambio de cargo o dedicación") {
    if (!datos.cargoSolicitado) {
      errores.cargoSolicitado = "Seleccioná el cargo solicitado.";
    }
    if (!datos.dedicacionSolicitada) {
      errores.dedicacionSolicitada = "Seleccioná la dedicación solicitada.";
    }
  }

  if (datos.novedad === "Alta") {
    // BR-002: Alta exige CV + DNI frente + DNI dorso.
    const requeridos: TipoAdjunto[] = ["cv", "dni_frente", "dni_dorso"];
    const faltantes = requeridos.filter((tipo) => !tieneAdjunto(datos.adjuntos, tipo));
    if (faltantes.length > 0) {
      errores.adjuntos = `Falta adjuntar: ${faltantes.map((t) => ETIQUETA_ADJUNTO[t]).join(", ")}.`;
    }
  }

  if (datos.novedad === "Baja") {
    // BR-003: Baja exige justificativo.
    if (!tieneAdjunto(datos.adjuntos, "justificativo")) {
      errores.adjuntos = "La baja exige adjuntar un justificativo.";
    }
  }

  if (datos.novedad === "Cambio de cargo o dedicación") {
    // BR-004: Cambio exige justificación.
    if (!datos.justificacion?.trim()) {
      errores.justificacion = "La justificación es obligatoria para un cambio.";
    }
  }

  return errores;
}

/** True si no hay errores de validación. */
export function esPedidoValido(errores: ErroresValidacion): boolean {
  return Object.keys(errores).length === 0;
}
