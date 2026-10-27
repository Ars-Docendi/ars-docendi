import axios, { AxiosError } from "axios";

/** Lo que se le dice a quien no tiene el permiso, sea al cargar el panel o al enviar. */
export const MENSAJE_SIN_ACCESO = "No tenés acceso al asistente con tus permisos actuales.";

/** Traduce un fallo de transporte a algo que una persona pueda leer. */
export function mensajeDeError(error: unknown): string {
  if (!axios.isAxiosError(error)) {
    return "No pude completar la consulta. Probá de nuevo en un momento.";
  }

  const estado = error.response?.status;

  if (estado === 403) {
    return MENSAJE_SIN_ACCESO;
  }

  // Una mención revalidada al enviar y que ya no está disponible —desconocida o fuera del alcance
  // ACTUAL del actor, design.md D11 de asistente- rediseno-v3—: SÍ se distingue del 400 genérico de
  // más abajo, porque acá hay algo concreto que hacer (elegir otra) y no sólo reformular.
  if (esMencionNoDisponible(error)) {
    return "Una de las menciones ya no está disponible. Volvé a elegirla.";
  }

  if (esHiloPerdido(error)) {
    return "Se perdió el hilo de la conversación. Volvé a hacer la pregunta.";
  }

  if (estado !== undefined && estado >= 500) {
    return "El asistente tuvo un problema al responder. Probá de nuevo en un momento.";
  }

  // El timeout NO es «sin conexión». La conexión está bien; lo que pasó es que la pregunta no entró
  // en el presupuesto, y lo que el usuario puede hacer es acotarla, no revisar su red.
  if (error.code === AxiosError.ECONNABORTED || error.code === AxiosError.ETIMEDOUT) {
    return "El asistente tardó demasiado en responder. Probá con una pregunta más acotada.";
  }

  if (error.response === undefined) {
    return "No pude comunicarme con el servidor. Revisá tu conexión y volvé a intentar.";
  }

  return "No pude completar la consulta. Probá formulándola de otra manera.";
}

/** El backend ya no reconoce el hilo: lo expiró por inactividad y responde 404. */
export function esHiloPerdido(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 404;
}

/**
 * El `400` puntual de `AsistenteController` cuando una referencia —conocida o no— ya no resuelve
 * contra el alcance actual del actor (asistente- menciones).
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

/** El request se abortó desde este lado: quien lo emitió se desmontó o dejó de esperar. */
export function esCancelacion(error: unknown): boolean {
  return axios.isCancel(error);
}
