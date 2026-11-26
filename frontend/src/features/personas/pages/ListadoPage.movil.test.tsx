import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { cambiarEstadoPersona, listarPersonas, type Persona } from "../api/personasApi";
import { ListadoPage } from "./ListadoPage";

vi.mock("../api/personasApi", async (original) => ({
  ...(await original<typeof import("../api/personasApi")>()),
  listarPersonas: vi.fn(),
  cambiarEstadoPersona: vi.fn(),
}));
vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({ user: { permissions: ["personas.administrar"] } }),
}));

function persona(id: string, apellido: string, rol: string | null): Persona {
  return {
    personaId: id,
    usuarioId: id,
    nombre: "Prueba",
    apellido,
    documento: `1000000${id}`,
    legajo: null,
    cuil: null,
    fechaNacimiento: null,
    telefono: null,
    upn: null,
    tieneCuenta: true,
    activo: true,
    version: 1,
    roles: rol ? [{ id: rol, codigo: rol, nombre: rol }] : [],
    membresias: [],
    asignaciones: [],
  } as unknown as Persona;
}

function Ubicacion() {
  const { search } = useLocation();
  return <output data-testid="url">{search}</output>;
}

function renderPagina() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={["/personas"]}>
        <ListadoPage />
        <Ubicacion />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  // Phone viewport: `(max-width: 559px)` matches.
  vi.stubGlobal("matchMedia", (consulta: string) => ({
    matches: consulta.includes("559px"),
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
  vi.mocked(listarPersonas).mockResolvedValue([
    persona("1", "Alfa", "Docente"),
    persona("2", "Beta", "Docente"),
    persona("3", "Gamma", "Coordinador"),
  ]);
  vi.mocked(cambiarEstadoPersona).mockResolvedValue(undefined as never);
});

afterEach(() => vi.unstubAllGlobals());

describe("Personas en un teléfono", () => {
  it("la hoja de filtros aplica un rol y la lista muestra sólo esas personas", async () => {
    const user = userEvent.setup();
    renderPagina();
    const lista = await screen.findByRole("list", { name: "Personas" });
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(within(lista).getAllByRole("listitem")).toHaveLength(3);

    await user.click(screen.getByRole("button", { name: /Filtros/ }));
    const hoja = await screen.findByRole("dialog", { name: "Filtros" });
    await user.click(within(hoja).getByRole("checkbox", { name: "Docente" }));
    await user.click(within(hoja).getByRole("button", { name: "Ver 2 resultados" }));

    await waitFor(() => expect(within(lista).getAllByRole("listitem")).toHaveLength(2));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(within(lista).queryByText(/Gamma/)).not.toBeInTheDocument();
    expect(screen.getByTestId("url")).toHaveTextContent("rol=Docente");
  });

  it("cerrar la hoja sin aplicar no cambia el filtro", async () => {
    const user = userEvent.setup();
    renderPagina();
    await screen.findByRole("list", { name: "Personas" });
    await user.click(screen.getByRole("button", { name: /Filtros/ }));
    const hoja = await screen.findByRole("dialog", { name: "Filtros" });
    await user.click(within(hoja).getByRole("checkbox", { name: "Docente" }));
    await user.click(within(hoja).getByRole("button", { name: "Cerrar" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByTestId("url")).toBeEmptyDOMElement();
  });

  it("el menú ⋯ de la tarjeta ejecuta la misma acción que la fila de la tabla", async () => {
    const user = userEvent.setup();
    renderPagina();
    const lista = await screen.findByRole("list", { name: "Personas" });
    const tarjeta = within(lista).getAllByRole("listitem")[0];
    await user.click(within(tarjeta).getByRole("button", { name: /^Acciones de/ }));
    await user.click(within(tarjeta).getByRole("menuitem", { name: "Desactivar" }));

    const confirmar = await screen.findByRole("dialog", { name: "Desactivar persona" });
    await user.click(within(confirmar).getByRole("button", { name: "Desactivar" }));
    await waitFor(() =>
      expect(cambiarEstadoPersona).toHaveBeenCalledWith(
        expect.objectContaining({ personaId: "1" }),
        false,
      ),
    );
  });
});
