// ============================================================
// Store mock de tareas. Singleton en memoria hidratado desde
// localStorage (clave "adoc.mock.tareas.v1") y persistido en cada
// escritura. Lectura/escritura SÍNCRONA: NO lo consumen los
// componentes directamente — solo lo usa `tareasApi.ts` (el seam del
// backend). Las copias (structuredClone) evitan que el caller mute el
// estado guardado por referencia. Mismo patrón que
// `designaciones/api/pedidosStore.ts`.
// ============================================================
import type { Tarea } from "../types";
import { crearSeedTareas } from "./tareasSeed";

const CLAVE = "adoc.mock.tareas.v1";

let tareas: Tarea[] | null = null;

function persistir(): void {
  if (tareas !== null) {
    localStorage.setItem(CLAVE, JSON.stringify(tareas));
  }
}

/** Hidrata el singleton desde localStorage; si está vacío, siembra el seed. */
function asegurarHidratado(): Tarea[] {
  if (tareas === null) {
    const crudo = localStorage.getItem(CLAVE);
    if (crudo) {
      tareas = JSON.parse(crudo) as Tarea[];
    } else {
      tareas = crearSeedTareas();
      persistir();
    }
  }
  return tareas;
}

export function leerTodas(): Tarea[] {
  return asegurarHidratado().map((t) => structuredClone(t));
}

export function buscar(id: string): Tarea | undefined {
  const encontrada = asegurarHidratado().find((t) => t.id === id);
  return encontrada ? structuredClone(encontrada) : undefined;
}

/** Inserta o reemplaza una tarea (upsert por id) y persiste. */
export function guardar(tarea: Tarea): Tarea {
  const lista = asegurarHidratado();
  const indice = lista.findIndex((t) => t.id === tarea.id);
  if (indice >= 0) {
    lista[indice] = structuredClone(tarea);
  } else {
    lista.push(structuredClone(tarea));
  }
  persistir();
  return structuredClone(tarea);
}

/** Reemplaza todo el contenido del store (útil para tests). */
export function sembrarTareas(iniciales: Tarea[]): void {
  tareas = iniciales.map((t) => structuredClone(t));
  persistir();
}

/** Resetea el singleton: la próxima lectura re-hidrata desde localStorage. */
export function reiniciarStoreTareas(): void {
  tareas = null;
}
