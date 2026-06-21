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
  | { tipo: "editar"; datos: DatosEditablesPedido };

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
    default: {
      // Exhaustividad: cuando SCRUM-8 agregue acciones, TS marcará este punto.
      const accionNoSoportada: never = accion;
      throw new ErrorDominioPedido(
        `Acción no soportada: ${(accionNoSoportada as AccionPedido).tipo}`,
      );
    }
  }
}
