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
  aulaAsignada?: string;
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
