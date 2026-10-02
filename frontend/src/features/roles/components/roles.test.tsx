import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import type { PermisoRol, RolMock } from "../models";
import { ListaRoles } from "./ListaRoles";
import { ModalEditarRol } from "./ModalEditarRol";
import { PanelPermisos } from "./PanelPermisos";

const ROL_PERSONALIZADO: RolMock = {
  id: "custom",
  codigo: "coordinacion",
  nombre: "Coordinación",
  descripcion: "Descripción",
  scope: "global",
  es_sistema: false,
  activo: true,
  version: 1,
  permisos: [],
};

const ROL_SISTEMA: RolMock = {
  ...ROL_PERSONALIZADO,
  id: "system",
  codigo: "secretaria",
  nombre: "Secretaría",
  es_sistema: true,
};

const PERMISO: PermisoRol = {
  id: "permission",
  codigo: "roles.ver",
  nombre: "Ver roles",
  descripcion: "Consultar roles.",
};

const PROPS_LISTA = {
  busqueda: "",
  onBusquedaChange: vi.fn(),
  rolSeleccionadoId: null,
  onSeleccionar: vi.fn(),
  puedeAdministrar: true,
  onEditar: vi.fn(),
  onEliminar: vi.fn(),
};

describe("componentes unificados de roles", () => {
  it("sólo ofrece baja para roles personalizados", () => {
    render(<ListaRoles {...PROPS_LISTA} roles={[ROL_SISTEMA, ROL_PERSONALIZADO]} />);

    expect(screen.getAllByRole("button", { name: "Editar" })).toHaveLength(2);
    expect(screen.getAllByRole("button", { name: "Eliminar" })).toHaveLength(1);
  });

  it("representa los metadatos del rol de sistema como sólo lectura", () => {
    render(
      <ModalEditarRol
        rol={ROL_SISTEMA}
        nombresExistentes={[]}
        onGuardar={vi.fn()}
        onCerrar={vi.fn()}
      />,
    );

    const dialog = screen.getByRole("dialog");
    expect(
      within(dialog)
        .getAllByRole("textbox")
        .every((campo) => campo.hasAttribute("disabled")),
    ).toBe(true);
    expect(within(dialog).getByRole("combobox")).toBeDisabled();
    expect(within(dialog).getByRole("button", { name: "Guardar" })).toBeDisabled();
    expect(within(dialog).getByText(/código, nombre, descripción y ámbito/i)).toBeInTheDocument();
  });

  it("mantiene el panel de permisos en sólo lectura sin membresía", () => {
    render(
      <PanelPermisos
        rol={ROL_SISTEMA}
        permisos={[PERMISO]}
        asignados={[]}
        puedeEditar={false}
        guardando={false}
        onToggle={vi.fn()}
        onGuardar={vi.fn()}
      />,
    );

    expect(screen.getByRole("checkbox", { name: /ver roles/i })).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Guardar cambios" })).not.toBeInTheDocument();
    expect(screen.getByText("Sólo lectura.")).toBeInTheDocument();
  });

  it("permite modificar y guardar permisos cuando corresponde", async () => {
    const user = userEvent.setup();
    const onToggle = vi.fn();
    const onGuardar = vi.fn();
    render(
      <PanelPermisos
        rol={ROL_PERSONALIZADO}
        permisos={[PERMISO]}
        asignados={[]}
        puedeEditar
        guardando={false}
        onToggle={onToggle}
        onGuardar={onGuardar}
      />,
    );

    await user.click(screen.getByRole("checkbox", { name: /ver roles/i }));
    await user.click(screen.getByRole("button", { name: "Guardar cambios" }));

    expect(onToggle).toHaveBeenCalledWith(PERMISO.id);
    expect(onGuardar).toHaveBeenCalledTimes(1);
  });
});
