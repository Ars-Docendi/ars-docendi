import { render, screen } from "@testing-library/react";
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
});
