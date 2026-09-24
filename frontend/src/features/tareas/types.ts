import type { Role } from "../../shared/auth/useCurrentUser";

/** Roles del sistema. Alias del `Role` del app shell (única fuente de verdad). */
export type Rol = Role;

export type EstadoTarea = "pendiente" | "en_curso" | "pausa" | "resuelta" | "cancelada";
export type Prioridad = "alta" | "media" | "baja";
export type TipoTarea =
  "extension" | "administrativa" | "posgrado" | "investigacion" | "academica" | "decanato";
export type EstadoProyecto = "abierto" | "finalizado" | "cancelado";

/** Persona involucrada en una tarea (Responsable o Autor). */
export interface ActorTarea {
  nombre: string;
  rol: Rol;
}

export interface ComentarioTarea {
  id: string;
  autor: string;
  rolAutor: Rol;
  texto: string;
  fecha: string; // ISO
}

export type AccionHistorialTarea = "crear" | "cambiar_estado" | "editar_avance" | "editar";

export interface EventoHistorialTarea {
  id: string;
  accion: AccionHistorialTarea;
  porRol: Rol;
  porNombre: string;
  estado: EstadoTarea; // estado de la tarea al momento del evento
  detalle?: string;
  fecha: string; // ISO
}

export interface Tarea {
  id: string;
  numero: number; // correlativo legible, asignado por el store al crear
  titulo: string;
  descripcion: string;
  fechaInicio: string; // ISO (solo fecha, yyyy-mm-dd)
  fechaFin: string; // ISO (solo fecha, yyyy-mm-dd) — vencimiento
  prioridad: Prioridad;
  tipo: TipoTarea;
  estado: EstadoTarea;
  porcentajeAvance: number; // 0-100, lo completa el Responsable
  solucion?: string; // detalle de resolución; obligatorio al pasar a "resuelta"
  responsable: ActorTarea;
  creadoPor: ActorTarea;
  comentarios: ComentarioTarea[];
  historial: EventoHistorialTarea[];
  proyectoId?: string; // opcional en tareas de primer nivel; heredado obligatorio si tareaPadreId existe
  tareaPadreId?: string; // presente solo si es una tarea hija
  tareasRelacionadasIds: string[]; // vínculo simple bidireccional, sin jerarquía
}

/** Subconjunto editable de una tarea (lo que el form de alta/edición produce). */
export interface DatosEditablesTarea {
  titulo: string;
  descripcion: string;
  fechaInicio: string;
  fechaFin: string;
  prioridad: Prioridad;
  tipo: TipoTarea;
  responsable: ActorTarea;
  proyectoId?: string;
}

/** Candidato a Responsable/Autor, para el combobox buscable. Ver `api/personasSeed.ts`. */
export interface PersonaCandidata {
  nombre: string;
  rol: Rol;
}

export interface Proyecto {
  id: string;
  numero: number; // correlativo legible, asignado por el store al crear
  nombre: string;
  descripcion: string;
  fechaInicio: string; // ISO (solo fecha)
  fechaFin: string; // ISO (solo fecha)
  estado: EstadoProyecto;
  responsable: ActorTarea; // restringido a rol Secretaría o Decanato
}

/** Subconjunto editable de un proyecto (lo que el form de alta produce). */
export interface DatosEditablesProyecto {
  nombre: string;
  descripcion: string;
  fechaInicio: string;
  fechaFin: string;
  responsable: ActorTarea;
}
