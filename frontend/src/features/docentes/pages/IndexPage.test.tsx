import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

import { IndexPage } from "./IndexPage";

const sesion = vi.hoisted(() => ({ rol: "Jefe de Cátedra" }));

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: {
      name: "Torres, Julián",
      initials: "TJ",
      upn: "jtorres@unlam.edu.ar",
      role: sesion.rol,
      roleCode: "rol",
      permissions: ["docentes.ver"],
    },
    isLoading: false,
    error: null,
    retry: () => {},
  }),
}));

vi.mock("../hooks/useDocentes", () => {
  const mutacion = { mutate: vi.fn(), isPending: false, isError: false, error: null };
  return {
    useDocentes: () => ({
      consulta: { data: [], isLoading: false, isError: false, refetch: vi.fn() },
      catalogos: { data: undefined, isLoading: false, isError: false },
      crear: mutacion,
      editar: mutacion,
      cambiarEstado: mutacion,
    }),
  };
});

function renderDocentes() {
  render(
    <MemoryRouter>
      <IndexPage />
    </MemoryRouter>,
  );
}

describe("Docentes — encabezado", () => {
  it("el Jefe de Cátedra ve Mis docentes en el título y el breadcrumb", () => {
    sesion.rol = "Jefe de Cátedra";
    renderDocentes();

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(/^Mis docentes$/);
    expect(screen.getByRole("navigation", { name: /breadcrumb|migas|ruta/i })).toHaveTextContent(
      "Mis docentes",
    );
  });

  it("el resto de los roles ve Administración de docentes", () => {
    sesion.rol = "Secretaría";
    renderDocentes();

    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
      /^Administración de docentes$/,
    );
  });
});
