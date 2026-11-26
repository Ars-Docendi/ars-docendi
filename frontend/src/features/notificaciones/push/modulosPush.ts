import { rutaArticulo } from "../../ayuda/utils/rutas";

/** Help article that explains how to turn push on, on each phone. */
export const RUTA_AYUDA_PUSH = rutaArticulo(
  "primeros-pasos",
  "activar-las-notificaciones-en-el-celular",
);

/** Modules a user can switch push on or off for: one switch each, covering every notice it emits. */
export interface ModuloPush {
  /** Same code the API uses (`ModulosPush` in the backend). */
  id: string;
  titulo: string;
  descripcion: string;
  /** Any of these permissions of the active role means the user can receive the module. */
  permisos: readonly string[];
}

export const MODULOS_PUSH: readonly ModuloPush[] = [
  {
    id: "AULAS",
    titulo: "Reserva de aulas",
    descripcion: "Asignaciones, cambios, rechazos y cancelaciones de tus reservas.",
    permisos: ["aulas.solicitar", "aulas.aprobar"],
  },
  {
    id: "DESIGNACIONES",
    titulo: "Designaciones",
    descripcion: "Pedidos que esperan tu revisión, y los que te devuelven o rechazan.",
    permisos: ["designaciones.gestionar", "designaciones.revisar"],
  },
  {
    id: "TAREAS",
    titulo: "Tareas",
    descripcion: "Tareas que te asignan, cambios de estado y comentarios nuevos.",
    permisos: ["tareas.ver"],
  },
];

/** Modules the active role can receive; with no permission list (unknown) every module is shown. */
export function modulosVisibles(permisos: readonly string[] | undefined): readonly ModuloPush[] {
  if (permisos === undefined) return MODULOS_PUSH;
  const efectivos = new Set(permisos);
  return MODULOS_PUSH.filter((m) => m.permisos.some((p) => efectivos.has(p)));
}
