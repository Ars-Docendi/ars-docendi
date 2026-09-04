// ============================================================
// Validación del form de pedido — LÓGICA PURA (sin React).
// Las reglas mapean a las BR del módulo (docs/business-rules/designaciones.md).
// Devuelve un mapa campo → mensaje; vacío ⇒ el pedido es válido.
// ============================================================
import type { Adjunto, DatosEditablesPedido, PedidoDesignacion, TipoAdjunto } from "./types";
import { indiceDedicacion } from "./api/catalogos";

export type CampoPedido =
  | "docente"
  | "horas"
  | "cargoSolicitado"
  | "dedicacionSolicitada"
  | "tipoBaja"
  | "tipoBajaDetalle"
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
  } else if (
    // BR-018: Baja/Cambio operan sobre un docente ya existente en el sistema,
    // que por eso ya tiene legajo asignado — a diferencia de Alta (docente nuevo).
    (datos.novedad === "Baja" || datos.novedad === "Cambio de cargo o dedicación") &&
    !datos.docente.legajo?.trim()
  ) {
    errores.docente = "El legajo del docente es obligatorio para una baja o un cambio.";
  }
  // Carga horaria de la cátedra. Las horas son un campo libre: no se valida que
  // cierren contra la dedicación solicitada (D2), sólo que sean positivas cuando el
  // pedido efectivamente pide una designación.
  if (
    (datos.novedad === "Alta" || datos.novedad === "Cambio de cargo o dedicación") &&
    datos.horas <= 0
  ) {
    errores.horas = "Ingresá la carga horaria de la materia (mayor a 0).";
  }

  // BR-001: un pedido por docente por período, SIN IMPORTAR LA CÁTEDRA.
  //
  // El mensaje no nombra la cátedra ni el autor del pedido bloqueante: puede
  // pertenecer a una cátedra ajena al actor, que no tiene por qué verla. Acá el
  // chequeo alcanza sólo a los pedidos que el actor ya tiene a la vista; la
  // autoridad real es el índice único parcial de PostgreSQL, y el backend traduce
  // su violación a este mismo mensaje.
  const duplicado = contexto.pedidosExistentes.some(
    (pedido) =>
      pedido.id !== contexto.pedidoActualId &&
      pedido.docente.dni.trim() === datos.docente.dni.trim() &&
      datos.docente.dni.trim() !== "" &&
      // Un pedido rechazado o cancelado libera el cupo: se puede volver a presentar.
      pedido.estado !== "rechazado" &&
      pedido.estado !== "cancelado",
  );
  if (duplicado) {
    errores.docente = "Ya existe un pedido en curso para este docente en el período.";
  }

  // Reglas por novedad.
  if (datos.novedad === "Alta" || datos.novedad === "Cambio de cargo o dedicación") {
    if (!datos.cargoSolicitado) {
      errores.cargoSolicitado = "Seleccioná el cargo solicitado.";
    }
    if (!datos.dedicacionSolicitada) {
      errores.dedicacionSolicitada = "Seleccioná la dedicación solicitada.";
    } else if (
      datos.novedad === "Cambio de cargo o dedicación" &&
      datos.dedicacionActual &&
      indiceDedicacion(datos.dedicacionSolicitada) >= indiceDedicacion(datos.dedicacionActual)
    ) {
      // La dedicación solo puede mejorar en un Cambio (Categoría 0 = mayor jerarquía).
      errores.dedicacionSolicitada = "La dedicación solicitada debe ser mejor que la actual.";
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
    // Tipificación de la baja: tipo obligatorio; "Otro" exige detalle en texto libre.
    if (!datos.tipoBaja) {
      errores.tipoBaja = "Seleccioná el tipo de baja.";
    } else if (datos.tipoBaja === "Otro" && !datos.tipoBajaDetalle?.trim()) {
      errores.tipoBajaDetalle = 'Describí el motivo cuando el tipo de baja es "Otro".';
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
