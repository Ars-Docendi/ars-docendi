import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import { IndexPage } from "./IndexPage";

const api = vi.hoisted(() => ({
  listarMateriasPropias: vi.fn(),
  listarMisSolicitudes: vi.fn(),
  listarTodasLasSolicitudes: vi.fn(),
  crearSolicitud: vi.fn(),
  cancelarSolicitud: vi.fn(),
}));

vi.mock("../api/solicitudesApi", () => api);

const permisos = vi.hoisted(() => ({ actuales: [] as string[] }));
vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: { permissions: permisos.actuales },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));

function renderizar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter>
        <IndexPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  permisos.actuales = ["aulas.solicitar"];
  api.listarMateriasPropias.mockResolvedValue([
    { id: "m1", codigo: "3001", nombre: "Ingeniería de Software" },
  ]);
  api.listarMisSolicitudes.mockResolvedValue([]);
  api.listarTodasLasSolicitudes.mockResolvedValue([]);
});

describe("Nueva solicitud", () => {
  it("opens with an empty form every time, without data typed in a previous opening", async () => {
    const usuario = userEvent.setup();
    renderizar();

    await usuario.click(await screen.findByRole("button", { name: "Nueva solicitud" }));
    await usuario.type(await screen.findByLabelText(/^Comisión/), "K3001");
    expect(screen.getByLabelText(/^Comisión/)).toHaveValue("K3001");

    // Close, discarding the changes when the form asks for confirmation.
    await usuario.keyboard("{Escape}");
    const descartar = screen.queryByRole("button", { name: "Descartar cambios" });
    if (descartar) await usuario.click(descartar);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    await usuario.click(screen.getByRole("button", { name: "Nueva solicitud" }));
    expect(await screen.findByLabelText(/^Comisión/)).toHaveValue("");
  });
});
