import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { useCurrentUser } from "../../shared/auth/useCurrentUser";
import type { CurrentUserState } from "../../shared/auth/useCurrentUser";
import { crearRutas } from "./routes";
import * as sistemaApi from "./api/sistemaApi";
import * as auditoriaApi from "./api/auditoriaApi";

vi.mock("../../shared/auth/useCurrentUser", () => ({ useCurrentUser: vi.fn() }));

function PanelAsistenteDePrueba() {
  return <p>Panel de asistente</p>;
}

const usuario: CurrentUserState = {
  user: {
    name: "Administración",
    initials: "AD",
    upn: "admin@example.test",
    role: "Administrador de Sistemas",
    roleCode: "sys_admin",
    permissions: [],
  },
  isLoading: false,
  error: null,
  retry: vi.fn(),
};

function conPermisos(permissions: string[]) {
  vi.mocked(useCurrentUser).mockReturnValue({
    ...usuario,
    user: { ...usuario.user!, permissions },
  });
}

function montar(entrada: string) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  const router = createMemoryRouter(
    [
      { path: "/", element: <p>Inicio</p> },
      ...crearRutas({ PanelAsistente: PanelAsistenteDePrueba }),
    ],
    { initialEntries: [entrada] },
  );
  return render(
    <QueryClientProvider client={cliente}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.spyOn(sistemaApi, "comprobarPing").mockResolvedValue({
    id: "aulas",
    nombre: "Aulas",
    disponible: true,
    duracionMs: 10,
    comprobadoEn: new Date().toISOString(),
  });
  vi.spyOn(sistemaApi, "consultarEstadoSistema").mockResolvedValue({
    estado: "disponible",
    comprobadoEn: new Date().toISOString(),
    duracionMs: 5,
    mantenimientoAsistente: "inactivo",
  });
  vi.spyOn(auditoriaApi, "listarAuditoria").mockResolvedValue({
    elementos: [],
    pagina: 1,
    tamanoPagina: 50,
    total: 0,
    parcial: false,
    fuentesNoDisponibles: [],
  });
});

afterEach(() => vi.restoreAllMocks());

describe("/sistema — cualquiera de los tres permisos alcanza", () => {
  it.each([["sistema.estado.ver"], ["asistente.administrar"], ["auditoria.ver"]])(
    "renderiza la sección con sólo %s",
    async (permiso) => {
      conPermisos([permiso]);

      montar("/sistema");

      expect(await screen.findByRole("heading", { name: "Sistema" })).toBeInTheDocument();
    },
  );

  it("sin ninguno de los tres, redirige a /", async () => {
    conPermisos([]);

    montar("/sistema");

    expect(await screen.findByText("Inicio")).toBeInTheDocument();
  });
});

describe("/auditoria redirige a /sistema#auditoria", () => {
  it("con permiso, termina mostrando la sección", async () => {
    conPermisos(["auditoria.ver"]);

    montar("/auditoria");

    expect(await screen.findByRole("heading", { name: "Sistema" })).toBeInTheDocument();
  });
});
