import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";

import type { CurrentUser } from "../../shared/auth/useCurrentUser";
import { reiniciarGuardias, useGuardiaDeCambios } from "../../shared/navegacion/guardias";
import { MenuUsuario, ModalCambiarRol } from "./MenuUsuario";

const elegir = vi.hoisted(() => ({
  mutateAsync: vi.fn().mockResolvedValue(undefined),
  isPending: false,
  isError: false,
}));
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

function FormularioConCambios() {
  useGuardiaDeCambios(true);
  return null;
}

afterEach(reiniciarGuardias);

describe("rol activo", () => {
  it("al ingresar, cerrar el modal confirma la asignación por defecto", async () => {
    elegir.mutateAsync.mockClear();
    render(
      <MemoryRouter>
        <ModalCambiarRol user={usuario(undefined, true)} alIngresar onCerrar={vi.fn()} />
      </MemoryRouter>,
    );

    expect(screen.getByRole("dialog")).toHaveTextContent("¿Con qué rol querés trabajar?");
    await userEvent.keyboard("{Escape}");

    expect(elegir.mutateAsync).toHaveBeenCalledWith("a-admin");
  });

  it("elegir otra asignación la guarda; elegir la actual desde Cambiar rol no hace nada", async () => {
    elegir.mutateAsync.mockClear();
    render(
      <MemoryRouter>
        <ModalCambiarRol user={usuario()} onCerrar={vi.fn()} />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByText("Administrador de Sistemas"));
    expect(elegir.mutateAsync).not.toHaveBeenCalled();
    await userEvent.click(screen.getByText("Jefe de Cátedra"));
    expect(elegir.mutateAsync).toHaveBeenCalledWith("a-jefe");
  });

  it("con cambios sin guardar pide confirmar antes de cambiar de rol y no lo cambia hasta descartar", async () => {
    elegir.mutateAsync.mockClear();
    render(
      <MemoryRouter>
        <FormularioConCambios />
        <ModalCambiarRol user={usuario()} onCerrar={vi.fn()} />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByText("Jefe de Cátedra"));
    expect(await screen.findByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    expect(elegir.mutateAsync).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole("button", { name: "Seguir editando" }));
    expect(screen.queryByText("Tenés cambios sin guardar.")).not.toBeInTheDocument();
    expect(elegir.mutateAsync).not.toHaveBeenCalled();

    await userEvent.click(screen.getByText("Jefe de Cátedra"));
    await userEvent.click(await screen.findByRole("button", { name: "Descartar cambios" }));
    expect(elegir.mutateAsync).toHaveBeenCalledWith("a-jefe");
  });

  it("si el cambio de rol falla tras descartar, la página vuelve a quedar protegida", async () => {
    elegir.mutateAsync.mockClear().mockRejectedValueOnce(new Error("falló"));
    render(
      <MemoryRouter>
        <FormularioConCambios />
        <ModalCambiarRol user={usuario()} onCerrar={vi.fn()} />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByText("Jefe de Cátedra"));
    await userEvent.click(await screen.findByRole("button", { name: "Descartar cambios" }));
    expect(elegir.mutateAsync).toHaveBeenCalledWith("a-jefe");

    await userEvent.click(screen.getByText("Jefe de Cátedra"));
    expect(await screen.findByText("Tenés cambios sin guardar.")).toBeInTheDocument();
    expect(elegir.mutateAsync).toHaveBeenCalledTimes(1);
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

describe("tema", () => {
  it("elegir un tema lo aplica al documento y lo recuerda en este navegador", async () => {
    render(
      <MemoryRouter>
        <MenuUsuario user={usuario([ADMIN])} />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole("button", { name: /Menú de usuario/ }));
    await userEvent.click(screen.getByRole("menuitemradio", { name: "Oscuro" }));

    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(localStorage.getItem("ars-docendi:tema")).toBe("oscuro");
    expect(screen.getByRole("menuitemradio", { name: "Oscuro" })).toHaveAttribute(
      "aria-checked",
      "true",
    );

    await userEvent.click(screen.getByRole("menuitemradio", { name: "Claro" }));
    expect(document.documentElement.dataset.theme).toBe("light");
  });

  it("con Sistema, el tema es el del dispositivo", async () => {
    vi.stubGlobal("matchMedia", (consulta: string) => ({
      matches: consulta === "(prefers-color-scheme: dark)",
    }));
    render(
      <MemoryRouter>
        <MenuUsuario user={usuario([ADMIN])} />
      </MemoryRouter>,
    );
    await userEvent.click(screen.getByRole("button", { name: /Menú de usuario/ }));
    await userEvent.click(screen.getByRole("menuitemradio", { name: "Sistema" }));

    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(screen.getByRole("menuitemradio", { name: "Sistema" })).toHaveAttribute(
      "aria-checked",
      "true",
    );
    vi.unstubAllGlobals();
  });

  it("al abrir el menú el foco va a la primera opción y las flechas lo recorren", async () => {
    render(
      <MemoryRouter>
        <MenuUsuario user={usuario([ADMIN])} />
      </MemoryRouter>,
    );
    screen.getByRole("button", { name: /Menú de usuario/ }).focus();
    await userEvent.keyboard("{Enter}");
    expect(screen.getByRole("menuitemradio", { name: "Claro" })).toHaveFocus();

    await userEvent.keyboard("{ArrowDown}");
    expect(screen.getByRole("menuitemradio", { name: "Oscuro" })).toHaveFocus();
    await userEvent.keyboard("{ArrowUp}{ArrowUp}");
    expect(screen.getByRole("menuitem", { name: "Cerrar sesión" })).toHaveFocus();
  });
});
