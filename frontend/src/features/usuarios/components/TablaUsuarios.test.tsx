import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

import type { UsuarioMock } from "../models";
import { TablaUsuarios } from "./TablaUsuarios";

const USUARIO: UsuarioMock = {
  id: "usuario-1",
  nombre: "Carla",
  apellido: "López",
  documento: "28341567",
  legajo: "0421",
  cuil: "27-28341567-3",
  fecha_nacimiento: "1980-03-14",
  telefono: "11-4000-0001",
  upn: "carla.lopez@unlam.edu.ar",
  is_active: true,
  roles: ["Docente"],
  membresias: [
    {
      id: "membresia-1",
      rolId: "rol-docente",
      codigo: "docente",
      nombre: "Docente",
      ambito: "materia",
      materiaId: "materia-1",
      carreraId: "carrera-1",
    },
  ],
  persona_id: "persona-1",
  perfilDocente: { esDocente: true, cantidadMaterias: 1 },
};

describe("TablaUsuarios", () => {
  it("no muestra la columna de ámbitos", () => {
    render(
      <TablaUsuarios
        usuarios={[USUARIO]}
        onDesactivar={vi.fn()}
        onActivar={vi.fn()}
        onEditarUsuario={vi.fn()}
      />,
    );

    expect(screen.queryByRole("columnheader", { name: "Ámbitos" })).not.toBeInTheDocument();
  });

  it("mantiene Acciones sin filtro ni orden y no ordena al abrir un filtro", async () => {
    const user = userEvent.setup();
    const onOrdenChange = vi.fn();
    render(
      <TablaUsuarios
        usuarios={[USUARIO]}
        onOrdenChange={onOrdenChange}
        onDesactivar={vi.fn()}
        onActivar={vi.fn()}
        onEditarUsuario={vi.fn()}
      />,
    );

    const acciones = screen.getByRole("columnheader", { name: "Acciones" });
    expect(within(acciones).queryByRole("button")).not.toBeInTheDocument();
    expect(acciones).not.toHaveAttribute("aria-sort");

    await user.click(screen.getByRole("button", { name: "Filtrar Apellido y Nombre" }));
    expect(onOrdenChange).not.toHaveBeenCalled();
  });
});
