import axios, { type AxiosRequestConfig } from "axios";
import { developmentAuthEnabled } from "../auth/developmentAuth";
import { obtenerSesionDesarrollo } from "../auth/dev/session";

// La API vive bajo /api del mismo host que el frontend: en los despliegues lo
// publica Traefik y en local lo proxea Vite. VITE_API_URL sólo hace falta para
// apuntar a otra API a propósito.
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || undefined,
  headers: { "Content-Type": "application/json" },
  // El token se recibe por header; nunca se toma de document.cookie.
  xsrfCookieName: "",
});

if (developmentAuthEnabled) {
  apiClient.interceptors.request.use((config) => {
    const sesion = obtenerSesionDesarrollo();
    if (sesion) {
      config.headers.set("X-Dev-User-Id", sesion.usuarioId);
      config.headers.set("X-Dev-Role-Code", sesion.rolCodigo);
    }
    return config;
  });
}

let tokenAntifalsificacion: string | undefined;
let generacionSesion = 0;
const generacionesSolicitud = new WeakMap<AxiosRequestConfig, number>();

function olvidarTokenAntifalsificacion(): void {
  tokenAntifalsificacion = undefined;
  generacionSesion++;
}

function destinoMismoOrigen(config: AxiosRequestConfig): URL | undefined {
  try {
    const destino = new URL(apiClient.getUri(config), window.location.href);
    return destino.origin === window.location.origin ? destino : undefined;
  } catch {
    return undefined;
  }
}

apiClient.interceptors.request.use((config) => {
  generacionesSolicitud.set(config, generacionSesion);
  // Incluso un override por solicitud no debe reactivar la lectura de cookies.
  config.xsrfCookieName = "";
  config.withXSRFToken = false;
  config.headers.delete("X-XSRF-TOKEN");
  if (
    destinoMismoOrigen(config) &&
    tokenAntifalsificacion &&
    ["post", "put", "patch", "delete"].includes(config.method ?? "get")
  ) {
    config.headers.set("X-XSRF-TOKEN", tokenAntifalsificacion);
  }
  return config;
});

apiClient.interceptors.response.use((respuesta) => {
  const destino = destinoMismoOrigen(respuesta.config);
  if (destino?.pathname === "/api/auth/logout" && respuesta.config.method === "post") {
    olvidarTokenAntifalsificacion();
  } else if (destino && generacionesSolicitud.get(respuesta.config) === generacionSesion) {
    const token =
      respuesta.headers instanceof axios.AxiosHeaders
        ? respuesta.headers.get("X-XSRF-TOKEN")
        : respuesta.headers["x-xsrf-token"];
    if (typeof token === "string") tokenAntifalsificacion = token;
  }
  return respuesta;
});

const manejadoresNoAutorizado = new Set<(url: string | undefined) => void>();

/** Avisa cuando la API responde 401; devuelve la función para dejar de escuchar. */
export function alRecibirNoAutorizado(manejador: (url: string | undefined) => void): () => void {
  manejadoresNoAutorizado.add(manejador);
  return () => manejadoresNoAutorizado.delete(manejador);
}

apiClient.interceptors.response.use(undefined, (error) => {
  if (axios.isAxiosError(error) && error.response?.status === 401) {
    if (error.config && destinoMismoOrigen(error.config)) olvidarTokenAntifalsificacion();
    manejadoresNoAutorizado.forEach((manejador) => manejador(error.config?.url));
  }
  return Promise.reject(error);
});
