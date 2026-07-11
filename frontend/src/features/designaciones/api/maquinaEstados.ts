// ============================================================
// Máquina de estados de los pedidos de designación — LÓGICA PURA.
// Sin React, sin Promise, sin I/O: dada (pedido, acción, actor),
// valida los guards y devuelve un pedido NUEVO o lanza
// ErrorDominioPedido. Es el corazón del TDD estricto del dominio.
//
// SCRUM-7 implementa las transiciones del lado del Jefe de Cátedra
// (enviar / cancelar / editar). SCRUM-8 EXTIENDE `AccionPedido` y el
// switch de `aplicarAccion` con aceptar / rechazar / devolver /
// reenviar / priorizar, sin reescribir lo que ya existe.
// ============================================================
import type {
  AccionHistorial,
  ActorContexto,
  DatosEditablesPedido,
  EstadoPedido,
  EventoHistorial,
  PedidoDesignacion,
  Rol,
} from "../types";

/** Error de dominio: un guard de la máquina de estados rechazó la acción. */
export class ErrorDominioPedido extends Error {
  constructor(mensaje: string) {
    super(mensaje);
    this.name = "ErrorDominioPedido";
  }
}

/**
 * Acciones que la máquina sabe aplicar.
 * SCRUM-8 suma aquí: `aceptar` | `rechazar` | `devolver` | `reenviar` | `priorizar`.
 */
export type AccionPedido =
  | { tipo: "enviar" }
  | { tipo: "cancelar" }
  | { tipo: "editar"; datos: DatosEditablesPedido }
  | { tipo: "aceptar"; comentario?: string }
  | { tipo: "rechazar"; comentario: string }
  | { tipo: "devolver"; comentario: string }
  | { tipo: "reenviar" }
  | { tipo: "priorizar"; comentario: string }
  | { tipo: "despriorizar"; comentario?: string };

const ESTADOS_TERMINALES: readonly EstadoPedido[] = ["cancelado", "rechazado", "en_lote"];

function esTerminal(estado: EstadoPedido): boolean {
  return ESTADOS_TERMINALES.includes(estado);
}

/** Construye un evento de historial para la transición aplicada. */
function nuevoEvento(
  accion: AccionHistorial,
  actor: ActorContexto,
  etapa: EstadoPedido,
  comentario?: string,
): EventoHistorial {
  return {
    id: crypto.randomUUID(),
    accion,
    porRol: actor.rol,
    porNombre: actor.nombre,
    etapa,
    comentario,
    fecha: new Date().toISOString(),
  };
}

/** Devuelve una copia del pedido con el evento agregado al historial. */
function conEvento(pedido: PedidoDesignacion, evento: EventoHistorial): PedidoDesignacion {
  return { ...pedido, historial: [...pedido.historial, evento] };
}

function enviar(pedido: PedidoDesignacion, actor: ActorContexto): PedidoDesignacion {
  if (pedido.estado !== "borrador") {
    throw new ErrorDominioPedido(
      `Solo se puede enviar a revisión un pedido en borrador (estado actual: "${pedido.estado}").`,
    );
  }
  if (actor.rol !== "Jefe de Cátedra") {
    throw new ErrorDominioPedido("Solo el Jefe de Cátedra puede enviar el pedido a revisión.");
  }
  const siguiente: PedidoDesignacion = { ...pedido, estado: "en_revision_coordinador" };
  return conEvento(siguiente, nuevoEvento("enviar", actor, siguiente.estado));
}

function cancelar(pedido: PedidoDesignacion, actor: ActorContexto): PedidoDesignacion {
  if (pedido.estado !== "borrador") {
    throw new ErrorDominioPedido(
      `Solo se puede cancelar un pedido en borrador (estado actual: "${pedido.estado}").`,
    );
  }
  if (actor.rol !== "Jefe de Cátedra") {
    throw new ErrorDominioPedido("Solo el Jefe de Cátedra puede cancelar el pedido.");
  }
  const siguiente: PedidoDesignacion = { ...pedido, estado: "cancelado" };
  return conEvento(siguiente, nuevoEvento("cancelar", actor, siguiente.estado));
}

