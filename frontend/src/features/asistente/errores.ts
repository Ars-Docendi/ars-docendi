import axios from "axios";

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
    return "No tenés acceso al asistente con tus permisos actuales.";
  }

  if (estado === 404) {
    return "Se perdió el hilo de la conversación. Volvé a hacer la pregunta.";
  }

  if (estado !== undefined && estado >= 500) {
    return "El asistente tuvo un problema al responder. Probá de nuevo en un momento.";
  }

  if (error.code === "ECONNABORTED" || error.response === undefined) {
    return "No pude comunicarme con el servidor. Revisá tu conexión y volvé a intentar.";
  }

  return "No pude completar la consulta. Probá formulándola de otra manera.";
}
