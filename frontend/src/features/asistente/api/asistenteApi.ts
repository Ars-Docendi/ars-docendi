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
   * El identificador del turno que este turno reemplaza —su propio `id` anterior, la misma
   * `Idempotency-Key` con la que se mandó, o el `turno_historico.id` de un turno restaurado por
   * «Reanudar»—, para «Editar y reenviar» (asistente-edicion-de-la-ultima-pregunta, design.md D9
   * de asistente-rediseno-v3).
   */
  reemplaza?: string;
  /**
   * Las «@materia»/«#docente» elegidas en el composer (asistente-menciones, design.md D10/D11 de
   * asistente-rediseno-v3), a lo sumo 5.
   */
  referencias?: ReferenciaDeMencion[];
}

/** Lo máximo que el cliente espera un turno, en milisegundos. */
export const PRESUPUESTO_DEL_TURNO_MS = 160_000;

export interface OpcionesDeConsulta {
  /** Para soltar el request desde afuera: quien lo emitió se desmontó o dejó de esperar. */
  signal?: AbortSignal;
}

/**
 * Un turno. La `Idempotency-Key` la genera quien llama, POR INTENTO y no por conversación:
 * «Reintentar» sobre un turno que terminó en error reusa la clave y el texto, que es exactamente
 * para lo que existe: si el backend ya había terminado cuando se cortó, devuelve lo que guardó en
 * lugar de cobrarle otra vez al modelo.
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
 * Lee los eventos COMPLETOS de un `text/event-stream`: el último bloque, si todavía no terminó en
 * línea en blanco, se deja para la próxima lectura.
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
 * Un turno por `POST /consultas/flujo`: el mismo pedido, la misma clave y la misma respuesta que
 * {@link consultar}, con la redacción llegando por partes mientras se escribe
 * (asistente-optimizaciones-modelo-local, D9).
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

/** Qué puede hacer el asistente para este actor. */
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
 * Rates an already-answered turn: thumbs up/down, with zero or more reasons and an optional
 * free-text comment on a thumbs-down.
 */
export async function enviarRetroalimentacion(pedido: PedidoDeRetroalimentacion): Promise<void> {
  await apiClient.post("/api/asistente/retroalimentacion", pedido);
}