function editar(
  pedido: PedidoDesignacion,
  actor: ActorContexto,
  datos: DatosEditablesPedido,
): PedidoDesignacion {
  const esBorradorDelJC = pedido.estado === "borrador" && actor.rol === "Jefe de Cátedra";
  const esDevueltoDelPropietario =
    pedido.estado === "devuelto" && pedido.propietarioActual === actor.rol;
  if (!esBorradorDelJC && !esDevueltoDelPropietario) {
    throw new ErrorDominioPedido(
      `No se puede editar el pedido en estado "${pedido.estado}" con el rol "${actor.rol}". ` +
        "Solo es editable en borrador o cuando fue devuelto al actor.",
    );
  }
  // editar no cambia el estado: aplica los datos del form y deja rastro.
  const siguiente: PedidoDesignacion = { ...pedido, ...datos };
  return conEvento(siguiente, nuevoEvento("editar", actor, siguiente.estado));
}

// ============================================================
// SCRUM-8 — Circuito de revisión: aceptar / rechazar / devolver /
// reenviar / priorizar, con guards de etapa [BR-013] y ámbito [BR-009].
// ============================================================

/** Etapa siguiente al aceptar (avance de la cadena). `en_lote` es terminal-prototipo. */
const SIGUIENTE_ETAPA: Partial<Record<EstadoPedido, EstadoPedido>> = {
  en_revision_coordinador: "en_revision_secretaria",
  en_revision_secretaria: "en_revision_decanato",
  en_revision_decanato: "en_lote",
};

/** Rol revisor esperado en cada etapa de revisión [BR-013]. */
const ROL_DE_ETAPA: Partial<Record<EstadoPedido, Rol>> = {
  en_revision_coordinador: "Coordinador",
  en_revision_secretaria: "Secretaría",
  en_revision_decanato: "Decanato",
};

/** Propietario que debe corregir cuando se devuelve desde cada etapa [BR-014]. */
const PROPIETARIO_DEVOLUCION: Partial<Record<EstadoPedido, Rol>> = {
  en_revision_coordinador: "Jefe de Cátedra",
  en_revision_secretaria: "Coordinador",
  en_revision_decanato: "Secretaría",
};

function esEtapaDeRevision(estado: EstadoPedido): boolean {
  return (
    estado === "en_revision_coordinador" ||
    estado === "en_revision_secretaria" ||
    estado === "en_revision_decanato"
  );
}

/**
 * ¿El actor alcanza el ámbito del pedido? [BR-009]
 * Coordinador: solo su carrera. Secretaría/Decanato/Administración: todo el
 * depto. Jefe de Cátedra: su cátedra. Docente: sin acceso a revisión.
 * Lo reutilizan la lectura por ámbito (`api/`) y los guards de revisión.
 */
export function actorAlcanzaAmbito(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  switch (actor.rol) {
    case "Coordinador":
      return pedido.carrera === actor.carrera;
    case "Secretaría":
    case "Decanato":
    case "Administración":
      return true;
    case "Jefe de Cátedra":
      return actor.catedra ? pedido.catedra === actor.catedra : pedido.carrera === actor.carrera;
    default:
      return false;
  }
}

/**
 * Guard de revisión: el pedido está en una etapa de revisión, el actor alcanza
 * el ámbito [BR-009] y es el revisor de la etapa actual o Administración [BR-013].
 */
function guardarRevisor(pedido: PedidoDesignacion, actor: ActorContexto): void {
  if (!esEtapaDeRevision(pedido.estado)) {
    throw new ErrorDominioPedido(
      `La acción de revisión requiere un pedido en revisión (estado actual: "${pedido.estado}").`,
    );
  }
  if (!actorAlcanzaAmbito(pedido, actor)) {
    throw new ErrorDominioPedido(
      `El pedido está fuera del ámbito del actor "${actor.rol}" [BR-009].`,
    );
  }
  const rolEsperado = ROL_DE_ETAPA[pedido.estado];
  if (actor.rol !== rolEsperado && actor.rol !== "Administración") {
    throw new ErrorDominioPedido(
      `Solo el revisor de la etapa actual (${rolEsperado}) o Administración puede actuar [BR-013].`,
    );
  }
}

