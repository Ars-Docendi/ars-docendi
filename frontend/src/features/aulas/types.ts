export type EstadoSolicitudAula = "pendiente" | "aprobada" | "rechazada" | "cancelada";

export interface DocenteSolicitud {
  id: string;
  nombre: string;
  apellido: string;
  legajo?: string;
}

/** Materia acotada a las que el docente solicitante tiene asignadas (identity.materias). */
export interface MateriaOpcion {
  id: string;
  codigo: string;
  nombre: string;
}

export interface SolicitudReservaAula {
  id: string;
  dia: string;
  horarioDesde: string;
  horarioHasta: string;
  cantidadAlumnosAprox: number;
  materia: MateriaOpcion;
  comision: string;
  estado: EstadoSolicitudAula;
  /** Nombre del aula del catálogo; sólo en una solicitud aprobada. */
  aulaAsignada?: string;
  aulaId?: string;
  motivoRechazo?: string;
  /** Sólo en una reserva aprobada que un Administrativo canceló. */
  motivoCancelacion?: string;
  creadoEn: string;
  docente?: DocenteSolicitud;
  /** La solicitud es de quien consulta. El backend lo calcula; no se infiere en el cliente. */
  esPropia: boolean;
}

export interface DatosNuevaSolicitud {
  dia: string;
  horarioDesde: string;
  horarioHasta: string;
  cantidadAlumnosAprox: number;
  materiaId: string;
  comision: string;
}

export type TipoAula = "aula" | "laboratorio";

/** Aula o laboratorio del catálogo que administra quien tiene `aulas.gestionar`. */
export interface Aula {
  id: string;
  nombre: string;
  tipo: TipoAula;
  capacidad: number;
  equipamiento?: string;
  activa: boolean;
  version: number;
}

export interface DatosAula {
  nombre: string;
  tipo: TipoAula;
  capacidad: number;
  equipamiento?: string;
  version?: number;
}
