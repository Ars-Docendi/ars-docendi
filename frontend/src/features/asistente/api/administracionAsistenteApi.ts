import { apiClient } from "../../../shared/api/client";
import type { MantenimientoDelAsistente, PeriodoDeUso, UsoDelAsistente } from "../types";

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

/** Edita el cupo diario default de un rol (`0` desactiva). Audita del lado del backend. */
export async function editarCupoDeRol(rol: string, cupo: number): Promise<void> {
  await apiClient.put(`/api/asistente/administracion/presupuestos/roles/${rol}`, { cupo });
}

/** Edita el override de cupo diario de un usuario puntual (`0` desactiva). */
export async function editarCupoDeUsuario(actorId: string, cupo: number): Promise<void> {
  await apiClient.put(`/api/asistente/administracion/presupuestos/usuarios/${actorId}`, { cupo });
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
