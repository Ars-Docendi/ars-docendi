import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

import { Sidebar } from "./Sidebar";
function renderSidebar(
  permissions: string[],
  { collapsed = false, route = "/designaciones" }: { collapsed?: boolean; route?: string } = {},
) {
  render(
    <MemoryRouter initialEntries={[route]}>
      <Sidebar collapsed={collapsed} permissions={permissions} />
    </MemoryRouter>,
  );
}

describe("Sidebar — sector Designaciones", () => {
  it.each([
    [["designaciones.gestionar"], ["Mis pedidos"]],
    [["designaciones.revisar"], ["Revisión"]],
    [
      ["designaciones.revisar", "periodos.administrar"],
      ["Revisión", "Períodos"],
    ],
    [[], []],
  ] as const)("filtra pantallas por permisos", (permissions, pantallas) => {
    renderSidebar([...permissions]);

    expect(screen.queryByRole("link", { name: "Designaciones" })).not.toBeInTheDocument();
    expect(screen.queryAllByText("Designaciones")).toHaveLength(pantallas.length ? 1 : 0);
    for (const pantalla of pantallas) {
      expect(screen.getByRole("link", { name: pantalla })).toBeInTheDocument();
    }
  });

  it("marca el hijo activo según la ruta y mantiene el grupo abierto", () => {
    renderSidebar(["designaciones.revisar", "periodos.administrar"], {
      route: "/designaciones/periodos",
    });

    const periodos = screen.getByRole("link", { name: "Períodos" });
    expect(periodos).toHaveAttribute("aria-current", "page");

    expect(screen.queryByRole("link", { name: "Designaciones" })).not.toBeInTheDocument();
  });

  it.each([
    ["sistema.estado.ver", "Dashboard del sistema", "/sistema", "Registros de auditoría"],
    ["auditoria.ver", "Registros de auditoría", "/auditoria", "Dashboard del sistema"],
  ])("muestra %s sólo con su permiso", (permiso, etiqueta, ruta, oculto) => {
    renderSidebar([permiso]);

    expect(screen.getByRole("link", { name: etiqueta })).toHaveAttribute("href", ruta);
    expect(screen.queryByRole("link", { name: oculto })).not.toBeInTheDocument();
  });

  it("Docente: no tiene sector Designaciones", () => {
    renderSidebar(["aulas.ver"], { route: "/aulas" });

    expect(screen.queryByText("Designaciones")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Reserva de aulas" })).toBeInTheDocument();
  });

  it("acepta un rol personalizado sin depender del nombre", () => {
    renderSidebar([
      "portal.ver",
      "aulas.ver",
      "tareas.ver",
      "usuarios.ver",
      "docentes.ver",
      "roles.ver",
      "designaciones.gestionar",
      "designaciones.revisar",
      "periodos.administrar",
    ]);

    expect(screen.getByRole("link", { name: "Roles" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Revisión" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Docentes" })).toBeInTheDocument();
  });
});
