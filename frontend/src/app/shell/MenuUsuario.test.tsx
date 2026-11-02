import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { MenuUsuario, ModalCambiarRol } from "./MenuUsuario";

const elegir = vi.hoisted(() => ({ mutate: vi.fn(), isPending: false, isError: false }));
vi.mock("../../shared/auth/rolActivo", async (original) => ({
  ...(await original<typeof import("../../shared/auth/rolActivo")>()),
  useElegirRol: () => elegir,
}));
vi.mock("../../shared/auth/auth", () => ({ useCerrarSesion: () => vi.fn() }));

const ADMIN = {
  id: "a-admin",
  rolCodigo: "sys_admin",
  rolNombre: "Administrador de Sistemas",
  ambitoTipo: "global" as const,
  ambitoNombre: null,
};
const JEFE = {
  id: "a-jefe",
  rolCodigo: "jefe_catedra",
  rolNombre: "Jefe de Cátedra",
  ambitoTipo: "materia" as const,
  ambitoNombre: "Programación I",
};
const usuario = (asignaciones = [ADMIN, JEFE], requiereElegirRol = false): CurrentUser => ({
  id: "u-1",
  name: "Vidal, Ernesto",
  initials: "VE",
  upn: "ernesto@dominio.edu.ar",
  role: ADMIN.rolNombre,
  roleCode: ADMIN.rolCodigo,
  permissions: [],
  asignacion: ADMIN,
  asignaciones,
  requiereElegirRol,
});

describe("rol activo", () => {
  it("al ingresar, cerrar el modal confirma la asignación por defecto", async () => {
    elegir.mutate.mockClear();
    render(
      <MemoryRouter>
        <ModalCambiarRol user={usuario(undefined, true)} alIngresar onCerrar={vi.fn()} />
      </MemoryRouter>,
    );

    expect(screen.getByRole("dialog")).toHaveTextContent("¿Con qué rol querés trabajar?");
    await userEvent.keyboard("{Escape}");

    expect(elegir.mutate).toHaveBeenCalledWith("a-admin", expect.anything());
  });

  it("elegir otra asignación la guarda; elegir la actual desde Cambiar rol no hace nada", async () => {
    elegir.mutate.mockClear();
    render(
      <MemoryRouter>
        <ModalCambiarRol user={usuario()} onCerrar={vi.fn()} />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByText("Administrador de Sistemas"));
    expect(elegir.mutate).not.toHaveBeenCalled();
    await userEvent.click(screen.getByText("Jefe de Cátedra"));
    expect(elegir.mutate).toHaveBeenCalledWith("a-jefe", expect.anything());
  });

  it("el menú ofrece Cambiar rol sólo con más de una asignación", async () => {
    const { unmount } = render(
      <MemoryRouter>
        <MenuUsuario user={usuario([ADMIN])} />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole("button", { name: /Menú de usuario/ }));
    expect(screen.queryByRole("menuitem", { name: "Cambiar rol" })).not.toBeInTheDocument();
    unmount();

    render(
      <MemoryRouter>
        <MenuUsuario user={usuario()} />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole("button", { name: /Menú de usuario/ }));
    await userEvent.click(screen.getByRole("menuitem", { name: "Cambiar rol" }));
    expect(screen.getByRole("dialog")).toHaveTextContent("Programación I");
  });
});
