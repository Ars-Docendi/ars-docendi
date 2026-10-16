import type { Role } from "../../shared/auth/useCurrentUser";

/** Roles del sistema. Alias del `Role` del app shell (única fuente de verdad). */
export type Rol = Role;

export type EstadoTarea = "pendiente" | "en_curso" | "pausa" | "resuelta" | "cancelada";
export type Prioridad = "alta" | "media" | "baja";
export type TipoTarea =
  "extension" | "administrativa" | "posgrado" | "investigacion" | "academica" | "decanato";
/** Código del estado de un proyecto: la lista y su comportamiento salen del catálogo del servidor. */
export type EstadoProyecto = string;

/** Persona involucrada en una tarea (Responsable o Autor). */
export interface ActorTarea {
  nombre: string;
  rol: Rol;
}

/** Persona tal como la devuelve la API: id de usuario, nombre para mostrar y su rol de mayor jerarquía. */
export interface PersonaTarea extends ActorTarea {
  id: string;
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
  detalle?: string | null;
  fecha: string; // ISO
}

export interface Tarea {
  id: string;
  numero: number; // correlativo legible, asignado por la base al crear
  titulo: string;
  descripcion: string;
  fechaInicio: string; // ISO (solo fecha, yyyy-mm-dd)
  fechaFin: string; // ISO (solo fecha, yyyy-mm-dd) — vencimiento
  prioridad: Prioridad;
  tipo: TipoTarea;
  estado: EstadoTarea;
  porcentajeAvance: number; // 0-100, lo completa el Responsable
  solucion?: string | null; // detalle de resolución; obligatorio al pasar a "resuelta"
  responsable: PersonaTarea;
  creadoPor: PersonaTarea;
  comentarios: ComentarioTarea[]; // vacío en el listado; el detalle los trae
  historial: EventoHistorialTarea[]; // vacío en el listado; el detalle los trae
  proyectoId?: string | null; // opcional en tareas de primer nivel; heredado obligatorio si tareaPadreId existe
  tareaPadreId?: string | null; // presente solo si es una tarea hija
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
  responsableId: string;
  proyectoId?: string;
}

/** Candidato a Responsable devuelto por `GET /api/tareas/candidatos` (ya filtrado por la jerarquía). */
export interface PersonaCandidata extends PersonaTarea {
  usuario: string;
  legajo?: string | null;
  documento?: string | null;
}

/** Estado de proyecto del catálogo; `verbo` es la acción que lleva a él ("Finalizar", "Cancelar"). */
export interface EstadoProyectoCatalogo {
  codigo: string;
  nombre: string;
  verbo: string;
  esInicial: boolean;
  admiteTareas: boolean;
}

export interface Proyecto {
  id: string;
  numero: number; // correlativo legible, asignado por la base al crear
  nombre: string;
  descripcion: string;
  fechaInicio: string; // ISO (solo fecha)
  fechaFin: string; // ISO (solo fecha)
  estado: EstadoProyecto;
  estadoNombre: string;
  admiteTareas: boolean; // del catálogo: recibe tareas nuevas y genera cuadro en la pantalla inicial
  responsable: PersonaTarea; // Decanato o Secretaría Académica
}

/** Subconjunto editable de un proyecto (lo que el form de alta produce). */
export interface DatosEditablesProyecto {
  nombre: string;
  descripcion: string;
  fechaInicio: string;
  fechaFin: string;
  responsableId: string;
}
