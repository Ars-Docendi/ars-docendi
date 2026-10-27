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

  it.each([["sistema.estado.ver"], ["asistente.administrar"], ["auditoria.ver"]])(
    "muestra un único enlace «Sistema» con cualquiera de sus tres permisos (%s)",
    (permiso) => {
      renderSidebar([permiso]);

      expect(screen.getByRole("link", { name: "Sistema" })).toHaveAttribute("href", "/sistema");
      expect(screen.queryByRole("link", { name: "Dashboard del sistema" })).not.toBeInTheDocument();
      expect(
        screen.queryByRole("link", { name: "Registros de auditoría" }),
      ).not.toBeInTheDocument();
      expect(screen.queryByRole("link", { name: "Uso del asistente" })).not.toBeInTheDocument();
    },
  );

  it("no muestra «Sistema» sin ninguno de los tres permisos", () => {
    renderSidebar(["portal.ver"], { route: "/portal" });

    expect(screen.queryByRole("link", { name: "Sistema" })).not.toBeInTheDocument();
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
