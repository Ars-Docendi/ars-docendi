// ============================================================
// API mock de tareas — EL SEAM DEL BACKEND. Cada función es async
// (Promise + latencia simulada), opera sobre el store mock y delega
// las transiciones de estado a `maquinaEstadosTarea.ts`. Cuando llegue
// el backend (Modules.Tareas), se reemplaza el CUERPO de cada función
// por llamadas `apiClient.get/post(...)` MANTENIENDO LA FIRMA. Mismo
// patrón que `designaciones/api/pedidosApi.ts`.
// ============================================================
import type {
  ActorTarea,
  ComentarioTarea,
  DatosEditablesTarea,
  EstadoTarea,
  Tarea,
} from "../types";
import {
  aplicarAccionTarea,
  ErrorDominioTarea,
  puedeAsignarComoResponsable,
  puedeCrearTarea,
} from "./maquinaEstadosTarea";
import * as store from "./tareasStore";

/** Latencia simulada para que la UI ejercite los estados de carga. */
function demora(ms = 250): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}

function requerirTarea(id: string): Tarea {
  const tarea = store.buscar(id);
  if (!tarea) {
    throw new Error(`No se encontró la tarea con id "${id}".`);
  }
  return tarea;
}

// TODO(backend): GET /api/tareas (Modules.Tareas). Mock actual: lee el store
//   completo — el listado es el mismo para todos los roles. Mantener la firma.
export async function listarTareas(): Promise<Tarea[]> {
  await demora();
  return store.leerTodas();
}

// TODO(backend): GET /api/tareas/:id (Modules.Tareas).
//   Mock actual: busca en el store. Mantener la firma.
export async function obtenerTarea(id: string): Promise<Tarea> {
  await demora();
  return requerirTarea(id);
}

// TODO(backend): POST /api/tareas (Modules.Tareas), restringido a
//   Secretaría Académica/Decanato/Administrativo. Mock actual: valida el rol con
//   `puedeCrearTarea`, la jerarquía de asignación del Responsable con
//   `puedeAsignarComoResponsable`, y crea la tarea en Pendiente con avance 0.
//   `tareaPadreId` presente ⇒ es una tarea hija: el Proyecto se hereda del
//   padre, ignorando `datos.proyectoId`. Mantener la firma.
export async function crearTarea(
  datos: DatosEditablesTarea,
  actor: ActorTarea,
  tareaPadreId?: string,
): Promise<Tarea> {
  await demora();
  if (!puedeCrearTarea(actor)) {
    throw new ErrorDominioTarea(
      "Solo Secretaría Académica, Decanato o Administrativo pueden crear tareas.",
    );
  }
  if (!puedeAsignarComoResponsable(actor, datos.responsable)) {
    throw new ErrorDominioTarea("No podés asignar un Responsable de mayor jerarquía que la tuya.");
  }

  let proyectoId = datos.proyectoId;
  if (tareaPadreId) {
    proyectoId = requerirTarea(tareaPadreId).proyectoId;
  }

  const nueva: Tarea = {
    id: crypto.randomUUID(),
    numero: siguienteNumero(),
    titulo: datos.titulo,
    descripcion: datos.descripcion,
    fechaInicio: datos.fechaInicio,
    fechaFin: datos.fechaFin,
    prioridad: datos.prioridad,
    tipo: datos.tipo,
    estado: "pendiente",
    porcentajeAvance: 0,
    responsable: datos.responsable,
    creadoPor: actor,
    comentarios: [],
    historial: [
      {
        id: crypto.randomUUID(),
        accion: "crear",
        porRol: actor.rol,
        porNombre: actor.nombre,
        estado: "pendiente",
        fecha: new Date().toISOString(),
      },
    ],
    proyectoId,
    tareaPadreId,
    tareasRelacionadasIds: [],
  };
  return store.guardar(nueva);
}

function siguienteNumero(): number {
  return store.leerTodas().reduce((max, t) => Math.max(max, t.numero), 0) + 1;
}

