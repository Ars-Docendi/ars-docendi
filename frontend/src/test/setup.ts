// ============================================================
// Setup global del harness de tests (Vitest + Testing Library).
// - Agrega los matchers de jest-dom a `expect`.
// - Limpia el DOM renderizado después de cada test.
// - Aísla el store mock entre tests: limpia `localStorage` antes de
//   cada test (el reinicio del singleton de pedidos se engancha en la
//   Fase 1, cuando exista `pedidosStore`).
// ============================================================
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach } from "vitest";

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  cleanup();
});
