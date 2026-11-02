import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, Link, Outlet, RouterProvider } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";

import { GestorDeNavegacion } from "../../../shared/navegacion/GestorDeNavegacion";
import { reiniciarGuardias } from "../../../shared/navegacion/guardias";
import type { RolMock } from "../models";
import { EditorPage } from "./EditorPage";

const ROL: RolMock = {
  id: "r1",
  codigo: "revisor",
  nombre: "Revisor",
  descripcion: "Revisa",
  scope: "global",
  es_sistema: false,
  activo: true,
  version: 1,
  permisos: [],
};

vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: { permissions: ["roles.ver", "roles.administrar", "roles.gestionar_membresia"] },
  }),
}));
vi.mock("../hooks/useRoles", () => ({
  useRoles: () => ({ consulta: { data: [ROL], isError: false, refetch: vi.fn() } }),
  useCatalogoPermisos: () => ({ data: [], isError: false }),
  useGuardarRol: () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null }),
}));

afterEach(reiniciarGuardias);

function montar() {
  const router = createMemoryRouter(
    [
      {
        element: (
          <>
            <Outlet />
            <GestorDeNavegacion />
            <Link to="/otra">Ir a otra sección</Link>
          </>
        ),
        children: [
          { path: "/roles", element: <p>Listado de roles</p> },
          { path: "/roles/:rolId", element: <EditorPage /> },
          { path: "/otra", element: <p>Otra sección</p> },
        ],
      },
    ],
    { initialEntries: ["/roles?estado=activo", { pathname: "/roles/r1" }], initialIndex: 1 },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe("Editor de roles — cambios sin guardar", () => {
  it("pregunta antes de salir con cambios y no pregunta si no se tocó nada", async () => {
    const router = montar();

    await userEvent.click(screen.getByRole("link", { name: "Ir a otra sección" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/otra"));
    await router.navigate(-1);

    await userEvent.type(await screen.findByLabelText(/Nombre/), " 2");
    await userEvent.click(screen.getByRole("link", { name: "Ir a otra sección" }));

    expect(await screen.findByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/roles/r1");
    expect(screen.getByRole("button", { name: "Seguir editando" })).toHaveFocus();
  });

  it("Cancelar vuelve a la lista sin salir de la aplicación", async () => {
    const router = montar();
    expect(screen.getByRole("link", { name: "Volver a Roles" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(router.state.location.pathname).toBe("/roles"));
  });
});
