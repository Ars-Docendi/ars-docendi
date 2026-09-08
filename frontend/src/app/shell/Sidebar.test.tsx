import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import { Sidebar } from "./Sidebar";
import type { Role } from "../../shared/auth/useCurrentUser";

// El Sidebar solo depende del router (NavLink/useLocation) y del rol activo.
function renderSidebar(
  role: Role,
  { collapsed = false, route = "/designaciones" }: { collapsed?: boolean; route?: string } = {},
) {
  render(
    <MemoryRouter initialEntries={[route]}>
      <Sidebar collapsed={collapsed} role={role} />
    </MemoryRouter>,
  );
}

describe("Sidebar — sector Designaciones", () => {
  it.each([
    ["Jefe de Cátedra", ["Mis pedidos"]],
    ["Coordinador", ["Revisión"]],
    ["Secretaría", ["Revisión", "Períodos"]],
    ["Decanato", ["Revisión"]],
    ["Administración", ["Revisión"]],
    ["Docente", []],
  ] as const)("%s muestra sólo sus pantallas autorizadas", (role, pantallas) => {
    renderSidebar(role);

    expect(screen.queryByRole("link", { name: "Designaciones" })).not.toBeInTheDocument();
    expect(screen.queryAllByText("DESIGNACIONES")).toHaveLength(pantallas.length ? 1 : 0);
    for (const pantalla of pantallas) {
      expect(screen.getByRole("link", { name: pantalla })).toBeInTheDocument();
    }
  });

  it("marca el hijo activo según la ruta y mantiene el grupo abierto", () => {
    renderSidebar("Secretaría", { route: "/designaciones/periodos" });

    const periodos = screen.getByRole("link", { name: "Períodos" });
    expect(periodos).toHaveAttribute("aria-current", "page");

    expect(screen.queryByRole("link", { name: "Designaciones" })).not.toBeInTheDocument();
  });

  it("modo colapsado: conserva nombres accesibles y foco al recorrer enlaces con teclado", async () => {
    const user = userEvent.setup();
    renderSidebar("Secretaría", { collapsed: true });

    expect(screen.queryByText("DESIGNACIONES")).not.toBeInTheDocument();
    const enlaces = screen.getAllByRole("link");
    expect(screen.getByRole("link", { name: "Revisión" })).toHaveAttribute("title", "Revisión");
    expect(screen.getByRole("link", { name: "Períodos" })).toHaveAttribute("title", "Períodos");

    for (const enlace of enlaces) {
      await user.tab();
      expect(document.activeElement).toBe(enlace);
    }
  });

  it("Docente: no tiene sector Designaciones", () => {
    renderSidebar("Docente", { route: "/aulas" });

    expect(screen.queryByText("DESIGNACIONES")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Reserva de aulas" })).toBeInTheDocument();
  });
});
