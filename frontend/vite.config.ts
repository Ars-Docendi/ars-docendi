/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/ · https://vitest.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // El navegador habla SIEMPRE con el mismo origen, también en desarrollo.
    //
    // Sin esto, el cliente apuntaba directo al puerto del backend y toda llamada
    // desde el navegador moría en CORS: el Host no declara ninguna política de
    // CORS —y no tiene por qué, porque en los ambientes desplegados Traefik
    // publica la API bajo /api en el mismo host que el frontend—. El proxy hace
    // que desarrollo se parezca a producción en vez de necesitar una excepción
    // que producción no tiene.
    //
    // El destino se puede mover con VITE_API_PROXY_TARGET para apuntar a un
    // backend en otro puerto sin tocar este archivo.
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY_TARGET ?? "http://localhost:5000",
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