// TODO(backend): PUT /api/tareas/:id (Modules.Tareas), exclusivo de la autoridad creadora.
//   Mock actual: valida el guard con la máquina de estados y guarda. Mantener la firma.
export async function editarTarea(
  id: string,
  datos: DatosEditablesTarea,
  actor: ActorTarea,
): Promise<Tarea> {
  await demora();
  const actual = requerirTarea(id);
  const siguiente = aplicarAccionTarea(actual, { tipo: "editar", datos }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/tareas/:id/estado (Modules.Tareas).
//   Mock actual: transiciona vía la máquina de estados (Pausa exige comentario,
//   Resuelta exige solución, Cancelar exclusivo de la autoridad). Mantener la firma.
export async function cambiarEstadoTarea(
  id: string,
  estadoDestino: EstadoTarea,
  actor: ActorTarea,
  opciones: { comentario?: string; solucion?: string } = {},
): Promise<Tarea> {
  await demora();
  const actual = requerirTarea(id);
  const siguiente = aplicarAccionTarea(
    actual,
    { tipo: "cambiarEstado", estadoDestino, ...opciones },
    actor,
  );
  return store.guardar(siguiente);
}

// TODO(backend): PATCH /api/tareas/:id/avance (Modules.Tareas), exclusivo del
//   Responsable o la autoridad creadora. Mock actual: valida rango 0-100. Mantener la firma.
export async function editarAvance(
  id: string,
  porcentajeAvance: number,
  actor: ActorTarea,
): Promise<Tarea> {
  await demora();
  const actual = requerirTarea(id);
  const siguiente = aplicarAccionTarea(actual, { tipo: "editarAvance", porcentajeAvance }, actor);
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/tareas/:id/comentarios (Modules.Tareas).
//   Mock actual: agrega el comentario al hilo, sin restricción de rol. Mantener la firma.
export async function agregarComentario(
  id: string,
  actor: ActorTarea,
  texto: string,
): Promise<Tarea> {
  await demora();
  const actual = requerirTarea(id);
  const nuevo: ComentarioTarea = {
    id: crypto.randomUUID(),
    autor: actor.nombre,
    rolAutor: actor.rol,
    texto,
    fecha: new Date().toISOString(),
  };
  const siguiente: Tarea = { ...actual, comentarios: [...actual.comentarios, nuevo] };
  return store.guardar(siguiente);
}

// TODO(backend): POST /api/tareas/:id/relaciones (Modules.Tareas). Mock
//   actual: vínculo simple, sin jerarquía ni efecto en estado/avance —
//   se persiste en ambas tareas para que la consulta de lectura sea trivial.
export async function agregarRelacion(idA: string, idB: string): Promise<void> {
  await demora();
  if (idA === idB) {
    throw new ErrorDominioTarea("Una tarea no puede relacionarse consigo misma.");
  }
  const a = requerirTarea(idA);
  const b = requerirTarea(idB);
  if (!a.tareasRelacionadasIds.includes(idB)) {
    store.guardar({ ...a, tareasRelacionadasIds: [...a.tareasRelacionadasIds, idB] });
  }
  if (!b.tareasRelacionadasIds.includes(idA)) {
    store.guardar({ ...b, tareasRelacionadasIds: [...b.tareasRelacionadasIds, idA] });
  }
}

// TODO(backend): DELETE /api/tareas/:id/relaciones/:otraId (Modules.Tareas).
export async function quitarRelacion(idA: string, idB: string): Promise<void> {
  await demora();
  const a = requerirTarea(idA);
  const b = requerirTarea(idB);
  store.guardar({
    ...a,
    tareasRelacionadasIds: a.tareasRelacionadasIds.filter((id) => id !== idB),
  });
  store.guardar({
    ...b,
    tareasRelacionadasIds: b.tareasRelacionadasIds.filter((id) => id !== idA),
  });
}
