import axios, { AxiosError } from "axios";

/**
 * Lo que se le dice a quien no tiene el permiso, sea al cargar el panel o al
 * enviar. Un solo texto: dos redacciones para la misma situación harían pensar
 * que son dos situaciones.
 */
export const MENSAJE_SIN_ACCESO = "No tenés acceso al asistente con tus permisos actuales.";

/**
 * Traduce un fallo de transporte a algo que una persona pueda leer.
 *
 * El invariante es que NO salga nada crudo: ni el código de estado, ni el nombre de
 * la excepción, ni el cuerpo del error. Lo que el usuario necesita saber es si tiene
 * algo que hacer, y todo lo demás es ruido que además puede filtrar cómo está
 * armado el sistema por dentro.
 */
export function mensajeDeError(error: unknown): string {
  if (!axios.isAxiosError(error)) {
    return "No pude completar la consulta. Probá de nuevo en un momento.";
  }

  const estado = error.response?.status;

  if (estado === 403) {
    return MENSAJE_SIN_ACCESO;
  }

  // Una mención revalidada al enviar y que ya no está disponible —desconocida
  // o fuera del alcance ACTUAL del actor, design.md D11 de asistente-
  // rediseno-v3—: SÍ se distingue del 400 genérico de más abajo, porque acá
  // hay algo concreto que hacer (elegir otra) y no sólo reformular. El chip
  // del composer ya se vació al enviar, así que «volver a elegirla» es
  // escribir una pregunta nueva con una mención nueva.
  if (esMencionNoDisponible(error)) {
    return "Una de las menciones ya no está disponible. Volvé a elegirla.";
  }

  if (esHiloPerdido(error)) {
    return "Se perdió el hilo de la conversación. Volvé a hacer la pregunta.";
  }

  if (estado !== undefined && estado >= 500) {
    return "El asistente tuvo un problema al responder. Probá de nuevo en un momento.";
  }

  // El timeout NO es «sin conexión». La conexión está bien; lo que pasó es que la
  // pregunta no entró en el presupuesto, y lo que el usuario puede hacer es
  // acotarla, no revisar su red.
  if (error.code === AxiosError.ECONNABORTED || error.code === AxiosError.ETIMEDOUT) {
    return "El asistente tardó demasiado en responder. Probá con una pregunta más acotada.";
  }

  if (error.response === undefined) {
    return "No pude comunicarme con el servidor. Revisá tu conexión y volvé a intentar.";
  }

  return "No pude completar la consulta. Probá formulándola de otra manera.";
}

/**
 * El backend ya no reconoce el hilo: lo expiró por inactividad y responde 404.
 *
 * Quien lo detecta tiene que SOLTAR el identificador que guardaba. Decirle al
 * usuario «volvé a hacer la pregunta» y mandar el mismo hilo muerto la vez
 * siguiente es prometer una salida y devolverlo al mismo error.
 */
export function esHiloPerdido(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 404;
}

/**
 * El `400` puntual de `AsistenteController` cuando una referencia —conocida
 * o no— ya no resuelve contra el alcance actual del actor (asistente-
 * menciones). Se detecta por el `title` del `ProblemDetails`, nunca por su
 * `detail`: el texto que se le muestra al usuario lo elige este archivo, no
 * el backend.
 */
export function esMencionNoDisponible(error: unknown): boolean {
  if (!axios.isAxiosError(error) || error.response?.status !== 400) return false;

  const datos: unknown = error.response.data;
  return (
    typeof datos === "object" &&
    datos !== null &&
    "title" in datos &&
    datos.title === "Mención no disponible"
  );
}

/**
 * El request se abortó desde este lado: quien lo emitió se desmontó o dejó de
 * esperar. NO es un error —nadie falló— y no se le muestra al usuario como tal.
 */
export function esCancelacion(error: unknown): boolean {
  return axios.isCancel(error);
}
