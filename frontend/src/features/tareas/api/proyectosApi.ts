// ============================================================
// API mock de proyectos — EL SEAM DEL BACKEND. Cada función es async
// (Promise + latencia simulada), opera sobre `proyectosStore.ts`.
// Cuando llegue el backend (Modules.Tareas), se reemplaza el CUERPO de
// cada función por llamadas `apiClient.get/post(...)` MANTENIENDO LA
// FIRMA. Mismo patrón que `tareasApi.ts`.
// ============================================================
import type { ActorTarea, DatosEditablesProyecto, EstadoProyecto, Proyecto } from "../types";
import {
  ErrorDominioProyecto,
  puedeAsignarComoResponsableProyecto,
  puedeCambiarEstadoProyecto,
  puedeCrearProyecto,
} from "./maquinaEstadosProyecto";
import * as store from "./proyectosStore";

/** Latencia simulada para que la UI ejercite los estados de carga. */
function demora(ms = 250): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}

function requerirProyecto(id: string): Proyecto {
  const proyecto = store.buscar(id);
  if (!proyecto) {
    throw new Error(`No se encontró el proyecto con id "${id}".`);
  }
  return proyecto;
}

function siguienteNumero(): number {
  return store.leerTodos().reduce((max, p) => Math.max(max, p.numero), 0) + 1;
}

// TODO(backend): GET /api/tareas/proyectos (Modules.Tareas). Mock actual:
//   lee el store completo — el listado es el mismo para todos los roles.
export async function listarProyectos(): Promise<Proyecto[]> {
  await demora();
  return store.leerTodos();
}

// TODO(backend): GET /api/tareas/proyectos/:id (Modules.Tareas).
export async function obtenerProyecto(id: string): Promise<Proyecto> {
  await demora();
  return requerirProyecto(id);
}

// TODO(backend): POST /api/tareas/proyectos (Modules.Tareas), restringido a
//   Secretaría Académica y Decanato. Mock actual: valida el rol con
//   `puedeCrearProyecto`, la jerarquía+universo del Responsable con
//   `puedeAsignarComoResponsableProyecto`, y crea el proyecto en Abierto.
export async function crearProyecto(
  datos: DatosEditablesProyecto,
  actor: ActorTarea,
): Promise<Proyecto> {
  await demora();
  if (!puedeCrearProyecto(actor)) {
    throw new ErrorDominioProyecto("Solo Secretaría Académica o Decanato pueden crear proyectos.");
  }
  if (!puedeAsignarComoResponsableProyecto(actor, datos.responsable)) {
    throw new ErrorDominioProyecto(
      "El Responsable de un Proyecto debe ser Secretaría Académica o Decanato, respetando la jerarquía.",
    );
  }
  const nuevo: Proyecto = {
    id: crypto.randomUUID(),
    numero: siguienteNumero(),
    nombre: datos.nombre,
    descripcion: datos.descripcion,
    fechaInicio: datos.fechaInicio,
    fechaFin: datos.fechaFin,
    estado: "abierto",
    responsable: datos.responsable,
  };
  return store.guardar(nuevo);
}

// TODO(backend): POST /api/tareas/proyectos/:id/estado (Modules.Tareas),
//   restringido a Secretaría Académica y Decanato (por rol, no ownership).
export async function cambiarEstadoProyecto(
  id: string,
  estadoDestino: EstadoProyecto,
  actor: ActorTarea,
): Promise<Proyecto> {
  await demora();
  if (!puedeCambiarEstadoProyecto(actor)) {
    throw new ErrorDominioProyecto(
      "Solo Secretaría Académica o Decanato pueden cambiar el estado de un proyecto.",
    );
  }
  const actual = requerirProyecto(id);
  return store.guardar({ ...actual, estado: estadoDestino });
}
