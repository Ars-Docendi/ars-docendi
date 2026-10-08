import { AxiosError, type AxiosResponse } from "axios";

import { apiClient } from "../../../shared/api/client";
import type {
  CapacidadesDelAsistente,
  RazonDeRetroalimentacion,
  ReferenciaDeMencion,
  RespuestaDelAsistente,
} from "../types";

export interface ConsultaDelAsistente {
  mensaje: string;
  hilo?: string | null;
  /**
   * El identificador del turno que este turno reemplaza —su propio `id`
   * anterior, la misma `Idempotency-Key` con la que se mandó, o el
   * `turno_historico.id` de un turno restaurado por «Reanudar»—, para
   * «Editar y reenviar» (asistente-edicion-de-la-ultima-pregunta,
   * design.md D9 de asistente-rediseno-v3). Ausente en un turno nuevo
   * cualquiera.
   */
  reemplaza?: string;
  /**
   * Las «@materia»/«#docente» elegidas en el composer (asistente-menciones,
   * design.md D10/D11 de asistente-rediseno-v3), a lo sumo 5. El controller
   * las revalida contra el alcance ACTUAL del actor antes del candado: una
   * desconocida o fuera de alcance da `400`, sin decir cuál de las dos fue.
   */
  referencias?: ReferenciaDeMencion[];
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

export interface OpcionesDeConsultaEnFlujo extends OpcionesDeConsulta {
  /** Recibe la redacción acumulada hasta ahora, cada vez que crece. */
  alRecibirRedaccion: (parcial: string) => void;
}

/** Lo que trae, hasta ahora, una respuesta `text/event-stream` del turno. */
export interface EventosDelFlujo {
  /** Los fragmentos de `redaccion` concatenados. */
  redaccion: string;
  /** El `data` crudo del evento `resultado`, si ya llegó. */
  resultado?: string;
  /** El status del evento `error`, si llegó. */
  errorStatus?: number;
}

/**
 * Lee los eventos COMPLETOS de un `text/event-stream`: el último bloque, si
 * todavía no terminó en línea en blanco, se deja para la próxima lectura.
 * Sólo parsea los fragmentos; el resultado se devuelve crudo para no parsear
 * las filas en cada progreso.
 */
export function leerEventosDelFlujo(texto: string): EventosDelFlujo {
  const eventos: EventosDelFlujo = { redaccion: "" };
  const bloques = texto.split("\n\n");
  // El último es lo que vino después del último separador: incompleto o vacío.
  bloques.pop();

  for (const bloque of bloques) {
    let nombre = "message";
    const datos: string[] = [];
    for (const linea of bloque.split("\n")) {
      if (linea.startsWith("event:")) nombre = linea.slice(6).trim();
      else if (linea.startsWith("data:")) datos.push(linea.slice(5).trimStart());
    }
    const dato = datos.join("\n");

    if (nombre === "redaccion") {
      const { texto: fragmento } = JSON.parse(dato) as { texto: string };
      eventos.redaccion += fragmento;
    } else if (nombre === "resultado") {
      eventos.resultado = dato;
    } else if (nombre === "error") {
      eventos.errorStatus = (JSON.parse(dato) as { status: number }).status;
    }
  }

  return eventos;
}

/**
 * Un turno por `POST /consultas/flujo`: el mismo pedido, la misma clave y la
 * misma respuesta que {@link consultar}, con la redacción llegando por partes
 * mientras se escribe (asistente-optimizaciones-modelo-local, D9).
 *
 * VA POR AXIOS Y NO POR `fetch` a propósito: así comparte cabeceras de sesión,
 * cancelación, timeout y la forma de los errores con el resto del turno, y
 * `mensajeDeError` no necesita un segundo vocabulario. El adaptador es XHR
 * porque es el que expone el texto parcial en cada progreso; axios los limita a
 * unos tres por segundo, que alcanza para leer y es lo que se pinta.
 *
 * Un rechazo antes de empezar (400, 404, 409) llega como cualquier error HTTP;
 * un `error` dentro del flujo se convierte en uno con ese status.
 */
export async function consultarEnFlujo(
  consulta: ConsultaDelAsistente,
  claveDeIdempotencia: string,
  { signal, alRecibirRedaccion }: OpcionesDeConsultaEnFlujo,
): Promise<RespuestaDelAsistente> {
  let mostrada = 0;

  const respuesta = await apiClient.post<string>("/api/asistente/consultas/flujo", consulta, {
    headers: { "Idempotency-Key": claveDeIdempotencia, Accept: "text/event-stream" },
    signal,
    timeout: PRESUPUESTO_DEL_TURNO_MS,
    adapter: "xhr",
    responseType: "text",
    // Un rechazo trae `ProblemDetails` en JSON y el resto de la feature lo lee
    // como objeto (`esMencionNoDisponible`); el flujo se deja como texto.
    transformResponse: [
      (datos: unknown, cabeceras) => {
        const tipo = String(cabeceras?.["content-type"] ?? "");
        if (typeof datos !== "string" || !tipo.includes("json")) return datos;
        try {
          return JSON.parse(datos) as unknown;
        } catch {
          return datos;
        }
      },
    ],
    onDownloadProgress: (progreso) => {
      const xhr = (progreso.event as ProgressEvent | undefined)?.target as
        XMLHttpRequest | undefined;
      if (!xhr?.responseText) return;
      const { redaccion } = leerEventosDelFlujo(xhr.responseText);
      if (redaccion.length > mostrada) {
        mostrada = redaccion.length;
        alRecibirRedaccion(redaccion);
      }
    },
  });

  const eventos = leerEventosDelFlujo(respuesta.data);
  if (eventos.resultado !== undefined) {
    return JSON.parse(eventos.resultado) as RespuestaDelAsistente;
  }

  // Sin resultado: o el servidor avisó qué pasó, o el flujo se cortó.
  throw new AxiosError(
    "El turno no terminó.",
    eventos.errorStatus === undefined ? AxiosError.ERR_NETWORK : AxiosError.ERR_BAD_RESPONSE,
    respuesta.config,
    respuesta.request,
    eventos.errorStatus === undefined
      ? undefined
      : ({
          data: null,
          status: eventos.errorStatus,
          statusText: "",
          headers: {},
          config: respuesta.config,
        } as AxiosResponse),
  );
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

export interface PedidoDeRetroalimentacion {
  token: string;
  voto: boolean;
  razones?: RazonDeRetroalimentacion[];
  comentario?: string;
}

/**
 * Rates an already-answered turn: thumbs up/down, with zero or more reasons and an
 * optional free-text comment on a thumbs-down.
 *
 * `token` is the turn's own `claveDeRetroalimentacion` — it authorizes rating
 * THAT turn, and carries no identity of its own. `comentario` is never logged,
 * never sent to the model, and has no read surface anywhere in the UI.
 */
export async function enviarRetroalimentacion(pedido: PedidoDeRetroalimentacion): Promise<void> {
  await apiClient.post("/api/asistente/retroalimentacion", pedido);
}
