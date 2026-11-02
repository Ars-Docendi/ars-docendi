import { apiClient } from "../../../shared/api/client";
import type { Aula, DatosAula } from "../types";

interface AulaDto extends Omit<Aula, "equipamiento"> {
  equipamiento: string | null;
}

const mapear = (dto: AulaDto): Aula => ({ ...dto, equipamiento: dto.equipamiento ?? undefined });

/** Catálogo de aulas. `activas` acota a las que se pueden asignar. */
export async function listarAulas(activas = false): Promise<Aula[]> {
  return (
    await apiClient.get<AulaDto[]>("/api/aulas/catalogo", { params: activas ? { activas } : {} })
  ).data.map(mapear);
}

export async function crearAula(datos: DatosAula): Promise<Aula> {
  return mapear((await apiClient.post<AulaDto>("/api/aulas/catalogo", datos)).data);
}

export async function editarAula(id: string, datos: DatosAula): Promise<Aula> {
  return mapear((await apiClient.put<AulaDto>(`/api/aulas/catalogo/${id}`, datos)).data);
}

export async function cambiarEstadoAula(aula: Aula, activa: boolean): Promise<Aula> {
  const accion = activa ? "activar" : "desactivar";
  return mapear(
    (
      await apiClient.post<AulaDto>(`/api/aulas/catalogo/${aula.id}/${accion}`, {
        version: aula.version,
      })
    ).data,
  );
}

export async function eliminarAula(id: string): Promise<void> {
  await apiClient.delete(`/api/aulas/catalogo/${id}`);
}
