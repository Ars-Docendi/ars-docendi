import type { Role } from "../../shared/auth/useCurrentUser";

export interface PeriodoDesignacion {
  id: string;
  nombre: string;
  /** Ventana en la que el Jefe de Cátedra puede cargar pedidos de designación. */
  cargaDesde: string;
  /** Límite blando: pasada esta fecha se sigue permitiendo cargar (el cierre real es manual, vía `activo`). */
  cargaHasta: string;
  /** Rango real de impacto de las designaciones (ej. 2do cuatrimestre = agosto-diciembre). */
  impactoDesde: string;
  impactoHasta: string;
  /** Solo puede haber un período con activo:true a la vez. */
  activo: boolean;
  version?: number;
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
export type Cargo = string;
export type Dedicacion = string;

/** Tipo de baja del docente (enum cerrado; "Otro" exige detalle en texto libre). */
export type TipoBaja = "Renuncia" | "Jubilación" | "Otro";

export type DepartamentoAgenteExterno =
  | "Departamento de Arquitectura"
  | "Departamento de Salud"
  | "Departamento de Derecho"
  | "Departamento de Económicas"
  | "Departamento de Humanidades"
  | "Departamento de Odontología"
  | "Secretaría Académica";

/**
 * Una materia del docente con su carga horaria. Modela una fila de la designación
 * vigente (`designaciones.designaciones` en el backend), NO una parte del pedido:
 * un pedido cubre exactamente una materia y lleva sus horas como campo propio.
 */
export interface AsignacionMateria {
  materia: string;
  horas: number;
}

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
  nombre: string;
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
  | "priorizar"
  | "despriorizar";

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
  /** Legajo institucional. Puede faltar en una Alta: el docente todavía no existe en el sistema. */
  legajo?: string;
}

/** Persona canónica disponible para un Alta, aun cuando todavía no tenga designación. */
export interface PersonaCatalogoPedido {
  id: string;
  dni: string;
  nombre: string;
  legajo?: string;
}

/**
 * Docente ya existente en el sistema, con su designación vigente.
 * Alimenta el selector de las novedades sobre docentes existentes
 * (Sin novedad / Baja / Cambio) y el panel de datos actuales read-only.
 * En el real provendría del módulo Portal / API Guaraní.
 */
export interface DocenteExistente {
  dni: string;
  nombre: string;
  /** Legajo institucional — un docente ya existente en el sistema siempre lo tiene. */
  legajo: string;
  antiguedad: number;
  cargoActual: Cargo;
  dedicacionActual: Dedicacion;
  /** Materias a las que pertenece el docente, con su carga horaria. Mínimo 1 elemento. */
  materiasActuales: AsignacionMateria[];
  /** Horas de investigación/externas vigentes del docente (base de comparación en Cambio). */
  horasInvestigacionActuales: number;
  horasExternasActuales: number;
}

export interface PedidoDesignacion {
  id: string;
  /** Número de trámite legible (formato "N°-AAAA-NNNN"). Lo asigna el backend al persistir. */
  numero?: string;
  periodoId: string; // FK al período (SCRUM-82)
  periodoNombre?: string;
  /**
   * La cátedra del pedido, que **es** su materia: el rol `jefe_catedra` tiene ámbito
   * de materia, así que cátedra y materia son el mismo concepto. Un pedido cubre
   * exactamente una, y de ella se deriva la carrera (un único Coordinador competente).
   */
  catedra: string;
  carrera: string; // para el ámbito del Coordinador
  docente: DocentePedido;
  /** Carga horaria del docente en la cátedra del pedido. */
  horas: number;
  cargoActual: Cargo | null;
  dedicacionActual: Dedicacion | null;
  novedad: Novedad;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  justificacion?: string;
  tipoBaja?: TipoBaja;
  tipoBajaDetalle?: string;
  horasExternas: number; // horas del docente en otro departamento (D2: libre, sin cierre)
  horasInvestigacion: number; // integración cross-module con Portal pendiente
  esAgenteExterno?: boolean;
  departamentoAgenteExterno?: DepartamentoAgenteExterno;
  adjuntos: Adjunto[];
  estado: EstadoPedido;
  prioritario: boolean;
  // cuando estado === "devuelto":
  etapaRetorno?: EstadoPedido; // a qué etapa de revisión vuelve al reenviar
  propietarioActual?: Rol; // quién debe corregir (JC / Coordinador / Secretaría)
  historial: EventoHistorial[];
  accionesPermitidas?: string[];
  version?: number;
  personaId?: string;
  materiaId?: string;
  cargoSolicitadoId?: string;
}

/**
 * Subconjunto editable de un pedido (lo que el form de alta/edición produce).
 * NO incluye `catedra`: la materia del pedido viene del ámbito del actor, no del
 * form — un Jefe de Cátedra sólo carga pedidos sobre la cátedra que tiene a cargo.
 */
export interface DatosEditablesPedido {
  docente: DocentePedido;
  /**
   * Cátedra del pedido, que es su materia. NO la elige el usuario: el form la
   * recibe del ámbito del actor y la reenvía tal cual, porque define a qué
   * Coordinador se rutea el pedido.
   */
  catedra: string;
  /** Carga horaria en la cátedra del pedido. */
  horas: number;
  cargoActual: Cargo | null;
  dedicacionActual: Dedicacion | null;
  novedad: Novedad;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  justificacion?: string;
  tipoBaja?: TipoBaja;
  tipoBajaDetalle?: string;
  horasExternas: number;
  horasInvestigacion: number;
  esAgenteExterno?: boolean;
  departamentoAgenteExterno?: DepartamentoAgenteExterno;
  adjuntos: Adjunto[];
  personaId?: string;
  materiaId?: string;
  cargoSolicitadoId?: string;
  periodoId?: string;
  version?: number;
}

/** Contexto presentacional derivado del usuario actual; nunca se envía como autoridad al backend. */
export interface ActorContexto {
  rol: Rol;
  nombre: string;
  carrera?: string; // ámbito del Coordinador (depto implícito para Secretaría/Decanato/Administración)
  /**
   * Cátedras que el Jefe de Cátedra tiene a cargo. Es una lista y no un valor
   * único porque el rol tiene ámbito de materia y se puede otorgar varias veces
   * al mismo usuario — se corresponde con `MateriasACargo` del backend.
   */
  catedras?: string[];
}