/** Exige un comentario/justificativo no vacío para la acción dada [BR-005]. */
function requerirComentario(comentario: string | undefined, mensaje: string): string {
  if (!comentario || comentario.trim() === "") {
    throw new ErrorDominioPedido(mensaje);
  }
  return comentario;
}

function aceptar(
  pedido: PedidoDesignacion,
  actor: ActorContexto,
  comentario?: string,
): PedidoDesignacion {
  guardarRevisor(pedido, actor);
  if (actor.rol === "Administración") {
    throw new ErrorDominioPedido("Administración revisa pero no aprueba pedidos [BR-015].");
  }
  const siguiente = SIGUIENTE_ETAPA[pedido.estado];
  if (!siguiente) {
    throw new ErrorDominioPedido(`No hay etapa siguiente para "${pedido.estado}".`);
  }
  const next: PedidoDesignacion = { ...pedido, estado: siguiente };
  return conEvento(next, nuevoEvento("aceptar", actor, siguiente, comentario));
}

function rechazar(
  pedido: PedidoDesignacion,
  actor: ActorContexto,
  comentario: string,
): PedidoDesignacion {
  guardarRevisor(pedido, actor);
  const justificativo = requerirComentario(
    comentario,
    "El rechazo exige un justificativo obligatorio [BR-005].",
  );
  const next: PedidoDesignacion = { ...pedido, estado: "rechazado" };
  return conEvento(next, nuevoEvento("rechazar", actor, next.estado, justificativo));
}

function devolver(
  pedido: PedidoDesignacion,
  actor: ActorContexto,
  comentario: string,
): PedidoDesignacion {
  guardarRevisor(pedido, actor);
  const comentarioDevolucion = requerirComentario(
    comentario,
    "La devolución exige un comentario obligatorio [BR-005].",
  );
  const propietario = PROPIETARIO_DEVOLUCION[pedido.estado];
  const next: PedidoDesignacion = {
    ...pedido,
    estado: "devuelto",
    etapaRetorno: pedido.estado,
    propietarioActual: propietario,
  };
  return conEvento(next, nuevoEvento("devolver", actor, next.estado, comentarioDevolucion));
}

function reenviar(pedido: PedidoDesignacion, actor: ActorContexto): PedidoDesignacion {
  if (pedido.estado !== "devuelto") {
    throw new ErrorDominioPedido(
      `Solo se puede reenviar un pedido devuelto (estado actual: "${pedido.estado}").`,
    );
  }
  if (pedido.propietarioActual !== actor.rol) {
    throw new ErrorDominioPedido(
      "Solo el propietario del pedido devuelto puede reenviarlo [BR-014].",
    );
  }
  if (!pedido.etapaRetorno) {
    throw new ErrorDominioPedido("El pedido devuelto no tiene etapa de retorno.");
  }
  const etapa = pedido.etapaRetorno;
  const next: PedidoDesignacion = {
    ...pedido,
    estado: etapa,
    etapaRetorno: undefined,
    propietarioActual: undefined,
  };
  return conEvento(next, nuevoEvento("reenviar", actor, etapa));
}

function priorizar(
  pedido: PedidoDesignacion,
  actor: ActorContexto,
  comentario: string,
): PedidoDesignacion {
  if (!actorAlcanzaAmbito(pedido, actor)) {
    throw new ErrorDominioPedido(
      `El pedido está fuera del ámbito del actor "${actor.rol}" [BR-009].`,
    );
  }
  const motivo = requerirComentario(
    comentario,
    "Marcar prioritario exige un justificativo obligatorio [BR-017].",
  );
  // No cambia el estado: solo marca la prioridad y deja rastro.
  const next: PedidoDesignacion = { ...pedido, prioritario: true };
  return conEvento(next, nuevoEvento("priorizar", actor, pedido.estado, motivo));
}

