/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/ · https://vitest.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Mismo origen que los despliegues: la cookie de sesión y el retorno del
    // ingreso con Microsoft pasan por /api sin CORS. Se conserva el Host para que
    // el backend arme la URL de retorno con el origen de Vite.
    proxy: {
      "/api": { target: "http://localhost:5000" },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
