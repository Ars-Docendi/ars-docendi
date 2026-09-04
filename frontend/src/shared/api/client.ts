import axios from "axios";
import { developmentAuthEnabled } from "../auth/developmentAuth";
import { obtenerSesionDesarrollo } from "../auth/dev/session";

// En desarrollo Vite y la API usan puertos distintos. En bundles desplegados,
// Traefik publica la API bajo /api en el mismo host que el frontend.
const baseURL =
  import.meta.env.VITE_API_URL ?? (import.meta.env.DEV ? "http://localhost:5000" : undefined);

export const apiClient = axios.create({
  baseURL,
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