function despriorizar(
  pedido: PedidoDesignacion,
  actor: ActorContexto,
  comentario?: string,
): PedidoDesignacion {
  if (!actorAlcanzaAmbito(pedido, actor)) {
    throw new ErrorDominioPedido(
      `El pedido está fuera del ámbito del actor "${actor.rol}" [BR-009].`,
    );
  }
  // A diferencia de priorizar, no exige justificativo: bajar la urgencia
  // no requiere la misma justificación que subirla.
  const next: PedidoDesignacion = { ...pedido, prioritario: false };
  return conEvento(next, nuevoEvento("despriorizar", actor, pedido.estado, comentario));
}

/**
 * Predicado de revisión: ¿el actor puede ejecutar acciones de revisión sobre
 * este pedido? (revisor de la etapa en su ámbito, o Administración). Lo usa la
 * UI para mostrar la botonera; la autoridad real la imponen los guards.
 */
export function puedeRevisar(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  if (!esEtapaDeRevision(pedido.estado)) {
    return false;
  }
  if (!actorAlcanzaAmbito(pedido, actor)) {
    return false;
  }
  return actor.rol === ROL_DE_ETAPA[pedido.estado] || actor.rol === "Administración";
}

/** ¿El actor puede aceptar (avanzar la cadena)? Administración nunca aprueba [BR-015]. */
export function puedeAceptar(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  return puedeRevisar(pedido, actor) && actor.rol !== "Administración";
}

/**
 * Predicado de edición: ¿el actor puede editar este pedido?
 * Mismo guard que la acción "editar" (borrador o devuelto del propietario,
 * nunca en estado terminal). Lo usa la UI para no ofrecer edición inválida.
 */
export function puedeEditarPedido(pedido: PedidoDesignacion, actor: ActorContexto): boolean {
  if (esTerminal(pedido.estado)) {
    return false;
  }
  const esBorradorDelJC = pedido.estado === "borrador" && actor.rol === "Jefe de Cátedra";
  const esDevueltoDelPropietario =
    pedido.estado === "devuelto" && pedido.propietarioActual === actor.rol;
  return esBorradorDelJC || esDevueltoDelPropietario;
}

/**
 * Valida los guards de la acción y devuelve el pedido resultante,
 * o lanza ErrorDominioPedido. No muta el pedido recibido.
 */
export function aplicarAccion(
  pedido: PedidoDesignacion,
  accion: AccionPedido,
  actor: ActorContexto,
): PedidoDesignacion {
  // Idempotencia terminal: ninguna acción procede sobre un estado terminal.
  if (esTerminal(pedido.estado)) {
    throw new ErrorDominioPedido(
      `El pedido está en un estado terminal ("${pedido.estado}"): no admite la acción "${accion.tipo}".`,
    );
  }

  switch (accion.tipo) {
    case "enviar":
      return enviar(pedido, actor);
    case "cancelar":
      return cancelar(pedido, actor);
    case "editar":
      return editar(pedido, actor, accion.datos);
    case "aceptar":
      return aceptar(pedido, actor, accion.comentario);
    case "rechazar":
      return rechazar(pedido, actor, accion.comentario);
    case "devolver":
      return devolver(pedido, actor, accion.comentario);
    case "reenviar":
      return reenviar(pedido, actor);
    case "priorizar":
      return priorizar(pedido, actor, accion.comentario);
    case "despriorizar":
      return despriorizar(pedido, actor, accion.comentario);
    default: {
      // Exhaustividad: cuando SCRUM-8 agregue acciones, TS marcará este punto.
      const accionNoSoportada: never = accion;
      throw new ErrorDominioPedido(
        `Acción no soportada: ${(accionNoSoportada as AccionPedido).tipo}`,
      );
    }
  }
}
