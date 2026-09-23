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
  creadoEn: string;
  docente?: DocenteSolicitud;
}

export interface DatosNuevaSolicitud {
  dia: string;
  horarioDesde: string;
  horarioHasta: string;
  cantidadAlumnosAprox: number;
  materiaId: string;
  comision: string;
}
