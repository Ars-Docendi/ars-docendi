import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("./developmentAuth", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./developmentAuth")>()),
  developmentAuthEnabled: true,
}));
// Independiente del .env.local de quien corre los tests: acá el botón principal abre el selector.
vi.mock("./sesionMicrosoft", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./sesionMicrosoft")>()),
  microsoftLoginEnabled: false,
}));
vi.mock("./dev/DevLoginModal", () => ({
  DevLoginModal: ({ open }: { open: boolean }) =>
    open ? <div role="dialog">Selector de identidades</div> : null,
}));

import { apiClient } from "../api/client";
import { LoginPage } from "./LoginPage";
import {
  obtenerSesionDesarrollo,
  elegirAsignacionDesarrollo,
  seleccionarSesionDesarrollo,
} from "./dev/session";
import { resolverDevelopmentAuthEnabled } from "./developmentAuth";

describe("autenticación sembrada configurable", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("se habilita en un bundle optimizado sólo mediante opt-in explícito", () => {
    expect(resolverDevelopmentAuthEnabled({ dev: false, configuredValue: "true" })).toBe(true);
    expect(resolverDevelopmentAuthEnabled({ dev: false, configuredValue: "false" })).toBe(false);
    expect(resolverDevelopmentAuthEnabled({ dev: false })).toBe(false);
  });

  it("abre el selector al pulsar ingresar en un bundle optimizado habilitado", async () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter>
          <LoginPage />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Ingresar con Microsoft" }));

    expect(await screen.findByRole("dialog")).toHaveTextContent("Selector de identidades");
  });

  it("conserva la sesión seleccionada cuando el opt-in está habilitado", () => {
    seleccionarSesionDesarrollo("usuario-1");
    elegirAsignacionDesarrollo("asignacion-1");
    expect(obtenerSesionDesarrollo()).toEqual({
      usuarioId: "usuario-1",
      asignacionId: "asignacion-1",
    });
  });

  it("adjunta la identidad seleccionada en las solicitudes", async () => {
    seleccionarSesionDesarrollo("usuario-1");
    elegirAsignacionDesarrollo("asignacion-1");
    const adapter = vi.fn(async (config) => ({
      data: {},
      status: 200,
      statusText: "OK",
      headers: {},
      config,
    }));

    await apiClient.get("/prueba", { adapter });

    const config = adapter.mock.calls[0][0];
    expect(config.headers.get("X-Dev-User-Id")).toBe("usuario-1");
    expect(config.headers.get("X-Dev-Asignacion-Id")).toBe("asignacion-1");
  });
});
