import { apiClient } from "../../../shared/api/client";
import type { RolSistema, UsuarioMock } from "../models";

interface AsignacionDto {
  rolId: string;
  nombre: string;
  ambito: string;
  materiaId: string | null;
  carreraId: string | null;
}
interface UsuarioDto {
  id: string;
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string | null;
  cuil: string | null;
  fechaNacimiento: string | null;
  telefono: string | null;
  upn: string;
  activo: boolean;
  version: number;
  roles: AsignacionDto[];
}
export interface CatalogosUsuarios {
  roles: { id: string; codigo: string; nombre: string; ambito: string }[];
  carreras: { id: string; codigo: string; nombre: string }[];
  materias: { id: string; codigo: string; nombre: string }[];
}

export async function listarUsuarios(): Promise<UsuarioMock[]> {
  const { data } = await apiClient.get<UsuarioDto[]>("/api/administracion/usuarios");
  return data.map(mapearUsuario);
}
export async function obtenerCatalogosUsuarios(): Promise<CatalogosUsuarios> {
  return (await apiClient.get<CatalogosUsuarios>("/api/administracion/catalogos")).data;
}
export async function crearUsuario(
  datos: Omit<UsuarioMock, "id" | "is_active">,
  catalogos: CatalogosUsuarios,
): Promise<UsuarioMock> {
  const { data } = await apiClient.post<UsuarioDto>(
    "/api/administracion/usuarios",
    payload(datos, catalogos),
  );
  return mapearUsuario(data);
}
export async function editarUsuario(
  id: string,
  datos: Omit<UsuarioMock, "id" | "is_active">,
  catalogos: CatalogosUsuarios,
): Promise<UsuarioMock> {
  const { data } = await apiClient.put<UsuarioDto>(
    `/api/administracion/usuarios/${id}`,
    payload(datos, catalogos),
  );
  return mapearUsuario(data);
}
export async function cambiarEstadoUsuario(
  usuario: UsuarioMock,
  activo: boolean,
): Promise<UsuarioMock> {
  const accion = activo ? "activar" : "desactivar";
  const { data } = await apiClient.post<UsuarioDto>(
    `/api/administracion/usuarios/${usuario.id}/${accion}`,
    { version: usuario.version },
  );
  return mapearUsuario(data);
}

function payload(datos: Omit<UsuarioMock, "id" | "is_active">, catalogos: CatalogosUsuarios) {
  return {
    nombre: datos.nombre,
    apellido: datos.apellido,
    documento: datos.documento,
    legajo: datos.legajo || null,
    cuil: datos.cuil || null,
    fechaNacimiento: datos.fecha_nacimiento || null,
    telefono: datos.telefono || null,
    upn: datos.upn,
    version: datos.version,
    roles: datos.roles.map((nombre) => {
      const rol = catalogos.roles.find((item) => item.nombre === nombre);
      if (!rol) throw new Error(`El rol ${nombre} no está disponible.`);
      if (rol.ambito === "materia") {
        const materiaId =
          datos.asignaciones?.find((a) => a.rolId === rol.id)?.materiaId ??
          catalogos.materias[0]?.id;
        const carreraId =
          datos.asignaciones?.find((a) => a.rolId === rol.id)?.carreraId ??
          catalogos.carreras[0]?.id;
        return { rolId: rol.id, materiaId, carreraId };
      }
      if (rol.ambito === "carrera") {
        const carreraId =
          datos.asignaciones?.find((a) => a.rolId === rol.id)?.carreraId ??
          catalogos.carreras[0]?.id;
        return { rolId: rol.id, carreraId };
      }
      return { rolId: rol.id };
    }),
  };
}

function mapearUsuario(dto: UsuarioDto): UsuarioMock {
  return {
    id: dto.id,
    nombre: dto.nombre,
    apellido: dto.apellido,
    documento: dto.documento,
    legajo: dto.legajo ?? "",
    cuil: dto.cuil ?? "",
    fecha_nacimiento: dto.fechaNacimiento ?? "",
    telefono: dto.telefono ?? "",
    upn: dto.upn,
    is_active: dto.activo,
    roles: dto.roles.map((r) => r.nombre as RolSistema),
    version: dto.version,
    asignaciones: dto.roles,
  };
}
