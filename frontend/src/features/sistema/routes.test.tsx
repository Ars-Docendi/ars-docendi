import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useCurrentUser } from "../../shared/auth/useCurrentUser";
import type { CurrentUserState } from "../../shared/auth/useCurrentUser";
import { routes } from "./routes";

vi.mock("../../shared/auth/useCurrentUser", () => ({ useCurrentUser: vi.fn() }));

const usuario: CurrentUserState = {
  user: {
    name: "Administración",
    initials: "AD",
    upn: "admin@example.test",
    role: "Administrador de Sistemas",
    roleCode: "sys_admin",
    permissions: [],
  },
  isLoading: false,
  error: null,
  retry: vi.fn(),
};

beforeEach(() => vi.mocked(useCurrentUser).mockReturnValue(usuario));

describe("rutas del sistema protegidas por permiso", () => {
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
