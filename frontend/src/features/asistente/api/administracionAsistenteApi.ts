import { apiClient } from "../../../shared/api/client";
import type {
  MantenimientoDelAsistente,
  PeriodoDeUso,
  PresupuestosDelAsistente,
  ServidorLocal,
  UsoDelAsistente,
} from "../types";

/**
 * Superficie admin de `asistente-administracion-de-uso` (grupo 9/6 del
 * backend): panel de uso, edición de presupuestos y modo mantenimiento.
 *
 * TODO exige `asistente.administrar` — el backend responde 403 a quien no lo
 * tiene, así que no hay chequeo de permiso duplicado acá: la ruta que monta
 * esta pantalla ya la protege `RequirePermission` (ver `routes.tsx`), igual
 * que `soporte-historial`.
 */
export async function obtenerUso(periodo: PeriodoDeUso): Promise<UsoDelAsistente> {
  const { data } = await apiClient.get<UsoDelAsistente>("/api/asistente/administracion/uso", {
    params: { periodo },
  });
  return data;
}

/**
 * El estado persistido de los presupuestos: el tope organizacional, el cupo
 * default de cada rol, cada override de usuario vigente, y el gasto estimado
 * del mes calendario en curso (tarea 12.8 de sistema-seccion-unificada).
 */
export async function obtenerPresupuestos(): Promise<PresupuestosDelAsistente> {
  const { data } = await apiClient.get<PresupuestosDelAsistente>(
    "/api/asistente/administracion/presupuestos",
  );
  return data;
}

/** Edita el cupo diario default de un rol (`0` desactiva). Audita del lado del backend. */
export async function editarCupoDeRol(rol: string, cupo: number): Promise<void> {
  await apiClient.put(`/api/asistente/administracion/presupuestos/roles/${rol}`, { cupo });
}

/** Edita el override de cupo diario de un usuario puntual (`0` desactiva). */
export async function editarCupoDeUsuario(actorId: string, cupo: number): Promise<void> {
  await apiClient.put(`/api/asistente/administracion/presupuestos/usuarios/${actorId}`, { cupo });
}

/**
 * Restablece el cupo de un usuario al de su rol: cierra la vigencia de su
 * override (asistente-acceso-granular).
 */
export async function restablecerCupoDeUsuario(actorId: string): Promise<void> {
  await apiClient.delete(`/api/asistente/administracion/presupuestos/usuarios/${actorId}`);
}

/** Prende o apaga el acceso de un rol al asistente. */
export async function editarAccesoDeRol(rol: string, habilitado: boolean): Promise<void> {
  await apiClient.put(`/api/asistente/administracion/presupuestos/roles/${rol}/acceso`, {
    habilitado,
  });
}

/**
 * Quita (`false`) o restablece (`true`) el acceso de un usuario. Restablecer
 * sólo borra la revocación: a un usuario se le puede quitar el acceso, no
 * darlo por encima de su rol.
 */
export async function editarAccesoDeUsuario(actorId: string, habilitado: boolean): Promise<void> {
  await apiClient.put(`/api/asistente/administracion/presupuestos/usuarios/${actorId}/acceso`, {
    habilitado,
  });
}

/** Edita el tope organizacional de gasto mensual estimado, en USD (`0` desactiva). */
export async function editarTopeOrganizacional(topeMensualUsd: number): Promise<void> {
  await apiClient.put("/api/asistente/administracion/tope-organizacional", { topeMensualUsd });
}

/** Prende o apaga el modo mantenimiento. `razon` es obligatoria para prenderlo. */
export async function editarMantenimiento(
  activo: boolean,
  razon?: string,
): Promise<MantenimientoDelAsistente> {
  const { data } = await apiClient.patch<MantenimientoDelAsistente>(
    "/api/asistente/administracion/mantenimiento",
    { activo, razon },
  );
  return data;
}

/**
 * La carga del servidor del modelo propio y de la compuerta del backend
 * (asistente-optimizaciones-modelo-local, D8). Con un proveedor que no es
 * `local` responde `configurado: false`.
 */
export async function obtenerServidorLocal(): Promise<ServidorLocal> {
  const { data } = await apiClient.get<ServidorLocal>(
    "/api/asistente/administracion/servidor-local",
  );
  return data;
}
