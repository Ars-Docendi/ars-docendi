import { apiClient } from "../../../shared/api/client";
import type { CapacidadesDelAsistente, RespuestaDelAsistente } from "../types";

export interface ConsultaDelAsistente {
  mensaje: string;
  hilo?: string | null;
}

/**
 * Lo máximo que el cliente espera un turno, en milisegundos.
 *
 * El backend acota cada turno a 150 s y, cuando lo agota, responde degradado con su
 * propio mensaje. El margen es para que el que corte sea el servidor con ese
 * mensaje; el cliente es sólo la red de seguridad para un request que se colgó
 * sin respuesta de ningún tipo.
 *
 * Va POR REQUEST y no en el cliente HTTP compartido: el resto de la aplicación no
 * tiene turnos de dos minutos y medio, y un tope global así de largo no protegería
 * a nadie.
 */
export const PRESUPUESTO_DEL_TURNO_MS = 160_000;

export interface OpcionesDeConsulta {
  /** Para soltar el request desde afuera: quien lo emitió se desmontó o dejó de esperar. */
  signal?: AbortSignal;
}

/**
 * Un turno.
 *
 * La `Idempotency-Key` la genera quien llama, POR INTENTO y no por conversación:
 * «Reintentar» sobre un turno que terminó en error reusa la clave y el texto, que
 * es exactamente para lo que existe: si el backend ya había terminado cuando se
 * cortó, devuelve lo que guardó en lugar de cobrarle otra vez al modelo. Generarla
 * una vez por conversación haría que el segundo turno recibiera la respuesta del
 * primero.
 *
 * Reusarla SÓLO cuando el turno terminó. La idempotencia del backend consulta la
 * caché antes de ejecutar y guarda después, sin registrar el turno en curso: la
 * misma clave mientras el original sigue corriendo ejecuta el turno dos veces.
 */
export async function consultar(
  consulta: ConsultaDelAsistente,
  claveDeIdempotencia: string,
  { signal }: OpcionesDeConsulta = {},
): Promise<RespuestaDelAsistente> {
  const { data } = await apiClient.post<RespuestaDelAsistente>(
    "/api/asistente/consultas",
    consulta,
    {
      headers: { "Idempotency-Key": claveDeIdempotencia },
      signal,
      timeout: PRESUPUESTO_DEL_TURNO_MS,
    },
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
