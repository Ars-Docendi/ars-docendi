import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useCurrentUser } from "../../shared/auth/useCurrentUser";
import type { CurrentUserState } from "../../shared/auth/useCurrentUser";
import { routes } from "./routes";

vi.mock("../../shared/auth/useCurrentUser", () => ({ useCurrentUser: vi.fn() }));
// Este test verifica el guard real y las rutas, no las dependencias de las páginas lazy.
vi.mock("./pages/DashboardPage", () => ({ DashboardPage: () => <p>Sistema</p> }));
vi.mock("./pages/AuditoriaPage", () => ({ AuditoriaPage: () => <p>Auditoría</p> }));

const usuario: CurrentUserState = {
  user: {
    id: "a0000000-0000-4000-8000-000000000099",
    name: "Administración",
    initials: "AD",
    upn: "admin@example.test",
    role: "Administrador de Sistemas",
    roleCode: "sys_admin",
    permissions: [],
    asignacion: null,
    asignaciones: [],
    requiereElegirRol: false,
  },
  isLoading: false,
  error: null,
  retry: vi.fn(),
};

beforeEach(() => vi.mocked(useCurrentUser).mockReturnValue(usuario));

describe("rutas del sistema protegidas por permiso", () => {
  it.each([
    ["/sistema", "sistema.estado.ver", "Sistema"],
    ["/auditoria", "auditoria.ver", "Auditoría"],
  ])("permite %s con el permiso %s", async (ruta, permiso, pagina) => {
    vi.mocked(useCurrentUser).mockReturnValue({
      ...usuario,
      user: { ...usuario.user!, permissions: [permiso] },
    });
    const router = createMemoryRouter([{ path: "/", element: <p>Inicio</p> }, ...routes], {
      initialEntries: [ruta],
    });
    render(<RouterProvider router={router} />);
    expect(await screen.findByText(pagina)).toBeInTheDocument();
    expect(screen.queryByText("Inicio")).not.toBeInTheDocument();
  });

  it.each([
    ["/sistema", "auditoria.ver"],
    ["/auditoria", "sistema.estado.ver"],
  ])("redirige %s si sólo está concedido %s", async (ruta, permisoDistinto) => {
    vi.mocked(useCurrentUser).mockReturnValue({
      ...usuario,
      user: { ...usuario.user!, permissions: [permisoDistinto] },
    });
    const router = createMemoryRouter([{ path: "/", element: <p>Inicio</p> }, ...routes], {
      initialEntries: [ruta],
    });

    render(<RouterProvider router={router} />);

    expect(await screen.findByText("Inicio")).toBeInTheDocument();
  });
});
