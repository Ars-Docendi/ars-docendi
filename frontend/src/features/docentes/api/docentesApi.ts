import { apiClient } from "../../../shared/api/client";
import type { DocenteMock, RolDocente } from "../models";

interface DocenteDto {
  personaId: string;
  nombre: string;
  apellido: string;
  documento: string;
  legajo: string | null;
  cuil: string | null;
  fechaNacimiento: string | null;
  telefono: string | null;
  upn: string | null;
  activo: boolean;
  version: number | null;
  roles: string[];
  asignaciones: {
    id: string;
    materiaId: string;
    materiaCodigo: string;
    materiaNombre: string;
    cargoId: string;
    cargoNombre: string;
    cargoAbreviatura: string;
    dedicacion: string | null;
    horas: number;
  }[];
}
export interface CatalogosDocentes {
  roles: { id: string; codigo: string; nombre: string }[];
  materias: { id: string; codigo: string; nombre: string }[];
  cargos: { id: string; codigo: string; nombre: string; abreviatura: string }[];
  personasElegibles: {
    id: string;
    nombre: string;
    apellido: string;
    documento: string;
    legajo: string | null;
    cuil: string | null;
    fechaNacimiento: string | null;
    telefono: string | null;
    upn: string | null;
    version: number | null;
  }[];
}
export async function listarDocentes(): Promise<DocenteMock[]> {
  return (await apiClient.get<DocenteDto[]>("/api/administracion/docentes")).data.map(mapear);
}
export async function obtenerCatalogosDocentes(): Promise<CatalogosDocentes> {
  return (await apiClient.get<CatalogosDocentes>("/api/administracion/docentes/catalogos")).data;
}
export async function crearDocente(
  datos: Omit<DocenteMock, "id" | "is_active">,
  catalogos: CatalogosDocentes,
) {
  return mapear(
    (await apiClient.post<DocenteDto>("/api/administracion/docentes", payload(datos, catalogos)))
      .data,
  );
}
export async function editarDocente(
  docente: DocenteMock,
  datos: Omit<DocenteMock, "id" | "is_active">,
  catalogos: CatalogosDocentes,
) {
  return mapear(
    (
      await apiClient.put<DocenteDto>(
        `/api/administracion/docentes/${docente.id}`,
        payload({ ...datos, version: docente.version, persona_id: docente.id }, catalogos),
      )
    ).data,
  );
}
export async function cambiarEstadoDocente(docente: DocenteMock, activo: boolean) {
  const accion = activo ? "activar" : "desactivar";
  return mapear(
    (
      await apiClient.post<DocenteDto>(`/api/administracion/docentes/${docente.id}/${accion}`, {
        version: docente.version,
      })
    ).data,
  );
}
function payload(datos: Omit<DocenteMock, "id" | "is_active">, catalogos: CatalogosDocentes) {
  return {
    personaId: datos.persona_id ?? null,
    nombre: datos.nombre,
    apellido: datos.apellido,
    documento: datos.documento,
    legajo: datos.legajo || null,
    cuil: datos.cuil || null,
    fechaNacimiento: datos.fecha_nacimiento || null,
    telefono: datos.telefono || null,
    upn: datos.upn,
    roles: datos.roles.map((nombre) => {
      const rol = catalogos.roles.find((item) => item.nombre === nombre);
      if (!rol) throw new Error(`El rol ${nombre} no está disponible.`);
      return rol.codigo;
    }),
    designaciones: datos.asignaciones.map((a) => ({
      materiaId: catalogos.materias.find((m) => m.codigo === a.materia.codigo)?.id,
      cargoId: catalogos.cargos.find((c) => c.nombre === a.cargo)?.id,
      dedicacion: a.dedicacion ?? null,
      horas: a.horas,
    })),
    version: datos.version,
  };
}
function mapear(dto: DocenteDto): DocenteMock {
  return {
    id: dto.personaId,
    persona_id: dto.personaId,
    nombre: dto.nombre,
    apellido: dto.apellido,
    documento: dto.documento,
    legajo: dto.legajo ?? "",
    cuil: dto.cuil ?? "",
    fecha_nacimiento: dto.fechaNacimiento ?? "",
    telefono: dto.telefono ?? "",
    upn: dto.upn ?? "",
    is_active: dto.activo,
    version: dto.version ?? undefined,
    roles: dto.roles.map((r) =>
      r === "jefe_catedra" ? "Jefe de Cátedra" : "Docente",
    ) as RolDocente[],
    asignaciones: dto.asignaciones.map((a) => ({
      id: a.id,
      materia: { id: a.materiaId, codigo: a.materiaCodigo, nombre: a.materiaNombre },
      cargo: a.cargoNombre,
      cargoId: a.cargoId,
      cargoAbreviatura: a.cargoAbreviatura,
      dedicacion: a.dedicacion,
      horas: a.horas,
    })),
  };
}
