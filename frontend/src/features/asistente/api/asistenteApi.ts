import { apiClient } from "../../../shared/api/client";
import type { CapacidadesDelAsistente, RespuestaDelAsistente } from "../types";

export interface ConsultaDelAsistente {
  mensaje: string;
  hilo?: string | null;
}

/**
 * Un turno.
 *
 * La `Idempotency-Key` la genera quien llama, POR INTENTO y no por conversación:
 * un reintento del mismo envío —doble clic, reintento por timeout— reusa la clave,
 * que es exactamente para lo que existe. Generarla una vez por conversación haría
 * que el segundo turno recibiera la respuesta del primero.
 */
export async function consultar(
  consulta: ConsultaDelAsistente,
  claveDeIdempotencia: string,
): Promise<RespuestaDelAsistente> {
  const { data } = await apiClient.post<RespuestaDelAsistente>(
    "/api/asistente/consultas",
    consulta,
    { headers: { "Idempotency-Key": claveDeIdempotencia } },
  );
  return data;
}

/**
 * Qué puede hacer el asistente para este actor.
 *
 * Sirve para dos cosas a la vez: es la pantalla inicial de la vista y es el gate de
 * acceso. Responde 403 a quien no tiene el permiso, así que preguntarlo es
 * preguntarle al backend por el permiso real en lugar de deducirlo del rol.
 */
export async function obtenerCapacidades(): Promise<CapacidadesDelAsistente> {
  const { data } = await apiClient.get<CapacidadesDelAsistente>("/api/asistente/capacidades");
  return data;
}
