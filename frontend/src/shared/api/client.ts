import axios from "axios";
import { developmentAuthEnabled } from "../auth/developmentAuth";
import { obtenerSesionDesarrollo } from "../auth/dev/session";

// La API vive bajo /api del mismo host que el frontend: en los despliegues lo
// publica Traefik y en local lo proxea Vite. VITE_API_URL sólo hace falta para
// apuntar a otra API a propósito.
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || undefined,
  headers: { "Content-Type": "application/json" },
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

const manejadoresNoAutorizado = new Set<(url: string | undefined) => void>();

/** Avisa cuando la API responde 401; devuelve la función para dejar de escuchar. */
export function alRecibirNoAutorizado(manejador: (url: string | undefined) => void): () => void {
  manejadoresNoAutorizado.add(manejador);
  return () => manejadoresNoAutorizado.delete(manejador);
}

apiClient.interceptors.response.use(undefined, (error) => {
  if (axios.isAxiosError(error) && error.response?.status === 401) {
    manejadoresNoAutorizado.forEach((manejador) => manejador(error.config?.url));
  }
  return Promise.reject(error);
});
