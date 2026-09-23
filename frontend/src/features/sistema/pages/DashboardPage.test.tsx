import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { consultarEstadoSistema } from "../api/sistemaApi";
import type { EstadoSistema } from "../api/sistemaApi";
import { DashboardPage } from "./DashboardPage";

vi.mock("../api/sistemaApi", () => ({ consultarEstadoSistema: vi.fn() }));

const estado: EstadoSistema = {
  modulos: [
    {
      id: "aulas",
      nombre: "Aulas",
      estado: "disponible",
      comprobadoEn: "2026-09-23T18:00:00Z",
      duracionMs: 12,
    },
    {
      id: "tareas",
      nombre: "Tareas",
      estado: "no_disponible",
      comprobadoEn: "2026-09-23T18:00:00Z",
      duracionMs: 31,
    },
    {
      id: "designaciones",
      nombre: "Designaciones",
      estado: "disponible",
      comprobadoEn: "2026-09-23T18:00:00Z",
      duracionMs: 8,
    },
    {
      id: "portal",
      nombre: "Portal",
      estado: "disponible",
      comprobadoEn: "2026-09-23T18:00:00Z",
      duracionMs: 11,
    },
  ],
  baseDatos: {
    id: "postgresql",
    nombre: "PostgreSQL",
    estado: "disponible",
    comprobadoEn: "2026-09-23T18:00:00Z",
    duracionMs: 4,
  },
};

function renderPagina() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={cliente}>
      <DashboardPage />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(consultarEstadoSistema).mockReset().mockResolvedValue(estado);
});

describe("Dashboard del sistema", () => {
  it("presenta cada módulo API y PostgreSQL con estado y latencia", async () => {
    renderPagina();

    const tareas = await screen.findByRole("row", { name: /Tareas/ });
    expect(within(tareas).getByText("No disponible")).toBeInTheDocument();
    expect(within(tareas).getByText("31 ms")).toBeInTheDocument();
    expect(
      within(await screen.findByRole("row", { name: /PostgreSQL/ })).getByText("Disponible"),
    ).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(6);
  });

  it("representa estados y horas desconocidas sin inventar disponibilidad", async () => {
    vi.mocked(consultarEstadoSistema).mockResolvedValue({
      ...estado,
      baseDatos: {
        ...estado.baseDatos,
        estado: "desconocido" as never,
        comprobadoEn: "fecha inválida",
        duracionMs: Number.NaN,
      },
    });
    renderPagina();

    const postgres = await screen.findByRole("row", { name: /PostgreSQL/ });
    expect(within(postgres).getByText("Desconocido")).toBeInTheDocument();
    expect(within(postgres).getByText("Hora no disponible")).toBeInTheDocument();
    expect(within(postgres).getByText("—")).toBeInTheDocument();
  });

  it("permite recuperar una consulta fallida sin exponer el error", async () => {
    const user = userEvent.setup();
    vi.mocked(consultarEstadoSistema)
      .mockRejectedValueOnce(new Error("detalle de conexión reservado"))
      .mockResolvedValueOnce(estado);
    renderPagina();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "No se pudo cargar el estado del sistema",
    );
    expect(screen.queryByText("detalle de conexión reservado")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Reintentar" }));
    expect(await screen.findByRole("row", { name: /PostgreSQL/ })).toBeInTheDocument();
  });

  it("permite volver a ejecutar las sondas", async () => {
    const user = userEvent.setup();
    renderPagina();
    await screen.findByRole("row", { name: /PostgreSQL/ });
    await user.click(screen.getByRole("button", { name: "Actualizar" }));
    await waitFor(() => expect(consultarEstadoSistema).toHaveBeenCalledTimes(2));
  });
});
