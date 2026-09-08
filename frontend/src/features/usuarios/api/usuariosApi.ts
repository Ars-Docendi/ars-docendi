import { apiClient } from "../../../shared/api/client";
import type { RolCatalogoUsuario, RolSistema, UsuarioFormulario, UsuarioMock } from "../models";

interface AsignacionDto {
  id: string;
  rolId: string;
  codigo: string;
  nombre: string;
  ambito: string;
  materiaId: string | null;
  carreraId: string | null;
}
interface UsuarioDto {
  id: string;
  personaId: string;
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
  roles: { id: string; codigo: string; nombre: string }[];
  membresias: AsignacionDto[];
  perfilDocente: { esDocente: boolean; cantidadMaterias: number };
}
export interface CatalogosUsuarios {
  roles: RolCatalogoUsuario[];
  carreras: { id: string; codigo: string; nombre: string }[];
  materias: { id: string; codigo: string; nombre: string; carreraId?: string | null }[];
}

export async function listarUsuarios(): Promise<UsuarioMock[]> {
  const { data } = await apiClient.get<UsuarioDto[]>("/api/administracion/usuarios");
  return data.map(mapearUsuario);
}
export async function obtenerCatalogosUsuarios(): Promise<CatalogosUsuarios> {
  return (await apiClient.get<CatalogosUsuarios>("/api/administracion/catalogos")).data;
}
export async function crearUsuario(datos: UsuarioFormulario): Promise<UsuarioMock> {
  const { data } = await apiClient.post<UsuarioDto>("/api/administracion/usuarios", payload(datos));
  return mapearUsuario(data);
}
export async function editarUsuario(id: string, datos: UsuarioFormulario): Promise<UsuarioMock> {
  const { data } = await apiClient.put<UsuarioDto>(
    `/api/administracion/usuarios/${id}`,
    payload(datos),
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

function payload(datos: UsuarioFormulario) {
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
    membresias: datos.membresias,
  };
}

function mapearUsuario(dto: UsuarioDto): UsuarioMock {
  return {
    id: dto.id,
    persona_id: dto.personaId,
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
    membresias: dto.membresias,
    perfilDocente: dto.perfilDocente,
  };
}
