import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";

import type { PermisoRol, RolMock } from "../models";
import { PermisosPorModulo } from "./PermisosPorModulo";

const api = vi.hoisted(() => ({
  listarRoles: vi.fn(),
  listarPermisos: vi.fn(),
  crearRol: vi.fn(),
  editarRol: vi.fn(),
  reemplazarPermisos: vi.fn(),
  eliminarRol: vi.fn(),
}));
vi.mock("../api/rolesApi", () => api);
vi.mock("../../../shared/auth/useCurrentUser", () => ({
  useCurrentUser: () => ({
    user: { permissions: ["roles.ver", "roles.administrar", "roles.gestionar_membresia"] },
  }),
}));

const permiso = (id: string, codigo: string, nombre: string): PermisoRol => ({
  id,
  codigo,
  nombre,
  descripcion: `Descripción de ${nombre}`,
});
const CATALOGO = [
  permiso("dv", "designaciones.ver", "Ver designaciones"),
  permiso("dg", "designaciones.gestionar", "Gestionar designaciones"),
  permiso("tv", "tareas.ver", "Ver tareas"),
];
const ROL: RolMock = {
  id: "r1",
  codigo: "coordinacion",
  nombre: "Coordinación",
  descripcion: "Descripción",
  scope: "global",
  es_sistema: false,
  activo: true,
  version: 3,
  permisos: [CATALOGO[0]],
};

describe("permisos por módulo", () => {
  it("muestra el módulo a medias como intermedio y al marcarlo asigna todos sus permisos", async () => {
    const onChange = vi.fn();
    render(
      <PermisosPorModulo
        catalogo={CATALOGO}
        asignados={["dv"]}
        deshabilitado={false}
        onChange={onChange}
      />,
    );

    const modulo = screen.getByRole("checkbox", { name: "Todos los permisos de Designaciones" });
    expect(modulo).toHaveProperty("indeterminate", true);
    expect(screen.getByText("Designaciones").closest("summary")).toHaveTextContent("1/2");

    await userEvent.click(modulo);
    expect(onChange).toHaveBeenCalledWith(["dv", "dg"]);
  });

  it("desmarcar un módulo completo quita sólo los permisos de ese módulo", async () => {
    const onChange = vi.fn();
    render(
      <PermisosPorModulo
        catalogo={CATALOGO}
        asignados={["dv", "dg", "tv"]}
        deshabilitado={false}
        onChange={onChange}
      />,
    );

    await userEvent.click(
      screen.getByRole("checkbox", { name: "Todos los permisos de Designaciones" }),
    );
    expect(onChange).toHaveBeenCalledWith(["tv"]);
  });
});

async function renderEditor(ruta: string) {
  const { EditorPage } = await import("../pages/EditorPage");
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route path="/roles/nuevo" element={<EditorPage />} />
          <Route path="/roles/:rolId" element={<EditorPage />} />
          <Route path="/roles" element={<p>Listado</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("editor de rol", () => {
  it("un rol nuevo arranca sin permisos y se crea con los marcados", async () => {
    api.listarRoles.mockResolvedValue([ROL]);
    api.listarPermisos.mockResolvedValue(CATALOGO);
    api.crearRol.mockResolvedValue({ ...ROL, id: "r2", nombre: "Observador" });
    const user = userEvent.setup();
    await renderEditor("/roles/nuevo");

    const checks = await screen.findAllByRole("checkbox");
    expect(checks.every((c) => !(c as HTMLInputElement).checked)).toBe(true);

    await user.type(screen.getAllByRole("textbox")[0], "Observador");
    await user.click(screen.getByRole("checkbox", { name: "Todos los permisos de Tareas" }));
    await user.click(screen.getByRole("button", { name: "Crear rol" }));

    expect(api.crearRol).toHaveBeenCalledWith(
      { nombre: "Observador", descripcion: "", scope: "global" },
      ["tv"],
    );
    expect(await screen.findByText("Listado")).toBeInTheDocument();
  });

  it("al editar guarda los permisos con la versión que devolvió la edición de datos", async () => {
    api.listarRoles.mockResolvedValue([ROL]);
    api.listarPermisos.mockResolvedValue(CATALOGO);
    api.editarRol.mockResolvedValue({ ...ROL, nombre: "Coordinación general", version: 4 });
    api.reemplazarPermisos.mockResolvedValue([]);
    const user = userEvent.setup();
    await renderEditor("/roles/r1");

    const nombre = await screen.findByDisplayValue("Coordinación");
    await user.clear(nombre);
    await user.type(nombre, "Coordinación general");
    await user.click(screen.getByRole("checkbox", { name: "Todos los permisos de Tareas" }));
    await user.click(screen.getByRole("button", { name: "Guardar cambios" }));

    expect(await screen.findByText("Listado")).toBeInTheDocument();
    expect(api.reemplazarPermisos).toHaveBeenCalledWith(expect.objectContaining({ version: 4 }), [
      "dv",
      "tv",
    ]);
  });

  it("no guarda un nombre que ya usa otro rol, sin distinguir mayúsculas", async () => {
    api.listarRoles.mockResolvedValue([ROL, { ...ROL, id: "r2", nombre: "Observador" }]);
    api.listarPermisos.mockResolvedValue(CATALOGO);
    api.editarRol.mockClear();
    const user = userEvent.setup();
    await renderEditor("/roles/r1");

    const nombre = await screen.findByDisplayValue("Coordinación");
    await user.clear(nombre);
    await user.type(nombre, " observador ");
    await user.click(screen.getByRole("button", { name: "Guardar cambios" }));

    expect(screen.getByText("Ya existe un rol con ese nombre.")).toBeInTheDocument();
    expect(api.editarRol).not.toHaveBeenCalled();
  });
});
