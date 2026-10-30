import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { Sidebar } from "./Sidebar";

const cerrarSesion = vi.hoisted(() => vi.fn(async () => {}));
vi.mock("../../shared/auth/auth", () => ({ useCerrarSesion: () => cerrarSesion }));

const usuario: CurrentUser = {
  id: "00000000-0000-4000-8000-000000000001",
  name: "Fernández Castellanos, María Guadalupe",
  initials: "FC",
  upn: "mgfernandez@unlam.edu.ar",
  role: "Docente",
  roleCode: "docente",
  permissions: [],
  asignacion: null,
  asignaciones: [],
  requiereElegirRol: false,
};

function renderSidebar(
  permissions: string[],
  {
    collapsed = false,
    route = "/designaciones",
    onToggleCollapse = () => {},
  }: { collapsed?: boolean; route?: string; onToggleCollapse?: () => void } = {},
) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[route]}>
        <Sidebar
          collapsed={collapsed}
          onToggleCollapse={onToggleCollapse}
          user={{ ...usuario, permissions }}
        />
        <Routes>
          <Route path="/login" element={<p>Pantalla de ingreso</p>} />
          <Route path="*" element={null} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
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

  it.each([
    ["sistema.estado.ver", "Dashboard del sistema", "/sistema", "Registros de auditoría"],
    ["auditoria.ver", "Registros de auditoría", "/auditoria", "Dashboard del sistema"],
  ])("muestra %s sólo con su permiso", (permiso, etiqueta, ruta, oculto) => {
    renderSidebar([permiso]);

    expect(screen.getByRole("link", { name: etiqueta })).toHaveAttribute("href", ruta);
    expect(screen.queryByRole("link", { name: oculto })).not.toBeInTheDocument();
  });

  it("acepta un rol personalizado sin depender del nombre", () => {
    renderSidebar([
      "portal.ver",
      "aulas.ver",
      "tareas.ver",
      "personas.ver",
      "roles.ver",
      "designaciones.gestionar",
      "designaciones.revisar",
      "periodos.administrar",
    ]);

    expect(screen.getByRole("link", { name: "Roles" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Revisión" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Personas" })).toBeInTheDocument();
  });
});

describe("Sidebar — Centro de ayuda", () => {
  it("lo muestra en el pie a un usuario sin permisos de navegación", () => {
    renderSidebar([]);

    expect(screen.getByRole("link", { name: "Centro de ayuda" })).toHaveAttribute("href", "/ayuda");
    expect(screen.queryByText("Personal")).not.toBeInTheDocument();
    expect(screen.queryByText("Trabajo")).not.toBeInTheDocument();
  });

  it("contraída muestra solo el ícono con title y nombre accesible", () => {
    renderSidebar([], { collapsed: true });

    const enlace = screen.getByRole("link", { name: "Centro de ayuda" });
    expect(enlace).toHaveAttribute("title", "Centro de ayuda");
    expect(enlace).toHaveAttribute("aria-label", "Centro de ayuda");
    expect(enlace).not.toHaveTextContent("Centro de ayuda");
  });
});

describe("Sidebar — cabecera y usuario", () => {
  it("el botón Menú pliega la barra lateral", async () => {
    const onToggleCollapse = vi.fn();
    renderSidebar([], { onToggleCollapse });

    await userEvent.click(screen.getByRole("button", { name: "Colapsar menú lateral" }));

    expect(onToggleCollapse).toHaveBeenCalledOnce();
  });

  it("la tarjeta muestra el nombre corto y cierra la sesión desde su menú", async () => {
    renderSidebar([]);

    const tarjeta = screen.getByRole("button", { name: /Menú de usuario/ });
    expect(tarjeta).toHaveTextContent("María Fernández");
    expect(tarjeta).toHaveAttribute("title", "Fernández Castellanos, María Guadalupe · Docente");
    await userEvent.click(tarjeta);
    await userEvent.click(screen.getByRole("menuitem", { name: "Cerrar sesión" }));

    expect(cerrarSesion).toHaveBeenCalledOnce();
    expect(await screen.findByText("Pantalla de ingreso")).toBeInTheDocument();
  });

  it("plegada conserva el menú de usuario en el avatar", async () => {
    renderSidebar([], { collapsed: true });

    await userEvent.click(screen.getByRole("button", { name: /Menú de usuario/ }));

    expect(screen.getByRole("menuitem", { name: "Cerrar sesión" })).toBeInTheDocument();
  });
});
