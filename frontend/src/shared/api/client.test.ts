import { describe, expect, it } from "vitest";
import { apiClient } from "./client";

/**
 * Regresión de un bug encontrado a mano, no por un test.
 *
 * El cliente traía un default absoluto al puerto del backend para desarrollo.
 * Toda llamada desde el navegador moría en el preflight de CORS —el Host no
 * declara ninguna política, y no tiene por qué: en los ambientes desplegados
 * Traefik publica la API bajo /api en el mismo host—. La aplicación quedaba
 * clavada en «Cargando sesión…» y ningún test lo veía, porque los tests llaman
 * a axios con un mock y nunca hay un origen del cual salir.
 *
 * Lo que se fija no es el valor sino la propiedad: mismo origen. El día que
 * alguien necesite otro, va a tener que agregarle CORS al backend a propósito.
 */
describe("apiClient", () => {
  it("no apunta a otro origen por default", () => {
    const base = apiClient.defaults.baseURL;

    expect(base === undefined || base.startsWith("/")).toBe(true);
  });

  it("las llamadas salen relativas al origen que sirve la aplicación", () => {
    // Con base relativa (o sin base), `/api/...` resuelve contra el origen del
    // documento: en producción lo sirve Traefik y en desarrollo el proxy de Vite.
    const base = apiClient.defaults.baseURL ?? "";

    expect(base).not.toMatch(/^https?:\/\//);
  });
});
