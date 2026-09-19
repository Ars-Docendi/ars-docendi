// ============================================================
// Store mock de configuraciones de filtros guardadas del listado de
// tareas. Singleton en memoria hidratado desde localStorage (clave
// "adoc.mock.tareas.filtros.v1"). Cada configuración queda asociada al
// nombre del actor que la guardó (no hay autenticación real: "por
// usuario" es un mock por nombre, igual que el resto del módulo).
// ============================================================
import type { FiltrosTareasState } from "../components/filtrosTareas";

export interface ConfiguracionFiltro {
  id: string;
  nombre: string;
  propietario: string;
  filtros: FiltrosTareasState;
}

const CLAVE = "adoc.mock.tareas.filtros.v1";

let configuraciones: ConfiguracionFiltro[] | null = null;

function persistir(): void {
  if (configuraciones !== null) {
    localStorage.setItem(CLAVE, JSON.stringify(configuraciones));
  }
}

function asegurarHidratado(): ConfiguracionFiltro[] {
  if (configuraciones === null) {
    const crudo = localStorage.getItem(CLAVE);
    configuraciones = crudo ? (JSON.parse(crudo) as ConfiguracionFiltro[]) : [];
  }
  return configuraciones;
}

/** Lista las configuraciones guardadas por un usuario (nombre del actor). */
export function leerDeUsuario(propietario: string): ConfiguracionFiltro[] {
  return asegurarHidratado()
    .filter((c) => c.propietario === propietario)
    .map((c) => structuredClone(c));
}

/** Guarda una nueva configuración de filtros y persiste. */
export function guardar(config: ConfiguracionFiltro): ConfiguracionFiltro {
  const lista = asegurarHidratado();
  lista.push(structuredClone(config));
  persistir();
  return structuredClone(config);
}

/** Resetea el singleton: la próxima lectura re-hidrata desde localStorage. */
export function reiniciarStoreFiltrosGuardados(): void {
  configuraciones = null;
}
