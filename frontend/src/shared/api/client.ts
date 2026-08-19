import axios from "axios";
import { obtenerSesionDesarrollo } from "../auth/dev/session";

const baseURL = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

export const apiClient = axios.create({
  baseURL,
  headers: { "Content-Type": "application/json" },
});

if (import.meta.env.DEV) {
  apiClient.interceptors.request.use((config) => {
    const sesion = obtenerSesionDesarrollo();
    if (sesion) {
      config.headers.set("X-Dev-User-Id", sesion.usuarioId);
      config.headers.set("X-Dev-Role-Code", sesion.rolCodigo);
    }
    return config;
  });
}
