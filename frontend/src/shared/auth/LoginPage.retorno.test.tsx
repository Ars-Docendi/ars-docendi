import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";

import { LoginPage } from "./LoginPage";

vi.mock("./developmentAuth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./developmentAuth")>()),
  developmentAuthEnabled: false,
}));
vi.mock("./sesionMicrosoft", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./sesionMicrosoft")>()),
  microsoftLoginEnabled: true,
  obtenerSesion: vi.fn().mockResolvedValue(null),
}));

afterEach(() => vi.unstubAllGlobals());

function renderizar(from: unknown) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[{ pathname: "/login", state: { from } }]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function ingresar() {
  const asignar = vi.fn();
  vi.stubGlobal("location", { ...window.location, assign: asignar });
  await userEvent.click(await screen.findByRole("button", { name: "Ingresar con Microsoft" }));
  return decodeURIComponent(new URL(asignar.mock.calls[0][0], "http://localhost").search);
}

describe("retorno después de ingresar", () => {
  it("conserva ruta, query y hash de la página que se pidió", async () => {
    renderizar({ pathname: "/tareas", search: "?estado=todas&t.titulo=luz", hash: "#fila-3" });
    expect(await ingresar()).toBe("?returnUrl=/tareas?estado=todas&t.titulo=luz#fila-3");
  });

  it.each([
    ["una ruta relativa al protocolo", "//malo.example", "", ""],
    ["una barra invertida", "/\\malo.example", "", ""],
    ["una URL absoluta", "https://malo.example/x", "", ""],
  ])("descarta %s y vuelve al portal", async (_nombre, pathname, search, hash) => {
    renderizar({ pathname, search, hash });
    expect(await ingresar()).toBe("?returnUrl=/portal");
  });
});
