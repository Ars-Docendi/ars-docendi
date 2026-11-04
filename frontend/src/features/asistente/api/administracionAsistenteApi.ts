import { apiClient } from "../../../shared/api/client";
import type {
  CalidadAgregada,
  CredencialReemplazada,
  EstadoDelModelo,
  MantenimientoDelAsistente,
  ModeloAplicado,
  PaginaDeProblemas,
  PedidoDeModelo,
  PedidoDePrecio,
  PeriodoDeCalidad,
  PeriodoDeUso,
  PrecioDelModelo,
  PresupuestosDelAsistente,
  ResultadoDePrueba,
  ServidorLocal,
  TipoDeProblema,
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

/** Quita (`false`) o restablece (`true`) el acceso de un usuario. */
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
 * (asistente-optimizaciones-modelo-local, D8).
 */
export async function obtenerServidorLocal(): Promise<ServidorLocal> {
  const { data } = await apiClient.get<ServidorLocal>(
    "/api/asistente/administracion/servidor-local",
  );
  return data;
}

/**
 * Lo que está en uso, los servidores locales configurados y el catálogo con el
 * estado de la clave de cada empresa (asistente-modelo-en-caliente).
 */
export async function obtenerEstadoDelModelo(): Promise<EstadoDelModelo> {
  const { data } = await apiClient.get<EstadoDelModelo>("/api/asistente/administracion/modelo");
  return data;
}

/**
 * Una llamada mínima real, sin persistir nada. Una prueba que falla es un 200 con `exito: false` y
 * un código; un pedido inválido (400) o sin clave maestra (503) sí rechaza la promesa.
 */
export async function probarModelo(pedido: PedidoDeModelo): Promise<ResultadoDePrueba> {
  const { data } = await apiClient.post<ResultadoDePrueba>(
    "/api/asistente/administracion/modelo/prueba",
    pedido,
  );
  return data;
}

/** Re-prueba en el servidor, guarda la clave nueva si viene y fija el modelo. */
export async function aplicarModelo(pedido: PedidoDeModelo): Promise<ModeloAplicado> {
  const { data } = await apiClient.put<ModeloAplicado>(
    "/api/asistente/administracion/modelo",
    pedido,
  );
  return data;
}

/** Vuelve a la configuración del servidor. Las claves guardadas se conservan. */
export async function restablecerModelo(): Promise<void> {
  await apiClient.delete("/api/asistente/administracion/modelo");
}

/**
 * Valida la clave nueva contra el proveedor y la reemplaza sólo si pasa; si
 * falla, 422 y la vieja sigue guardada y en uso.
 */
export async function reemplazarClaveDeEmpresa(
  empresa: string,
  clave: string,
): Promise<CredencialReemplazada> {
  const { data } = await apiClient.post<CredencialReemplazada>(
    `/api/asistente/administracion/credenciales/${encodeURIComponent(empresa)}/reemplazo`,
    { clave },
  );
  return data;
}

/**
 * Escribe un precio como una versión nueva del modelo: cierra la vigente y abre la nueva en una
 * sola transacción.
 */
export async function cargarPrecio(pedido: PedidoDePrecio): Promise<PrecioDelModelo> {
  const { data } = await apiClient.post<PrecioDelModelo>(
    "/api/asistente/administracion/precios",
    pedido,
  );
  return data;
}

/** Aggregate quality metrics (asistente-metricas-de-calidad). */
export async function obtenerCalidad(
  periodo: PeriodoDeCalidad,
  modelo?: string,
): Promise<CalidadAgregada> {
  const { data } = await apiClient.get<CalidadAgregada>("/api/asistente/administracion/calidad", {
    params: { periodo, modelo },
  });
  return data;
}

/** One page (25 rows) of the anonymous problem list. */
export async function obtenerProblemasDeCalidad(
  periodo: PeriodoDeCalidad,
  pagina: number,
  modelo?: string,
  tipo?: TipoDeProblema,
): Promise<PaginaDeProblemas> {
  const { data } = await apiClient.get<PaginaDeProblemas>(
    "/api/asistente/administracion/calidad/problemas",
    { params: { periodo, modelo, tipo, pagina } },
  );
  return data;
}
