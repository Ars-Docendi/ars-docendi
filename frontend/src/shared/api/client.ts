import axios from "axios";
import { developmentAuthEnabled } from "../auth/developmentAuth";
import { obtenerSesionDesarrollo } from "../auth/dev/session";

// Sin base: todas las llamadas ya empiezan con `/api`, así que salen relativas al
// origen que sirve la aplicación. Es lo que hacen los ambientes desplegados, donde
// Traefik publica la API bajo /api en el mismo host que el frontend, y desde el
// proxy de Vite (ver vite.config.ts) también lo es en desarrollo.
//
// Antes acá había un default absoluto al puerto del backend para desarrollo. No
// funcionaba: el Host no declara ninguna política de CORS, así que toda llamada
// desde el navegador moría en el preflight. Un default que solo servía para que
// el navegador la rechazara.
//
// VITE_API_URL sigue existiendo para apuntar a un backend de otro origen a
// propósito; el día que se use, ese backend va a necesitar CORS.
const baseURL = import.meta.env.VITE_API_URL ?? undefined;

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
