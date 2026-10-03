import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";

import { useCurrentUser } from "./useCurrentUser";
import type { CurrentUserState } from "./useCurrentUser";
import { RequirePermission } from "./RequirePermission";

vi.mock("./useCurrentUser", () => ({ useCurrentUser: vi.fn() }));

const usuarioBase: CurrentUserState = {
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

function renderConPermisos(permisos: string[], permission: string | readonly string[]) {
  vi.mocked(useCurrentUser).mockReturnValue({
    ...usuarioBase,
    user: { ...usuarioBase.user!, permissions: permisos },
  });
  const router = createMemoryRouter(
    [
      { path: "/", element: <p>Inicio</p> },
      {
        element: <RequirePermission permission={permission} />,
        children: [{ path: "/protegida", element: <p>Contenido protegido</p> }],
      },
    ],
    { initialEntries: ["/protegida"] },
  );
  render(<RouterProvider router={router} />);
}

describe("RequirePermission — permiso único o any-of", () => {
  it("un string único sigue funcionando sin cambios", async () => {
    renderConPermisos(["auditoria.ver"], "auditoria.ver");
    expect(await screen.findByText("Contenido protegido")).toBeInTheDocument();
  });

  it("un arreglo pasa con cualquiera de los permisos listados", async () => {
    renderConPermisos(
      ["asistente.administrar"],
      ["sistema.estado.ver", "asistente.administrar", "auditoria.ver"],
    );
    expect(await screen.findByText("Contenido protegido")).toBeInTheDocument();
  });

  it("un arreglo redirige a / sin ninguno de los permisos", async () => {
    renderConPermisos([], ["sistema.estado.ver", "asistente.administrar", "auditoria.ver"]);
    expect(await screen.findByText("Inicio")).toBeInTheDocument();
  });
});
