import type { Role } from "../../shared/auth/useCurrentUser";

export type EstadoPeriodo = "abierto" | "cerrado" | "proximo";

export interface PeriodoDesignacion {
  id: string;
  nombre: string;
  cuatrimestre: "1C" | "2C" | "Verano";
  anio: number;
  fechaApertura: string;
  fechaCierre: string;
  estado: EstadoPeriodo;
}

// ============================================================
// Pedidos de designación (SCRUM-7 + SCRUM-8).
// El modelo se declara completo —incluidos los estados y campos
// que recién ejercita SCRUM-8 (revisión)— para que el flujo de
// aprobación extienda la máquina de estados sin romper el tipo.
// ============================================================

/** Roles del sistema. Alias del `Role` del app shell (única fuente de verdad). */
export type Rol = Role;

export type Novedad = "Sin novedad" | "Alta" | "Baja" | "Cambio de cargo o dedicación";
export type Cargo = "Titular" | "Adjunto" | "JTP" | "Ayudante";
export type Dedicacion =
  | "Categoría 1"
  | "Categoría 2"
  | "Categoría 3"
  | "Categoría 4"
  | "Categoría 5"
  | "Categoría 6";

export type EstadoPedido =
  | "borrador"
  | "en_revision_coordinador"
  | "en_revision_secretaria"
  | "en_revision_decanato"
  | "devuelto" // sub-estado: volvió a un actor anterior para corrección
  | "en_lote" // terminal-para-el-prototipo (flujo real: → Universitaria → Aprobado)
  | "rechazado" // terminal
  | "cancelado"; // terminal

export type TipoAdjunto = "cv" | "dni_frente" | "dni_dorso" | "justificativo";

export interface Adjunto {
  id: string;
  nombre: string; // solo nombre/tipo en el mock
  tipo: TipoAdjunto;
}

export type AccionHistorial =
  | "crear"
  | "enviar"
  | "aceptar"
  | "rechazar"
  | "devolver"
  | "reenviar"
  | "editar"
  | "cancelar"
  | "priorizar";

export interface EventoHistorial {
  id: string;
  accion: AccionHistorial;
  porRol: Rol;
  porNombre: string;
  etapa: EstadoPedido; // estado del pedido al momento de registrar el evento
  comentario?: string; // justificativo (rechazo) / comentario (devolución) / motivo (prioridad)
  fecha: string; // ISO
}

export interface DocentePedido {
  dni: string;
  nombre: string;
  antiguedad: number;
}

export interface PedidoDesignacion {
  id: string;
  periodoId: string; // FK al período (SCRUM-82)
  catedra: string;
  carrera: string; // para el ámbito del Coordinador
  docente: DocentePedido;
  materiaAsociada: string;
  cargoActual: Cargo | null;
  dedicacionActual: Dedicacion | null;
  novedad: Novedad;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  justificacion?: string;
  haceHorasOtroDepto: boolean;
  horasInvestigacion?: number; // mock (cross-module Portal en el real)
  adjuntos: Adjunto[];
  estado: EstadoPedido;
  prioritario: boolean;
  // cuando estado === "devuelto":
  etapaRetorno?: EstadoPedido; // a qué etapa de revisión vuelve al reenviar
  propietarioActual?: Rol; // quién debe corregir (JC / Coordinador / Secretaría)
  historial: EventoHistorial[];
}

/** Subconjunto editable de un pedido (lo que el form de alta/edición produce). */
export interface DatosEditablesPedido {
  docente: DocentePedido;
  materiaAsociada: string;
  cargoActual: Cargo | null;
  dedicacionActual: Dedicacion | null;
  novedad: Novedad;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  justificacion?: string;
  haceHorasOtroDepto: boolean;
  adjuntos: Adjunto[];
}

/** Contexto del actor que ejecuta una acción (rol + ámbito), derivado de useCurrentUser + mock. */
export interface ActorContexto {
  rol: Rol;
  nombre: string;
  carrera?: string; // ámbito del Coordinador (depto implícito para Secretaría/Decanato/Administración)
  catedra?: string; // ámbito del Jefe de Cátedra (cátedra que posee)
}
