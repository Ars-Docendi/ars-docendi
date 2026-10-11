import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

import { PeriodosPage } from "./PeriodosPage";
import { TableroRevisionPage } from "./TableroRevisionPage";

vi.mock("../hooks/usePeriodos", () => {
  const mutacion = { mutate: vi.fn(), isPending: false, isError: false };
  return {
    usePeriodos: () => ({
      consulta: { data: [], isLoading: false, isError: false, refetch: vi.fn() },
      crear: mutacion,
      editar: mutacion,
      eliminar: mutacion,
    }),
  };
});

vi.mock("../hooks/useActorContexto", () => ({
  useActorContexto: () => ({
    rol: "Coordinador",
    nombre: "M. Díaz",
    carrera: "Ingeniería en Informática",
  }),
}));

vi.mock("../hooks/usePedidos", () => ({
  usePedidosPorAmbito: () => ({
    data: undefined,
    isLoading: true,
    isError: false,
    refetch: vi.fn(),
  }),
}));

vi.mock("../hooks/useCatalogosDesignaciones", () => ({
  useCatalogosDesignaciones: () => ({ data: undefined, refetch: vi.fn() }),
}));

function textoBreadcrumb() {
  return screen.getByRole("navigation", { name: /breadcrumb|migas|ruta/i }).textContent;
}

describe("encabezados de Designaciones", () => {
  it("Períodos no agrega nivel de sección y se titula como el sidebar", () => {
    render(
      <MemoryRouter>
        <PeriodosPage />
      </MemoryRouter>,
    );

    expect(textoBreadcrumb()).toMatch(/^Inicio.*Períodos$/);
    expect(textoBreadcrumb()).not.toContain("Designaciones");
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(/^Períodos$/);
    expect(screen.queryByText("Configuración")).not.toBeInTheDocument();
  });

  it("Revisión se titula como el sidebar y no repite el rol en el meta", () => {
    render(
      <MemoryRouter>
        <TableroRevisionPage />
      </MemoryRouter>,
    );

    expect(textoBreadcrumb()).toMatch(/^Inicio.*Revisión$/);
    expect(textoBreadcrumb()).not.toContain("Designaciones");
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(/^Revisión$/);
    expect(screen.queryByText(/Coordinador/)).not.toBeInTheDocument();
  });
});
