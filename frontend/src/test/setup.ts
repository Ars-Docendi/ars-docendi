// ============================================================
// Setup global del harness de tests (Vitest + Testing Library).
// - Agrega los matchers de jest-dom a `expect`.
// - Limpia el DOM renderizado después de cada test.
// - Aísla el store mock entre tests: limpia `localStorage` y resetea
//   el singleton de pedidos antes de cada test.
// ============================================================
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach } from "vitest";
import { reiniciarStorePedidos } from "../features/designaciones/api/pedidosStore";

beforeEach(() => {
  localStorage.clear();
  reiniciarStorePedidos();
});

afterEach(() => {
  cleanup();
});
