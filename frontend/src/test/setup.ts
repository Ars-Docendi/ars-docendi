// ============================================================
// Setup global del harness de tests (Vitest + Testing Library).
// - Agrega los matchers de jest-dom a `expect`.
// - Limpia el DOM renderizado después de cada test.
// - Aísla las preferencias de sesión entre tests: limpia `localStorage` y resetea
//   el singleton de pedidos antes de cada test.
// ============================================================
import "@testing-library/jest-dom/vitest";
import { cleanup, configure } from "@testing-library/react";
import { afterEach, beforeEach } from "vitest";

// Esperar las transiciones de React y las notificaciones de React Query también
// cuando los workers comparten CPU. Una espera agotada sigue fallando el test.
configure({ asyncUtilTimeout: 3_000 });

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  cleanup();
});
