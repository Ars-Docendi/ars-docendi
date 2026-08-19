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
}

// ============================================================
// Pedidos de designación (SCRUM-7 + SCRUM-8).
// El modelo se declara completo —incluidos los estados y campos
// que recién ejercita SCRUM-8 (revisión)— para que el flujo de
// aprobación extienda la máquina de estados sin romper el tipo.
// ============================================================

/** Roles del sistema. Alias del `Role` del app shell (única fuente de verdad). */
export type Rol = Role;

export type Novedad = "Alta" | "Baja" | "Cambio de cargo o dedicación";
export type Cargo = "Titular" | "Adjunto" | "JTP" | "Ayudante";
export type Dedicacion =
  | "Categoría 0"
  | "Categoría 1"
  | "Categoría 2"
  | "Categoría 3"
  | "Categoría 4"
  | "Categoría 5"
  | "Categoría 6";

/** Tipo de baja del docente (enum cerrado; "Otro" exige detalle en texto libre). */
export type TipoBaja = "Renuncia" | "Jubilación" | "Otro";

/** Una materia asignada a un pedido, con su carga horaria. */
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

/**
 * Docente ya existente en el sistema, con su designación vigente.
 * Alimenta el selector de las novedades sobre docentes existentes
 * (Baja / Cambio) y el panel de datos actuales read-only.
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
  catedra: string;
  carrera: string; // para el ámbito del Coordinador
  docente: DocentePedido;
  /** Materias del pedido con su carga horaria. Mínimo 1 elemento (BR: no puede quedar vacío). */
  asignaciones: AsignacionMateria[];
  cargoActual: Cargo | null;
  dedicacionActual: Dedicacion | null;
  novedad: Novedad;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  justificacion?: string;
  tipoBaja?: TipoBaja;
  tipoBajaDetalle?: string;
  horasExternas: number; // horas del docente en otro departamento (D2: libre, sin cierre)
  horasInvestigacion: number; // mock (cross-module Portal en el real)
  /** Docente contratado como agente externo. Sin "valor actual": dato nuevo, sin histórico previo. */
  esAgenteExterno: boolean;
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
  asignaciones: AsignacionMateria[];
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
  esAgenteExterno: boolean;
  adjuntos: Adjunto[];
}

/** Contexto del actor que ejecuta una acción (rol + ámbito), derivado de useCurrentUser + mock. */
export interface ActorContexto {
  rol: Rol;
  nombre: string;
  carrera?: string; // ámbito del Coordinador (depto implícito para Secretaría/Decanato/Administración)
  catedra?: string; // ámbito del Jefe de Cátedra (cátedra que posee)
}
