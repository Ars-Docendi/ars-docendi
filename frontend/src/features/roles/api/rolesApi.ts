import { apiClient } from "../../../shared/api/client";
import type { DatosRolEditables, DatosRolNuevo, PermisoRol, RolMock, ScopeRol } from "../models";

interface RolDto {
  id: string;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  ambito: ScopeRol;
  esSistema: boolean;
  activo: boolean;
  version: number;
  permisos: PermisoRol[];
}
const mapear = (rol: RolDto): RolMock => ({
  id: rol.id,
  codigo: rol.codigo,
  nombre: rol.nombre,
  descripcion: rol.descripcion ?? "",
  scope: rol.ambito,
  es_sistema: rol.esSistema,
  activo: rol.activo,
  version: rol.version,
  permisos: rol.permisos,
});

export async function listarRoles(): Promise<RolMock[]> {
  return (await apiClient.get<RolDto[]>("/api/administracion/roles")).data.map(mapear);
}
export async function crearRol(datos: DatosRolNuevo, permisoIds: string[]): Promise<RolMock> {
  const { data } = await apiClient.post<RolDto>("/api/administracion/roles", {
    nombre: datos.nombre,
    descripcion: datos.descripcion,
    ambito: datos.scope,
    permisoIds,
  });
  return mapear(data);
}
export async function editarRol(rol: RolMock, datos: DatosRolEditables): Promise<RolMock> {
  const { data } = await apiClient.put<RolDto>(`/api/administracion/roles/${rol.id}`, {
    nombre: datos.nombre,
    descripcion: datos.descripcion,
    ambito: datos.scope,
    version: rol.version,
  });
  return mapear(data);
}
export async function cambiarEstadoRol(rol: RolMock, activar: boolean): Promise<RolMock> {
  const accion = activar ? "activar" : "desactivar";
  const { data } = await apiClient.post<RolDto>(`/api/administracion/roles/${rol.id}/${accion}`, {
    version: rol.version,
  });
  return mapear(data);
}
export async function listarPermisos(): Promise<PermisoRol[]> {
  return (await apiClient.get<PermisoRol[]>("/api/administracion/permisos")).data;
}
export async function reemplazarPermisos(
  rol: RolMock,
  permisoIds: string[],
): Promise<PermisoRol[]> {
  return (
    await apiClient.put<PermisoRol[]>(`/api/administracion/roles/${rol.id}/permisos`, {
      permisoIds,
      version: rol.version,
    })
  ).data;
}
