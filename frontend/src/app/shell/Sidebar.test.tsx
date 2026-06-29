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

describe("Sidebar — grupo colapsable Designaciones", () => {
  it("Secretaría: muestra el padre Designaciones con sus hijos Revisión y Períodos abiertos", () => {
    renderSidebar("Secretaría");

    expect(screen.getByRole("link", { name: "Designaciones" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Revisión" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Períodos" })).toBeInTheDocument();

    // Arranca abierto: el chevron está expandido.
    expect(screen.getByRole("button", { name: "Colapsar Designaciones" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
  });

  it("el chevron colapsa y vuelve a expandir los hijos", async () => {
    const user = userEvent.setup();
    renderSidebar("Secretaría");

    await user.click(screen.getByRole("button", { name: "Colapsar Designaciones" }));

    expect(screen.queryByRole("link", { name: "Revisión" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Períodos" })).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Expandir Designaciones" }));

    expect(screen.getByRole("link", { name: "Revisión" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Períodos" })).toBeInTheDocument();
  });

  it("marca el hijo activo según la ruta y mantiene el grupo abierto", () => {
    renderSidebar("Secretaría", { route: "/designaciones/periodos" });

    const periodos = screen.getByRole("link", { name: "Períodos" });
    expect(periodos).toHaveAttribute("aria-current", "page");

    // El padre NO queda marcado cuando un hijo está activo (NavLink end).
    expect(screen.getByRole("link", { name: "Designaciones" })).not.toHaveAttribute("aria-current");
  });

  it("Jefe de Cátedra: el único hijo es Mis pedidos (no Revisión)", () => {
    renderSidebar("Jefe de Cátedra");

    expect(screen.getByRole("link", { name: "Designaciones" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Mis pedidos" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Revisión" })).not.toBeInTheDocument();
  });

  it("modo colapsado: sin chevron, los hijos quedan como enlaces planos alcanzables", () => {
    renderSidebar("Secretaría", { collapsed: true });

    expect(screen.queryByRole("button", { name: /Designaciones/ })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Designaciones" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Revisión" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Períodos" })).toBeInTheDocument();
  });

  it("Docente: no tiene grupo Designaciones", () => {
    renderSidebar("Docente", { route: "/aulas" });

    expect(screen.queryByRole("link", { name: "Designaciones" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Reserva de aulas" })).toBeInTheDocument();
  });
});
