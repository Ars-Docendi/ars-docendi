// ============================================================
// Store mock de proyectos. Singleton en memoria hidratado desde
// localStorage (clave "adoc.mock.tareas.proyectos.v1") y persistido en
// cada escritura. Lectura/escritura SÍNCRONA: NO lo consumen los
// componentes directamente — solo lo usa `proyectosApi.ts` (el seam del
// backend). Store separado del de tareas, no un campo embebido — las
// tareas solo guardan `proyectoId` como referencia. Mismo patrón que
// `tareasStore.ts`.
// ============================================================
import type { Proyecto } from "../types";
import { crearSeedProyectos } from "./proyectosSeed";

const CLAVE = "adoc.mock.tareas.proyectos.v1";

let proyectos: Proyecto[] | null = null;

function persistir(): void {
  if (proyectos !== null) {
    localStorage.setItem(CLAVE, JSON.stringify(proyectos));
  }
}

/** Hidrata el singleton desde localStorage; si está vacío, siembra el seed. */
function asegurarHidratado(): Proyecto[] {
  if (proyectos === null) {
    const crudo = localStorage.getItem(CLAVE);
    if (crudo) {
      proyectos = JSON.parse(crudo) as Proyecto[];
    } else {
      proyectos = crearSeedProyectos();
      persistir();
    }
  }
  return proyectos;
}

export function leerTodos(): Proyecto[] {
  return asegurarHidratado().map((p) => structuredClone(p));
}

export function buscar(id: string): Proyecto | undefined {
  const encontrado = asegurarHidratado().find((p) => p.id === id);
  return encontrado ? structuredClone(encontrado) : undefined;
}

/** Inserta o reemplaza un proyecto (upsert por id) y persiste. */
export function guardar(proyecto: Proyecto): Proyecto {
  const lista = asegurarHidratado();
  const indice = lista.findIndex((p) => p.id === proyecto.id);
  if (indice >= 0) {
    lista[indice] = structuredClone(proyecto);
  } else {
    lista.push(structuredClone(proyecto));
  }
  persistir();
  return structuredClone(proyecto);
}

/** Reemplaza todo el contenido del store (útil para tests). */
export function sembrarProyectos(iniciales: Proyecto[]): void {
  proyectos = iniciales.map((p) => structuredClone(p));
  persistir();
}

/** Resetea el singleton: la próxima lectura re-hidrata desde localStorage. */
export function reiniciarStoreProyectos(): void {
  proyectos = null;
}
